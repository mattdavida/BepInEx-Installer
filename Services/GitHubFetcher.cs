using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BepInExInstaller.Services;

public enum BepInExAssetStyle
{
    Official,
    VRising
}

/// <summary>
/// Zip to pull for a known game. Most games use a GitHub release
/// (<see cref="Owner"/>/<see cref="Repo"/>@<see cref="Tag"/>). Garden of Witches
/// uses a pinned BepInBuilds artifact via <see cref="DirectDownloadUrl"/>.
/// </summary>
public sealed record BepInExReleaseSource(
    string Owner,
    string Repo,
    string Tag,
    BepInExAssetStyle AssetStyle = BepInExAssetStyle.Official,
    string? DirectDownloadUrl = null)
{
    public static BepInExReleaseSource VRising { get; } = new(
        "decaprime",
        "VRising-Modding",
        "1.733.2",
        BepInExAssetStyle.VRising);

    // Stock GitHub pre.2 dies with "We support 23-29, got 31". BE 785 reads
    // metadata v31 and was verified against Garden of Witches (Unity 2022.3.62f3).
    public static BepInExReleaseSource GardenOfWitches { get; } = new(
        "BepInEx",
        "BepInEx",
        "6.0.0-be.785+6abdba4",
        BepInExAssetStyle.Official,
        "https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785%2B6abdba4.zip");

    public bool HasDirectDownload => !string.IsNullOrWhiteSpace(DirectDownloadUrl);

    public string Label => HasDirectDownload
        ? $"builds.bepinex.dev {Tag}"
        : $"{Owner}/{Repo} {Tag}";
}

/// <summary>
/// Downloads BepInEx from GitHub. Stable uses <c>/releases/latest</c> (BepInEx 5, Mono).
/// Bleeding Edge walks recent releases (including prereleases) for a BepInEx 6 Unity zip
/// that matches the game's backend, OS, and architecture. Known games can pin a
/// <see cref="BepInExReleaseSource"/> (GitHub tag or a BepInBuilds URL).
/// </summary>
public static class GitHubFetcher
{
    private static readonly Regex SafeRepoPart = new(
        @"^[A-Za-z0-9_.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HttpClient Http = CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<string> DownloadAsync(
        BepInExChannel channel,
        GameOs os,
        GameArch architecture,
        ScriptingBackend backend,
        BepInExReleaseSource? source = null,
        CancellationToken cancellationToken = default)
    {
        if (source is not null)
        {
            if (source.HasDirectDownload)
                return await DownloadDirectAsync(source, cancellationToken);

            var release = await GetReleaseAsync(
                ReleaseTagUrl(source.Owner, source.Repo, source.Tag),
                cancellationToken);
            var community = SelectAsset(release.Assets, channel, os, architecture, backend, source.AssetStyle);
            if (community is null)
            {
                throw new InvalidOperationException(
                    $"No matching zip was found on {source.Label}.");
            }

            return await DownloadAssetAsync(community, cancellationToken);
        }

        if (channel == BepInExChannel.Stable && backend == ScriptingBackend.Il2Cpp)
        {
            throw new InvalidOperationException(
                "IL2CPP games need Bleeding Edge (BepInEx 6). Stable is BepInEx 5 and only supports Unity Mono.");
        }

        if (architecture == GameArch.Unknown)
            architecture = GameArch.X64;

        var resolvedBackend = backend == ScriptingBackend.Unknown
            ? ScriptingBackend.Mono
            : backend;

        GitHubAsset? asset;
        if (channel == BepInExChannel.Stable)
        {
            var release = await GetReleaseAsync(
                "https://api.github.com/repos/BepInEx/BepInEx/releases/latest",
                cancellationToken);
            asset = SelectAsset(release.Assets, channel, os, architecture, resolvedBackend);
            if (asset is null)
            {
                throw new InvalidOperationException(
                    $"No matching Stable zip was found for {Describe(os, architecture, resolvedBackend)}.");
            }
        }
        else
        {
            asset = await FindBleedingEdgeAssetAsync(os, architecture, resolvedBackend, cancellationToken);
            if (asset is null)
            {
                throw new InvalidOperationException(
                    $"No matching Bleeding Edge zip was found for {Describe(os, architecture, resolvedBackend)}.");
            }
        }

        return await DownloadAssetAsync(asset, cancellationToken);
    }

    private static async Task<GitHubAsset?> FindBleedingEdgeAssetAsync(
        GameOs os,
        GameArch architecture,
        ScriptingBackend backend,
        CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(
            "https://api.github.com/repos/BepInEx/BepInEx/releases?per_page=30",
            cancellationToken);
        EnsureApiSuccess(response);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken)
                       ?? [];

        foreach (var release in releases)
        {
            var asset = SelectAsset(release.Assets, BepInExChannel.BleedingEdge, os, architecture, backend);
            if (asset is not null)
                return asset;
        }

        return null;
    }

    private static async Task<GitHubRelease> GetReleaseAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, cancellationToken);
        EnsureApiSuccess(response);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("GitHub returned an empty release payload.");
    }

    private static async Task<string> DownloadDirectAsync(
        BepInExReleaseSource source,
        CancellationToken cancellationToken)
    {
        var url = source.DirectDownloadUrl!;
        EnsureTrustedDownloadUrl(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Pinned BepInEx download URL is invalid.");

        var fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
        if (string.IsNullOrWhiteSpace(fileName)
            || !fileName.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pinned BepInEx download URL is not a BepInEx zip.");
        }

        return await DownloadToTempAsync(url, fileName, expectedSize: 0, cancellationToken);
    }

    private static async Task<string> DownloadAssetAsync(GitHubAsset asset, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(asset.Name);
        if (string.IsNullOrWhiteSpace(fileName)
            || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("GitHub returned an unexpected asset name.");
        }

        return await DownloadToTempAsync(asset.BrowserDownloadUrl, fileName, asset.Size, cancellationToken);
    }

    private static async Task<string> DownloadToTempAsync(
        string url,
        string fileName,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        EnsureTrustedDownloadUrl(url);

        var downloadDir = Path.Combine(Path.GetTempPath(), "BepInExInstaller");
        Directory.CreateDirectory(downloadDir);
        var destination = Path.Combine(downloadDir, fileName);

        await using (var remote = await Http.GetStreamAsync(url, cancellationToken))
        await using (var file = File.Create(destination))
        {
            await remote.CopyToAsync(file, cancellationToken);
        }

        EnsureCompleteDownload(destination, expectedSize);
        return destination;
    }

    public static async Task<string> DownloadConfigurationManagerAsync(
        bool il2cpp,
        CancellationToken cancellationToken = default)
    {
        var release = await GetReleaseAsync(
            "https://api.github.com/repos/BepInEx/BepInEx.ConfigurationManager/releases/latest",
            cancellationToken);
        var asset = SelectConfigurationManagerAsset(release.Assets, il2cpp);
        if (asset is null)
        {
            throw new InvalidOperationException(il2cpp
                ? "No IL2CPP Configuration Manager zip was found on GitHub."
                : "No BepInEx 5 Configuration Manager zip was found on GitHub.");
        }

        return await DownloadAssetAsync(asset, cancellationToken);
    }

    internal static void EnsureCompleteDownload(string path, long expectedSize)
    {
        if (expectedSize <= 0)
            return;

        var actual = new FileInfo(path).Length;
        if (actual == expectedSize)
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort; the size check is what matters.
        }

        throw new IOException("Download didn't finish. Try again.");
    }

    internal static GitHubAsset? SelectAsset(
        IReadOnlyList<GitHubAsset> assets,
        BepInExChannel channel,
        GameOs os,
        GameArch architecture,
        ScriptingBackend backend,
        BepInExAssetStyle style = BepInExAssetStyle.Official)
    {
        IEnumerable<GitHubAsset> matches = style == BepInExAssetStyle.VRising
            ? assets.Where(a => IsVRisingZip(a.Name))
            : channel == BepInExChannel.BleedingEdge
                ? assets.Where(a => IsUnity6Zip(a.Name, os, architecture, backend))
                : assets.Where(a => IsStableZip(a.Name, os, architecture));

        return matches
            .OrderByDescending(a => a.UpdatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();
    }

    // e.g. BepInEx_win_x64_5.4.23.5.zip — never Patcher or v6 Unity packs.
    internal static bool IsStableZip(string name, GameOs os, GameArch architecture)
    {
        var file = Path.GetFileName(name);
        if (!file.StartsWith("BepInEx_", StringComparison.OrdinalIgnoreCase)
            || !file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || file.Contains("Patcher", StringComparison.OrdinalIgnoreCase)
            || file.Contains("IL2CPP", StringComparison.OrdinalIgnoreCase)
            || file.Contains("Unity.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (os == GameOs.MacOs)
            return file.Contains("macos", StringComparison.OrdinalIgnoreCase);

        var needle = $"_{UnityGameDetector.FormatOs(os)}_{UnityGameDetector.FormatArch(architecture)}_";
        return file.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    // e.g. BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip
    internal static bool IsUnity6Zip(string name, GameOs os, GameArch architecture, ScriptingBackend backend)
    {
        var file = Path.GetFileName(name);
        if (!file.StartsWith("BepInEx-Unity.", StringComparison.OrdinalIgnoreCase)
            || !file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var backendToken = backend == ScriptingBackend.Il2Cpp ? "IL2CPP" : "Mono";
        var osToken = UnityGameDetector.FormatOs(os);
        var archToken = UnityGameDetector.FormatArch(architecture);
        var needle = $"-{osToken}-{archToken}-";

        return file.Contains($".{backendToken}-", StringComparison.OrdinalIgnoreCase)
               && file.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    // e.g. BepInEx_V-Rising_Experimental_Dev_1.733.2.zip
    internal static bool IsVRisingZip(string name)
    {
        var file = Path.GetFileName(name);
        if (!file.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)
            || !file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return file.Contains("V-Rising", StringComparison.OrdinalIgnoreCase)
               || file.Contains("V_Rising", StringComparison.OrdinalIgnoreCase)
               || file.Contains("VRising", StringComparison.OrdinalIgnoreCase);
    }

    // e.g. BepInEx.ConfigurationManager_BepInEx5_v19.0.zip
    //      BepInEx.ConfigurationManager_IL2CPP_v19.0.zip
    internal static GitHubAsset? SelectConfigurationManagerAsset(IReadOnlyList<GitHubAsset> assets, bool il2cpp)
        => assets
            .Where(a => IsConfigurationManagerZip(a.Name, il2cpp))
            .OrderByDescending(a => a.UpdatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

    internal static bool IsConfigurationManagerZip(string name, bool il2cpp)
    {
        var file = Path.GetFileName(name);
        if (!file.StartsWith("BepInEx.ConfigurationManager_", StringComparison.OrdinalIgnoreCase)
            || !file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isIl2Cpp = file.Contains("_IL2CPP_", StringComparison.OrdinalIgnoreCase)
                       || file.Contains("_IL2CPP.", StringComparison.OrdinalIgnoreCase);
        return il2cpp == isIl2Cpp;
    }

    internal static string Describe(GameOs os, GameArch architecture, ScriptingBackend backend)
        => $"{UnityGameDetector.FormatBackend(backend)} {UnityGameDetector.FormatOs(os)} {UnityGameDetector.FormatArch(architecture)}";

    internal static string ReleaseTagUrl(string owner, string repo, string tag)
    {
        if (!SafeRepoPart.IsMatch(owner) || !SafeRepoPart.IsMatch(repo) || !SafeRepoPart.IsMatch(tag))
            throw new InvalidOperationException("Invalid GitHub repository.");

        return $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BepInEx-Installer-v1");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var token = ReadOptionalToken();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static string? ReadOptionalToken()
    {
        foreach (var name in new[] { "BEPINEX_INSTALLER_GITHUB_TOKEN", "GITHUB_TOKEN" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static void EnsureApiSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden
            or System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException(
                "GitHub's download listing limit was reached for this network. Wait and try again, or set BEPINEX_INSTALLER_GITHUB_TOKEN. A GitHub account is not required for normal use.");
        }

        response.EnsureSuccessStatusCode();
    }

    internal static void EnsureTrustedDownloadUrl(string url)
    {
        if (!IsTrustedDownloadUrl(url))
            throw new InvalidOperationException("Download URL is not from a trusted BepInEx host.");
    }

    internal static bool IsTrustedDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!host.Equals("builds.bepinex.dev", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 4
               && parts[0].Equals("projects", StringComparison.OrdinalIgnoreCase)
               && parts[1].Equals("bepinex_be", StringComparison.OrdinalIgnoreCase)
               && parts[2].All(char.IsAsciiDigit)
               && parts[3].StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)
               && parts[3].EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class GitHubRelease
{
    public List<GitHubAsset> Assets { get; set; } = [];
}

internal sealed class GitHubAsset
{
    public string Name { get; set; } = "";

    public string BrowserDownloadUrl { get; set; } = "";

    public long Size { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
