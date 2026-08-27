using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class ConfigurationManagerSupportTests
{
    [Fact]
    public void Offers_BepInEx5_zip_for_Stable_Mono()
    {
        Assert.True(ConfigurationManagerSupport.CanOffer(ScriptingBackend.Mono, BepInExChannel.Stable));
    }

    [Fact]
    public void Offers_IL2CPP_zip_on_Bleeding_Edge()
    {
        Assert.True(ConfigurationManagerSupport.CanOffer(ScriptingBackend.Il2Cpp, BepInExChannel.BleedingEdge));
    }

    [Fact]
    public void Hides_Mono_Bleeding_Edge_because_there_is_no_official_zip()
    {
        Assert.False(ConfigurationManagerSupport.CanOffer(ScriptingBackend.Mono, BepInExChannel.BleedingEdge));
    }

    [Fact]
    public void Il2Cpp_hint_mentions_stripped_IMGUI()
    {
        Assert.Contains("IMGUI", ConfigurationManagerSupport.Hint(ScriptingBackend.Il2Cpp), StringComparison.Ordinal);
        Assert.DoesNotContain("IMGUI", ConfigurationManagerSupport.Hint(ScriptingBackend.Mono), StringComparison.Ordinal);
    }

    [Fact]
    public void Detects_tracked_install_by_display_name()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins"));
        ModTracker.SaveMod(game, new InstalledMod
        {
            Name = ConfigurationManagerSupport.DisplayName,
            Kind = ModPackageKind.PluginsFolder,
            Files = ["BepInEx/plugins/ConfigurationManager/ConfigurationManager.dll"]
        });

        Assert.True(ConfigurationManagerSupport.IsInstalled(game));
        Assert.Equal(ConfigurationManagerSupport.DisplayName, ConfigurationManagerSupport.FindTracked(game)?.Name);
    }
}
