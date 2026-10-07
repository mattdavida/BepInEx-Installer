using System.Text.RegularExpressions;

namespace BepInExInstaller.Services;

public sealed class KnownGamePack
{
    public required string DisplayName { get; init; }
    public string[] SteamAppIds { get; init; } = [];
    public string[] NameContains { get; init; } = [];
    public string[] FolderNames { get; init; } = [];
    public BepInExReleaseSource? Source { get; init; }
    public UnstrippedLibraries? UnstrippedLibraries { get; init; }
    public bool HasCustomSource => Source is not null;
    public bool NeedsUnstrippedLibraries => UnstrippedLibraries is not null;
    public bool IgnoreDoorstopDisableSwitch { get; init; }
    public bool DisableUnityLogListening { get; init; }
    public string? BadgeText { get; init; }
    public string? InstallHint { get; init; }
}

/// <summary>
/// Unstripped Mono + Unity assemblies from unity.bepinex.dev. Needed when the
/// game's <c>mscorlib</c> is linker-stripped (BepInEx dies on <c>Module.GetPEKind</c>).
/// </summary>
public sealed record UnstrippedLibraries(
    string CorlibsUrl,
    string? EngineLibrariesUrl = null,
    string Folder = "unstripped_corlib")
{
    private static readonly Regex UnityVersion = new(
        @"^\d+(\.\d+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static UnstrippedLibraries ForUnity(string version)
    {
        if (!UnityVersion.IsMatch(version))
            throw new ArgumentException("Invalid Unity version.", nameof(version));

        return new(
            $"https://unity.bepinex.dev/corlibs/{version}.zip",
            $"https://unity.bepinex.dev/libraries/{version}.zip");
    }

    public IEnumerable<string> DownloadUrls()
    {
        yield return CorlibsUrl;
        if (!string.IsNullOrWhiteSpace(EngineLibrariesUrl))
            yield return EngineLibrariesUrl;
    }
}

/// <summary>
/// Game-specific BepInEx handling. Matched by Steam app id, then name, then folder.
/// V Rising downloads the community pack from <see cref="BepInExReleaseSource.VRising"/>.
/// Garden of Witches pins BepInEx 6.0.0-be.785 from BepInBuilds
/// (GitHub pre.2 cannot read IL2CPP metadata v31).
/// The Rogue Prince of Persia and Tainted Grail: The Fall of Avalon pin 6.0.0-be.788
/// for the same metadata error on Unity 6.
/// Prince of Persia: The Lost Crown stays on stock Bleeding Edge and enables Doorstop's
/// ignore switch (the game sets DOORSTOP_DISABLE, so BepInEx never starts).
/// Skul stays on Stable and adds unstripped Unity 2020.3.34 corlibs.
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
        },
        // Unity 2022.3.62f3 / metadata v31. Verified with official BE 785.
        new KnownGamePack
        {
            DisplayName = "Garden of Witches BepInEx",
            SteamAppIds = ["2530470"],
            NameContains = ["Garden of Witches"],
            FolderNames = ["Garden of Witches"],
            Source = BepInExReleaseSource.GardenOfWitches,
            BadgeText = "BE 785",
            InstallHint = "Uses BepInEx 6.0.0-be.785 from builds.bepinex.dev, not stock GitHub pre.2."
        },
        // Unity 6000.0.64f1 / metadata v31. GitHub pre.2 (be.697) reports
        // "We support 23-29, got 31". No community pack; BE 788 reads v31.
        new KnownGamePack
        {
            DisplayName = "The Rogue Prince of Persia BepInEx",
            SteamAppIds = ["2717880"],
            NameContains = ["The Rogue Prince of Persia", "Rogue Prince of Persia"],
            FolderNames = ["The Rogue Prince Of Persia"],
            Source = BepInExReleaseSource.RoguePrince,
            BadgeText = "BE 788",
            InstallHint = "Uses BepInEx 6.0.0-be.788 from builds.bepinex.dev, not stock GitHub pre.2."
        },
        // Unity 6000.0.x / metadata v31. GitHub pre.2 reports "We support 23-29, got 31".
        // Same BE 788 pin as The Rogue Prince of Persia.
        new KnownGamePack
        {
            DisplayName = "Tainted Grail Fall of Avalon BepInEx",
            SteamAppIds = ["1466060"],
            NameContains = ["Tainted Grail: The Fall of Avalon", "Fall of Avalon"],
            FolderNames = ["Tainted Grail FoA"],
            Source = BepInExReleaseSource.TaintedGrail,
            BadgeText = "BE 788",
            InstallHint = "Uses BepInEx 6.0.0-be.788 from builds.bepinex.dev, not stock GitHub pre.2."
        },
        // Unity IL2CPP, metadata v29. Stock pre.2 can read it, but the game sets
        // DOORSTOP_DISABLE so the proxy exits before BepInEx logs anything.
        // Lyall's PoPTLCFix pack sets ignore_disable_switch and turns Unity log listening off.
        new KnownGamePack
        {
            DisplayName = "Prince of Persia The Lost Crown BepInEx",
            SteamAppIds = ["2751000"],
            NameContains = ["Prince of Persia The Lost Crown", "The Lost Crown"],
            FolderNames = ["Prince of Persia The Lost Crown"],
            IgnoreDoorstopDisableSwitch = true,
            DisableUnityLogListening = true,
            BadgeText = "Doorstop fix",
            InstallHint = "Enables Doorstop's ignore switch and disables Unity log listening so BepInEx can start."
        },
        // Unity 2020.3.34 Mono. Stock BepInEx 5 dies with MissingMethodException
        // Module.GetPEKind — mscorlib is linker-stripped (2.6 MB vs 4.0 MB unstripped).
        new KnownGamePack
        {
            DisplayName = "Skul BepInEx",
            SteamAppIds = ["1147560"],
            NameContains = ["Skul"],
            FolderNames = ["Skul"],
            UnstrippedLibraries = UnstrippedLibraries.ForUnity("2020.3.34"),
            BadgeText = "Unstripped libs",
            InstallHint = "Adds unstripped Unity 2020.3.34 corlibs. Stock BepInEx cannot load this game's stripped mscorlib."
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
