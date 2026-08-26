namespace BepInExInstaller.Services;

public sealed class KnownGamePack
{
    public required string DisplayName { get; init; }
    public string[] SteamAppIds { get; init; } = [];
    public string[] NameContains { get; init; } = [];
    public string[] FolderNames { get; init; } = [];
    public BepInExReleaseSource? Source { get; init; }
    public bool HasCustomSource => Source is not null;
    public string? BadgeText { get; init; }
    public string? InstallHint { get; init; }
}

/// <summary>
/// Game-specific BepInEx handling. Matched by Steam app id, then name, then folder.
/// V Rising downloads the community pack from <see cref="BepInExReleaseSource.VRising"/>
/// instead of stock GitHub Bleeding Edge (Cpp2IL in pre.2 cannot read metadata v31).
/// </summary>
public static class KnownGameCatalog
{
    public static readonly IReadOnlyList<KnownGamePack> Packs =
    [
        // V Rising wiki / Thunderstore recommend BepInExPack_V_Rising (BE 733 + Il2CppInterop
        // patches). GitHub pre.2 dies with "We support 23-29, got 31".
        new KnownGamePack
        {
            DisplayName = "V Rising BepInEx",
            SteamAppIds = ["1604030", "1829350"],
            NameContains = ["V Rising", "VRising"],
            FolderNames = ["VRising", "V Rising", "VRisingDedicatedServer"],
            Source = BepInExReleaseSource.VRising,
            BadgeText = "V Rising zip",
            InstallHint = "Uses the V Rising community pack (1.733.2), not stock Bleeding Edge."
        }
    ];

    public static KnownGamePack? Find(DetectedGame game)
        => Find(game.AppId, game.Name, game.InstallPath, game.GamePath);

    public static KnownGamePack? Find(string? appId, string? name, string? installPath, string? gamePath)
    {
        foreach (var pack in Packs)
        {
            if (Matches(pack, appId, name, installPath, gamePath))
                return pack;
        }

        return null;
    }

    private static bool Matches(
        KnownGamePack pack,
        string? appId,
        string? name,
        string? installPath,
        string? gamePath)
    {
        if (!string.IsNullOrEmpty(appId))
        {
            foreach (var id in pack.SteamAppIds)
            {
                if (string.Equals(id, appId, StringComparison.Ordinal))
                    return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var token in pack.NameContains)
            {
                if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return FolderMatches(pack, installPath) || FolderMatches(pack, gamePath);
    }

    private static bool FolderMatches(KnownGamePack pack, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || pack.FolderNames.Length == 0)
            return false;

        foreach (var segment in path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var folder in pack.FolderNames)
            {
                if (segment.Equals(folder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
