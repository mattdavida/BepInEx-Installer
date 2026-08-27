namespace BepInExInstaller.Services;

/// <summary>
/// Official BepInEx Configuration Manager plugin (F1 in-game).
/// BepInEx 5 Mono uses the BepInEx5 zip. IL2CPP uses the IL2CPP zip.
/// There is no official BepInEx 6 Mono build, so that combo is hidden.
/// </summary>
public static class ConfigurationManagerSupport
{
    public const string DisplayName = "Configuration Manager";

    public const string MonoHint =
        "In-game F1 menu for plugin settings from GitHub (BepInEx.ConfigurationManager). Uncheck to remove it.";

    public const string Il2CppHint =
        "In-game F1 menu for plugin settings from GitHub (BepInEx.ConfigurationManager). Uncheck to remove it. On some IL2CPP games the menu never appears because Unity IMGUI is stripped.";

    public static bool CanOffer(ScriptingBackend backend, BepInExChannel channel)
    {
        if (backend == ScriptingBackend.Il2Cpp)
            return true;

        return channel != BepInExChannel.BleedingEdge;
    }

    public static string Hint(ScriptingBackend backend)
        => backend == ScriptingBackend.Il2Cpp ? Il2CppHint : MonoHint;

    public static InstalledMod? FindTracked(string gamePath)
        => ModTracker.List(gamePath).FirstOrDefault(LooksLikeConfigManager);

    public static bool IsInstalled(string gamePath)
        => FindTracked(gamePath) is not null || HasPluginDll(gamePath);

    internal static bool LooksLikeConfigManager(InstalledMod mod)
    {
        if (mod.Name.Contains("ConfigurationManager", StringComparison.OrdinalIgnoreCase)
            || mod.Name.Contains("Configuration Manager", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mod.Files.Any(file =>
            file.Contains("ConfigurationManager", StringComparison.OrdinalIgnoreCase)
            && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasPluginDll(string gamePath)
    {
        var plugins = Path.Combine(gamePath, "BepInEx", "plugins");
        if (!Directory.Exists(plugins))
            return false;

        try
        {
            return Directory.EnumerateFiles(plugins, "*ConfigurationManager*.dll", SearchOption.AllDirectories)
                .Any();
        }
        catch
        {
            return false;
        }
    }
}
