using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class UnityGameDetectorTests
{
    [Fact]
    public void Finds_a_Unity_game_from_the_Steam_folder()
    {
        using var temp = new TempDir();
        CreateUnityLayout(temp.Path, "Valheim", il2Cpp: false);

        var found = UnityGameDetector.FindGameRoot(temp.Path);
        Assert.Equal(Path.GetFullPath(temp.Path), found);
    }

    [Fact]
    public void Accepts_the_exe_or_the_Data_folder()
    {
        using var temp = new TempDir();
        var (exe, data) = CreateUnityLayout(temp.Path, "Valheim", il2Cpp: false);

        Assert.Equal(Path.GetFullPath(temp.Path), UnityGameDetector.FindGameRoot(exe));
        Assert.Equal(Path.GetFullPath(temp.Path), UnityGameDetector.FindGameRoot(data));
    }

    [Fact]
    public void Finds_a_nested_Unity_game_and_skips_non_games()
    {
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Combine("BepInEx", "plugins"));
        var game = temp.Combine("Valheim");
        CreateUnityLayout(game, "Valheim", il2Cpp: true);

        var found = UnityGameDetector.FindGameRoot(temp.Path);
        Assert.Equal(Path.GetFullPath(game), found);
    }

    [Fact]
    public void Detects_Mono_from_Managed()
    {
        using var temp = new TempDir();
        CreateUnityLayout(temp.Path, "Valheim", il2Cpp: false);

        Assert.True(UnityGameDetector.TryInspect(temp.Path, out var info));
        Assert.Equal(ScriptingBackend.Mono, info.Backend);
        Assert.Equal(GameOs.Windows, info.Os);
        Assert.Equal("Valheim.exe", Path.GetFileName(info.ExePath));
    }

    [Fact]
    public void Detects_IL2CPP_from_GameAssembly()
    {
        using var temp = new TempDir();
        CreateUnityLayout(temp.Path, "LethalCompany", il2Cpp: true);

        Assert.True(UnityGameDetector.TryInspect(temp.Path, out var info));
        Assert.Equal(ScriptingBackend.Il2Cpp, info.Backend);
    }

    [Fact]
    public void Returns_null_when_the_folder_is_not_Unity()
    {
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Combine("Binaries", "Win64"));
        File.WriteAllBytes(temp.Combine("game.exe"), [1]);

        Assert.Null(UnityGameDetector.FindGameRoot(temp.Path));
    }

    [Fact]
    public void ReadElfArchitecture_detects_64_and_32_bit()
    {
        using var temp = new TempDir();
        var elf64 = temp.Combine("UnityPlayer.so");
        var elf32 = temp.Combine("player32.so");
        File.WriteAllBytes(elf64, [0x7F, (byte)'E', (byte)'L', (byte)'F', 2]);
        File.WriteAllBytes(elf32, [0x7F, (byte)'E', (byte)'L', (byte)'F', 1]);

        Assert.Equal(GameArch.X64, UnityGameDetector.ReadElfArchitecture(elf64));
        Assert.Equal(GameArch.X86, UnityGameDetector.ReadElfArchitecture(elf32));
    }

    private static (string Exe, string Data) CreateUnityLayout(string root, string name, bool il2Cpp)
    {
        Directory.CreateDirectory(root);
        var exe = Path.Combine(root, name + ".exe");
        var data = Path.Combine(root, name + "_Data");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(exe, [1]);

        if (il2Cpp)
        {
            File.WriteAllBytes(Path.Combine(root, "GameAssembly.dll"), [1]);
            Directory.CreateDirectory(Path.Combine(data, "il2cpp_data"));
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(data, "Managed"));
            File.WriteAllText(Path.Combine(data, "Managed", "Assembly-CSharp.dll"), "x");
        }

        return (exe, data);
    }
}
