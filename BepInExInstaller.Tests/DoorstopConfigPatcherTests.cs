using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class DoorstopConfigPatcherTests
{
    [Fact]
    public void Sets_empty_override_and_keeps_comments()
    {
        using var temp = new TempDir();
        var path = DoorstopConfigPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(path, """
            [UnityMono]

            # Overrides default Mono DLL search path
            dll_search_path_override =
            """);

        DoorstopConfigPatcher.SetDllSearchPathOverride(temp.Path, "unstripped_corlib");

        var text = File.ReadAllText(path);
        Assert.Contains("dll_search_path_override = unstripped_corlib", text);
        Assert.Contains("# Overrides default Mono DLL search path", text);
        Assert.Equal("unstripped_corlib", DoorstopConfigPatcher.ReadDllSearchPathOverride(temp.Path));
    }

    [Fact]
    public void Updates_legacy_key_name()
    {
        using var temp = new TempDir();
        var path = DoorstopConfigPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(path, """
            [UnityMono]
            dllSearchPathOverride=old
            """);

        DoorstopConfigPatcher.SetDllSearchPathOverride(temp.Path, "unstripped_corlib");

        Assert.Contains("dllSearchPathOverride= unstripped_corlib", File.ReadAllText(path));
        Assert.Equal("unstripped_corlib", DoorstopConfigPatcher.ReadDllSearchPathOverride(temp.Path));
    }

    [Fact]
    public void Enables_ignore_disable_switch_and_keeps_the_target_assembly()
    {
        using var temp = new TempDir();
        var path = DoorstopConfigPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(path, """
            [General]

            # If enabled, DOORSTOP_DISABLE env var value is ignored
            ignore_disable_switch = false

            target_assembly = BepInEx\core\BepInEx.Unity.IL2CPP.dll

            [Il2Cpp]
            coreclr_path = dotnet\coreclr.dll
            """);

        Assert.False(DoorstopConfigPatcher.ReadIgnoreDisableSwitch(temp.Path));
        DoorstopConfigPatcher.SetIgnoreDisableSwitch(temp.Path, true);

        var text = File.ReadAllText(path);
        Assert.Contains("ignore_disable_switch = true", text);
        Assert.Contains("target_assembly = BepInEx\\core\\BepInEx.Unity.IL2CPP.dll", text);
        Assert.Contains("coreclr_path = dotnet\\coreclr.dll", text);
        Assert.True(DoorstopConfigPatcher.ReadIgnoreDisableSwitch(temp.Path));
    }

    [Fact]
    public void Updates_legacy_ignore_disable_switch_key()
    {
        using var temp = new TempDir();
        var path = DoorstopConfigPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(path, """
            [General]
            ignoreDisableSwitch = false
            """);

        DoorstopConfigPatcher.SetIgnoreDisableSwitch(temp.Path, true);

        Assert.Contains("ignoreDisableSwitch = true", File.ReadAllText(path));
        Assert.True(DoorstopConfigPatcher.ReadIgnoreDisableSwitch(temp.Path));
    }

    [Fact]
    public void Rejects_path_traversal()
    {
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(DoorstopConfigPatcher.ConfigPath(temp.Path), "[UnityMono]\n");

        Assert.Throws<ArgumentException>(() =>
            DoorstopConfigPatcher.SetDllSearchPathOverride(temp.Path, "../other"));
    }
}
