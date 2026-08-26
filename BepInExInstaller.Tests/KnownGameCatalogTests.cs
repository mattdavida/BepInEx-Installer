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
    public void Unknown_games_have_no_pack()
    {
        Assert.Null(KnownGameCatalog.Find("1", "Hollow Knight", @"D:\Steam\Hollow Knight", null));
    }
}
