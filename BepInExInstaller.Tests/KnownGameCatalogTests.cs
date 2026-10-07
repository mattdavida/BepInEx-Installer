using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class KnownGameCatalogTests
{
    [Fact]
    public void Matches_V_Rising_by_app_id_and_uses_the_community_zip()
    {
        var pack = KnownGameCatalog.Find("1604030", "Something Else", null, null);
        Assert.Equal("V Rising BepInEx", pack?.DisplayName);
        Assert.True(pack?.HasCustomSource);
        Assert.Equal(BepInExReleaseSource.VRising, pack?.Source);
        Assert.Equal("V Rising zip", pack?.BadgeText);
        Assert.Contains("1.733.2", pack!.InstallHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Matches_V_Rising_dedicated_server_by_app_id()
    {
        var pack = KnownGameCatalog.Find("1829350", "Dedicated", null, null);
        Assert.Equal(BepInExReleaseSource.VRising, pack?.Source);
    }

    [Fact]
    public void Matches_V_Rising_by_folder_name()
    {
        var pack = KnownGameCatalog.Find(null, null, @"D:\SteamLibrary\steamapps\common\VRising", null);
        Assert.Equal(BepInExReleaseSource.VRising, pack?.Source);
    }

    [Fact]
    public void Matches_V_Rising_by_name()
    {
        var pack = KnownGameCatalog.Find(null, "V Rising", null, null);
        Assert.Equal(BepInExReleaseSource.VRising, pack?.Source);
    }

    [Fact]
    public void Matches_Garden_of_Witches_by_app_id_and_pins_BE_785()
    {
        var pack = KnownGameCatalog.Find("2530470", "Something Else", null, null);
        Assert.Equal("Garden of Witches BepInEx", pack?.DisplayName);
        Assert.True(pack?.HasCustomSource);
        Assert.Equal(BepInExReleaseSource.GardenOfWitches, pack?.Source);
        Assert.Equal("BE 785", pack?.BadgeText);
        Assert.Contains("6.0.0-be.785", pack!.InstallHint, StringComparison.OrdinalIgnoreCase);
        Assert.True(pack.Source!.HasDirectDownload);
    }

    [Fact]
    public void Matches_Garden_of_Witches_by_folder_name()
    {
        var pack = KnownGameCatalog.Find(
            null, null, @"D:\SteamLibrary\steamapps\common\Garden of Witches", null);
        Assert.Equal(BepInExReleaseSource.GardenOfWitches, pack?.Source);
    }

    [Fact]
    public void Matches_Garden_of_Witches_by_name()
    {
        var pack = KnownGameCatalog.Find(null, "Garden of Witches", null, null);
        Assert.Equal(BepInExReleaseSource.GardenOfWitches, pack?.Source);
    }

    [Fact]
    public void Matches_Rogue_Prince_by_app_id_and_pins_BE_788()
    {
        var pack = KnownGameCatalog.Find("2717880", "Something Else", null, null);
        Assert.Equal("The Rogue Prince of Persia BepInEx", pack?.DisplayName);
        Assert.True(pack?.HasCustomSource);
        Assert.Equal(BepInExReleaseSource.RoguePrince, pack?.Source);
        Assert.Equal("BE 788", pack?.BadgeText);
        Assert.Contains("6.0.0-be.788", pack!.InstallHint, StringComparison.OrdinalIgnoreCase);
        Assert.True(pack.Source!.HasDirectDownload);
    }

    [Fact]
    public void Matches_Rogue_Prince_by_folder_name()
    {
        var pack = KnownGameCatalog.Find(
            null, null, @"D:\SteamLibrary\steamapps\common\The Rogue Prince Of Persia", null);
        Assert.Equal(BepInExReleaseSource.RoguePrince, pack?.Source);
    }

    [Fact]
    public void Matches_Rogue_Prince_by_name()
    {
        var pack = KnownGameCatalog.Find(null, "The Rogue Prince of Persia", null, null);
        Assert.Equal(BepInExReleaseSource.RoguePrince, pack?.Source);
    }

    [Fact]
    public void Matches_Tainted_Grail_by_app_id_and_pins_BE_788()
    {
        var pack = KnownGameCatalog.Find("1466060", "Something Else", null, null);
        Assert.Equal("Tainted Grail Fall of Avalon BepInEx", pack?.DisplayName);
        Assert.True(pack?.HasCustomSource);
        Assert.Equal(BepInExReleaseSource.TaintedGrail, pack?.Source);
        Assert.Equal(BepInExReleaseSource.RoguePrince, pack?.Source);
        Assert.Equal("BE 788", pack?.BadgeText);
        Assert.Contains("6.0.0-be.788", pack!.InstallHint, StringComparison.OrdinalIgnoreCase);
        Assert.True(pack.Source!.HasDirectDownload);
    }

    [Fact]
    public void Matches_Tainted_Grail_by_folder_name()
    {
        var pack = KnownGameCatalog.Find(
            null, null, @"D:\SteamLibrary\steamapps\common\Tainted Grail FoA", null);
        Assert.Equal(BepInExReleaseSource.TaintedGrail, pack?.Source);
    }

    [Fact]
    public void Matches_Tainted_Grail_by_name()
    {
        var pack = KnownGameCatalog.Find(null, "Tainted Grail: The Fall of Avalon", null, null);
        Assert.Equal(BepInExReleaseSource.TaintedGrail, pack?.Source);
    }

    [Fact]
    public void Tainted_Grail_name_does_not_match_Conquest()
    {
        Assert.Null(KnownGameCatalog.Find(null, "Tainted Grail: Conquest", null, null));
    }

    [Fact]
    public void Matches_Lost_Crown_by_app_id_and_enables_the_doorstop_ignore_switch()
    {
        var pack = KnownGameCatalog.Find("2751000", "Something Else", null, null);
        Assert.Equal("Prince of Persia The Lost Crown BepInEx", pack?.DisplayName);
        Assert.False(pack?.HasCustomSource);
        Assert.True(pack?.IgnoreDoorstopDisableSwitch);
        Assert.True(pack?.DisableUnityLogListening);
        Assert.Equal("Doorstop fix", pack?.BadgeText);
        Assert.Contains("ignore switch", pack!.InstallHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Matches_Lost_Crown_by_folder_name()
    {
        var pack = KnownGameCatalog.Find(
            null, null, @"D:\SteamLibrary\steamapps\common\Prince of Persia The Lost Crown", null);
        Assert.True(pack?.IgnoreDoorstopDisableSwitch);
        Assert.Null(pack?.Source);
    }

    [Fact]
    public void Lost_Crown_name_does_not_match_Rogue_Prince()
    {
        var pack = KnownGameCatalog.Find(null, "The Rogue Prince of Persia", null, null);
        Assert.Equal(BepInExReleaseSource.RoguePrince, pack?.Source);
        Assert.False(pack?.IgnoreDoorstopDisableSwitch);
    }

    [Fact]
    public void Matches_Skul_by_app_id_and_uses_unstripped_2020_3_34_libs()
    {
        var pack = KnownGameCatalog.Find("1147560", "Something Else", null, null);
        Assert.Equal("Skul BepInEx", pack?.DisplayName);
        Assert.False(pack?.HasCustomSource);
        Assert.True(pack?.NeedsUnstrippedLibraries);
        Assert.Equal("Unstripped libs", pack?.BadgeText);
        Assert.Equal("unstripped_corlib", pack!.UnstrippedLibraries!.Folder);
        Assert.Equal(
            "https://unity.bepinex.dev/corlibs/2020.3.34.zip",
            pack.UnstrippedLibraries.CorlibsUrl);
        Assert.Equal(
            "https://unity.bepinex.dev/libraries/2020.3.34.zip",
            pack.UnstrippedLibraries.EngineLibrariesUrl);
        Assert.Contains("stripped mscorlib", pack.InstallHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Matches_Skul_by_folder_name()
    {
        var pack = KnownGameCatalog.Find(null, null, @"D:\SteamLibrary\steamapps\common\Skul", null);
        Assert.True(pack?.NeedsUnstrippedLibraries);
        Assert.Null(pack?.Source);
    }

    [Fact]
    public void Matches_Skul_by_name()
    {
        var pack = KnownGameCatalog.Find(null, "Skul: The Hero Slayer", null, null);
        Assert.True(pack?.NeedsUnstrippedLibraries);
    }

    [Fact]
    public void Unstripped_libraries_reject_invalid_unity_versions()
    {
        Assert.Throws<ArgumentException>(() => UnstrippedLibraries.ForUnity("../2020.3.34"));
        Assert.Throws<ArgumentException>(() => UnstrippedLibraries.ForUnity("2020.3.34.zip"));
    }

    [Fact]
    public void Unknown_games_have_no_pack()
    {
        Assert.Null(KnownGameCatalog.Find("1", "Hollow Knight", @"D:\Steam\Hollow Knight", null));
    }
}
