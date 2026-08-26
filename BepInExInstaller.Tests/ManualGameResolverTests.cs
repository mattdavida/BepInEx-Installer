using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class ManualGameResolverTests
{
    [Fact]
    public void Steam_common_root_is_the_game_folder_under_common()
    {
        var root = ManualGameResolver.SteamCommonGameRoot(
            @"D:\SteamLibrary\steamapps\common\Valheim\Valheim_Data");
        Assert.Equal(
            Path.GetFullPath(@"D:\SteamLibrary\steamapps\common\Valheim"),
            root);
    }

    [Fact]
    public void Infer_uses_the_picked_folder_when_it_contains_the_game()
    {
        using var temp = new TempDir();
        var picked = temp.Combine("Valheim");
        var game = temp.Combine("Valheim");
        Directory.CreateDirectory(game);

        var inferred = ManualGameResolver.InferInstallPath(picked, game);
        Assert.Equal(Path.GetFullPath(picked), inferred);
    }
}
