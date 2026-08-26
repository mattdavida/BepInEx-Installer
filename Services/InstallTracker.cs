using System.Text.Json;
using System.Text.Json.Serialization;

namespace BepInExInstaller.Services;

public enum BepInExChannel
{
    Stable,
    BleedingEdge
}

public enum InstallKind
{
    None,
    Managed,
    Unmanaged
}

public sealed class InstallState
{
    public required InstallKind Kind { get; init; }
    public BepInExChannel? Channel { get; init; }

    public string StatusText => Kind switch
    {
        InstallKind.Managed => $"Currently {FormatChannel(Channel)} (managed by this installer).",
        InstallKind.Unmanaged => "BepInEx is present but was not installed by this app. Install once with the installer before switching Stable/Bleeding Edge, or leftovers may remain.",
        _ => "BepInEx is not installed."
    };

    private static string FormatChannel(BepInExChannel? channel)
        => channel == BepInExChannel.BleedingEdge ? "Bleeding Edge" : "Stable";
}

public sealed class InstallerManifest
{
    public const string FileName = ".bepinex-installer.json";

    public BepInExChannel Channel { get; set; }

    public List<string> Files { get; set; } = [];
}

public static class InstallTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static InstallState Detect(string gamePath)
    {
        var manifest = TryLoad(gamePath);
        if (manifest is not null)
            return new InstallState { Kind = InstallKind.Managed, Channel = manifest.Channel };

        if (LooksInstalled(gamePath))
            return new InstallState { Kind = InstallKind.Unmanaged };

        return new InstallState { Kind = InstallKind.None };
    }

    public static InstallerManifest? TryLoad(string gamePath)
    {
        foreach (var path in ManifestPaths(gamePath))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                var manifest = JsonSerializer.Deserialize<InstallerManifest>(json, JsonOptions);
                if (manifest is not null)
                    return manifest;
            }
            catch
            {
                // Ignore a corrupt marker and treat the install as unmanaged.
            }
        }

        return null;
    }

    public static void Save(string gamePath, InstallerManifest manifest)
    {
        var path = PreferredManifestPath(gamePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public static string PreferredManifestPath(string gamePath)
        => Path.Combine(gamePath, "BepInEx", InstallerManifest.FileName);

    private static IEnumerable<string> ManifestPaths(string gamePath)
    {
        yield return PreferredManifestPath(gamePath);
        yield return Path.Combine(gamePath, InstallerManifest.FileName);
    }

    private static bool LooksInstalled(string gamePath)
        => Directory.Exists(Path.Combine(gamePath, "BepInEx"))
           || File.Exists(Path.Combine(gamePath, "doorstop_config.ini"))
           || File.Exists(Path.Combine(gamePath, "winhttp.dll"))
           || File.Exists(Path.Combine(gamePath, "version.dll"));
}
