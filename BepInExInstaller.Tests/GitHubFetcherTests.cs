using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class GitHubFetcherTests
{
    [Fact]
    public void Stable_picks_the_matching_win_x64_zip_and_ignores_patcher()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx_Patcher_5.4.23.5.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-09T00:00:00Z") },
            new() { Name = "BepInEx_win_x86_5.4.23.5.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-08T00:00:00Z") },
            new() { Name = "BepInEx_win_x64_5.4.23.5.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-08T00:00:00Z") },
            new() { Name = "BepInEx_linux_x64_5.4.23.5.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-08T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectAsset(
            assets, BepInExChannel.Stable, GameOs.Windows, GameArch.X64, ScriptingBackend.Mono);
        Assert.Equal("BepInEx_win_x64_5.4.23.5.zip", picked?.Name);
    }

    [Fact]
    public void Bleeding_edge_picks_the_Unity_IL2CPP_zip()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx-Unity.Mono-win-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z") },
            new() { Name = "BepInEx-Unity.IL2CPP-win-x86-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z") },
            new() { Name = "BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z") },
            new() { Name = "BepInEx-NET.CoreCLR-net6.0-win-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2024-01-02T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectAsset(
            assets, BepInExChannel.BleedingEdge, GameOs.Windows, GameArch.X64, ScriptingBackend.Il2Cpp);
        Assert.Equal("BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip", picked?.Name);
    }

    [Fact]
    public void Bleeding_edge_picks_Unity_Mono_for_linux()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx-Unity.Mono-linux-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z") },
            new() { Name = "BepInEx-Unity.Mono-win-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectAsset(
            assets, BepInExChannel.BleedingEdge, GameOs.Linux, GameArch.X64, ScriptingBackend.Mono);
        Assert.Equal("BepInEx-Unity.Mono-linux-x64-6.0.0-pre.2.zip", picked?.Name);
    }

    [Fact]
    public void Stable_macos_accepts_universal_zip()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx_macos_universal_5.4.23.5.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-09T00:00:00Z") },
            new() { Name = "BepInEx_win_x64_5.4.23.5.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-09T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectAsset(
            assets, BepInExChannel.Stable, GameOs.MacOs, GameArch.X64, ScriptingBackend.Mono);
        Assert.Equal("BepInEx_macos_universal_5.4.23.5.zip", picked?.Name);
    }

    [Fact]
    public void Complete_download_is_accepted()
    {
        using var temp = new TempDir();
        var file = temp.Combine("asset.zip");
        File.WriteAllBytes(file, [1, 2, 3, 4]);
        GitHubFetcher.EnsureCompleteDownload(file, expectedSize: 4);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Incomplete_download_is_deleted_and_throws()
    {
        using var temp = new TempDir();
        var file = temp.Combine("asset.zip");
        File.WriteAllBytes(file, [1, 2]);
        var ex = Assert.Throws<IOException>(() => GitHubFetcher.EnsureCompleteDownload(file, expectedSize: 99));
        Assert.Contains("didn't finish", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Release_url_rejects_unsafe_repo_parts()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GitHubFetcher.ReleaseTagUrl("BepInEx", "BepInEx", "v5/latest"));
    }

    [Fact]
    public void V_Rising_picks_the_community_zip_not_official_IL2CPP()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-09T00:00:00Z") },
            new() { Name = "BepInEx_V-Rising_Experimental_Dev_1.733.2.zip", UpdatedAt = DateTimeOffset.Parse("2025-04-28T00:00:00Z") },
            new() { Name = "source.zip", UpdatedAt = DateTimeOffset.Parse("2025-04-29T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectAsset(
            assets,
            BepInExChannel.BleedingEdge,
            GameOs.Windows,
            GameArch.X64,
            ScriptingBackend.Il2Cpp,
            BepInExAssetStyle.VRising);
        Assert.Equal("BepInEx_V-Rising_Experimental_Dev_1.733.2.zip", picked?.Name);
    }

    [Fact]
    public void V_Rising_style_ignores_official_zips()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip", UpdatedAt = DateTimeOffset.Parse("2026-02-09T00:00:00Z") }
        };

        Assert.Null(GitHubFetcher.SelectAsset(
            assets,
            BepInExChannel.BleedingEdge,
            GameOs.Windows,
            GameArch.X64,
            ScriptingBackend.Il2Cpp,
            BepInExAssetStyle.VRising));
    }

    [Fact]
    public void V_Rising_release_url_uses_the_community_tag()
    {
        Assert.Equal(
            "https://api.github.com/repos/decaprime/VRising-Modding/releases/tags/1.733.2",
            GitHubFetcher.ReleaseTagUrl(
                BepInExReleaseSource.VRising.Owner,
                BepInExReleaseSource.VRising.Repo,
                BepInExReleaseSource.VRising.Tag));
    }

    [Fact]
    public void Configuration_manager_picks_BepInEx5_zip_for_Mono()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx.ConfigurationManager_IL2CPP_v19.0.zip", UpdatedAt = DateTimeOffset.Parse("2026-06-29T00:00:00Z") },
            new() { Name = "BepInEx.ConfigurationManager_BepInEx5_v19.0.zip", UpdatedAt = DateTimeOffset.Parse("2026-06-29T00:00:00Z") },
            new() { Name = "source.zip", UpdatedAt = DateTimeOffset.Parse("2026-06-30T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectConfigurationManagerAsset(assets, il2cpp: false);
        Assert.Equal("BepInEx.ConfigurationManager_BepInEx5_v19.0.zip", picked?.Name);
    }

    [Fact]
    public void Configuration_manager_picks_IL2CPP_zip_and_ignores_BepInEx5()
    {
        var assets = new List<GitHubAsset>
        {
            new() { Name = "BepInEx.ConfigurationManager_BepInEx5_v19.0.zip", UpdatedAt = DateTimeOffset.Parse("2026-06-29T00:00:00Z") },
            new() { Name = "BepInEx.ConfigurationManager_IL2CPP_v19.0.zip", UpdatedAt = DateTimeOffset.Parse("2026-06-28T00:00:00Z") }
        };

        var picked = GitHubFetcher.SelectConfigurationManagerAsset(assets, il2cpp: true);
        Assert.Equal("BepInEx.ConfigurationManager_IL2CPP_v19.0.zip", picked?.Name);
    }
}
