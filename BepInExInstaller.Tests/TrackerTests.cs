using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class ModTrackerTests
{
    [Fact]
    public void Replacing_the_same_name_keeps_a_single_record()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx"));

        ModTracker.SaveMod(game, new InstalledMod
        {
            Name = "CoolPlugin",
            Kind = ModPackageKind.PluginsFolder,
            Files = ["BepInEx/plugins/CoolPlugin.dll"]
        });
        ModTracker.SaveMod(game, new InstalledMod
        {
            Name = "CoolPlugin",
            Kind = ModPackageKind.PluginsFolder,
            Files = ["BepInEx/plugins/CoolPlugin.v2.dll"]
        });

        var listed = ModTracker.List(game);
        Assert.Single(listed);
        Assert.Equal("BepInEx/plugins/CoolPlugin.v2.dll", Assert.Single(listed[0].Files));
    }
}

public sealed class InstallTrackerTests
{
    [Fact]
    public void Detects_managed_unmanaged_and_missing_installs()
    {
        using var temp = new TempDir();
        var none = temp.Combine("none");
        Directory.CreateDirectory(none);
        Assert.Equal(InstallKind.None, InstallTracker.Detect(none).Kind);

        var unmanaged = temp.Combine("unmanaged");
        Directory.CreateDirectory(unmanaged);
        File.WriteAllText(Path.Combine(unmanaged, "winhttp.dll"), "x");
        Assert.Equal(InstallKind.Unmanaged, InstallTracker.Detect(unmanaged).Kind);

        var managed = temp.Combine("managed");
        Directory.CreateDirectory(Path.Combine(managed, "BepInEx"));
        File.WriteAllText(Path.Combine(managed, "winhttp.dll"), "x");
        InstallTracker.Save(managed, new InstallerManifest
        {
            Channel = BepInExChannel.BleedingEdge,
            Files = ["winhttp.dll"]
        });
        var state = InstallTracker.Detect(managed);
        Assert.Equal(InstallKind.Managed, state.Kind);
        Assert.Equal(BepInExChannel.BleedingEdge, state.Channel);
    }

    [Fact]
    public void Leftover_BepInEx_folder_without_dlls_still_counts_as_installed()
    {
        using var temp = new TempDir();
        var leftover = temp.Combine("leftover");
        Directory.CreateDirectory(Path.Combine(leftover, "BepInEx", "plugins"));
        Assert.Equal(InstallKind.Unmanaged, InstallTracker.Detect(leftover).Kind);
    }
}
