using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class BepInExInstallTests
{
    [Fact]
    public void Channel_switch_deletes_files_not_in_the_new_zip()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(game);

        var bleeding = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("BepInEx/core/BepInEx.dll", "core"),
            ("BepInEx/be-only.txt", "be"));
        ZipInstaller.InstallBepInEx(bleeding, game, BepInExChannel.BleedingEdge);
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "be-only.txt")));

        var stable = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("BepInEx/core/BepInEx.dll", "core"));
        ZipInstaller.InstallBepInEx(stable, game, BepInExChannel.Stable);

        Assert.False(File.Exists(Path.Combine(game, "BepInEx", "be-only.txt")));
        Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "core", "BepInEx.dll")));
    }

    [Fact]
    public void Same_channel_keeps_existing_config()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(game);

        var first = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("doorstop_config.ini", "target_assembly=old"),
            ("BepInEx/config/BepInEx.cfg", "LogLevels = old"));
        ZipInstaller.InstallBepInEx(first, game, BepInExChannel.Stable);

        var doorstop = Path.Combine(game, "doorstop_config.ini");
        var cfg = Path.Combine(game, "BepInEx", "config", "BepInEx.cfg");
        File.WriteAllText(doorstop, "target_assembly=user");
        File.WriteAllText(cfg, "LogLevels = user");

        var second = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("doorstop_config.ini", "target_assembly=new"),
            ("BepInEx/config/BepInEx.cfg", "LogLevels = new"));
        ZipInstaller.InstallBepInEx(second, game, BepInExChannel.Stable);

        Assert.Equal("target_assembly=user", File.ReadAllText(doorstop));
        Assert.Equal("LogLevels = user", File.ReadAllText(cfg));
    }

    [Fact]
    public void Uninstall_removes_BepInEx_and_doorstop_but_not_game_files()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        var save = temp.Combine("Game", "Valheim_Data", "saves", "world.db");
        Directory.CreateDirectory(game);
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        File.WriteAllText(save, "keep me");

        var zip = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("doorstop_config.ini", "cfg"),
            ("BepInEx/core/BepInEx.dll", "core"),
            ("BepInEx/plugins/HandCopied.dll", "mod"));
        ZipInstaller.InstallBepInEx(zip, game, BepInExChannel.Stable);
        ZipInstaller.UninstallBepInEx(game);

        Assert.False(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.False(File.Exists(Path.Combine(game, "doorstop_config.ini")));
        Assert.False(Directory.Exists(Path.Combine(game, "BepInEx")));
        Assert.True(File.Exists(save));
        Assert.Empty(ModTracker.List(game));
    }

    [Fact]
    public void Plugin_zip_extracts_into_BepInEx_plugins()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));

        var zip = TestZip.CreateNamed(temp.Path, "CoolPlugin.zip", ("CoolPlugin.dll", "plugin"));
        var result = ZipInstaller.InstallMod(zip, game);

        Assert.Equal(ModPackageKind.PluginsFolder, result.Kind);
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "plugins", "CoolPlugin.dll")));
        Assert.Equal("CoolPlugin", Assert.Single(ModTracker.List(game)).Name);
    }

    [Fact]
    public void Full_pack_zip_extracts_into_the_game_folder()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(game);

        var zip = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("BepInEx/plugins/Pack.dll", "plugin"));
        var result = ZipInstaller.InstallMod(zip, game);

        Assert.Equal(ModPackageKind.GameDirectory, result.Kind);
        Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "plugins", "Pack.dll")));
    }

    [Fact]
    public void Wrapper_folder_is_stripped_from_a_BepInEx_pack()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(game);

        var zip = TestZip.Create(temp.Path,
            ("BepInEx_pack/winhttp.dll", "proxy"),
            ("BepInEx_pack/BepInEx/core/BepInEx.dll", "core"));
        ZipInstaller.InstallBepInEx(zip, game, BepInExChannel.Stable);

        Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "core", "BepInEx.dll")));
    }

    [Fact]
    public void Uninstall_plugin_leaves_BepInEx_core_alone()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(game);

        var core = TestZip.Create(temp.Path,
            ("winhttp.dll", "proxy"),
            ("BepInEx/core/BepInEx.dll", "core"));
        ZipInstaller.InstallBepInEx(core, game, BepInExChannel.Stable);

        var plugin = TestZip.CreateNamed(temp.Path, "CoolPlugin.zip", ("CoolPlugin.dll", "plugin"));
        var installed = ZipInstaller.InstallMod(plugin, game);
        var id = Assert.Single(ModTracker.List(game)).Id;

        ZipInstaller.UninstallMod(game, id);

        Assert.False(File.Exists(Path.Combine(game, "BepInEx", "plugins", "CoolPlugin.dll")));
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "core", "BepInEx.dll")));
        Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.Equal("CoolPlugin", installed.Name);
    }

    [Fact]
    public void V_Rising_pack_wrapper_and_dotnet_runtime_extract_then_uninstall()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(game);

        var zip = TestZip.Create(temp.Path,
            ("BepInExPack_V_Rising/winhttp.dll", "proxy"),
            ("BepInExPack_V_Rising/doorstop_config.ini", "cfg"),
            ("BepInExPack_V_Rising/BepInEx/core/BepInEx.dll", "core"),
            ("BepInExPack_V_Rising/dotnet/coreclr.dll", "runtime"));
        ZipInstaller.InstallBepInEx(zip, game, BepInExChannel.BleedingEdge);

        Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.True(File.Exists(Path.Combine(game, "BepInEx", "core", "BepInEx.dll")));
        Assert.True(File.Exists(Path.Combine(game, "dotnet", "coreclr.dll")));

        ZipInstaller.UninstallBepInEx(game);

        Assert.False(File.Exists(Path.Combine(game, "winhttp.dll")));
        Assert.False(Directory.Exists(Path.Combine(game, "BepInEx")));
        Assert.False(File.Exists(Path.Combine(game, "dotnet", "coreclr.dll")));
    }
}
