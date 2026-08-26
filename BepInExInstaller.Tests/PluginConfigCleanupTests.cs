using System.Reflection;
using System.Reflection.Emit;
using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class PluginConfigCleanupTests
{
    [Fact]
    public void Uninstall_deletes_runtime_cfg_named_after_the_plugin_dll()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "config"));

        var zip = TestZip.CreateNamed(temp.Path, "Volkolak.zip", ("Volkolak.dll", "plugin"));
        ZipInstaller.InstallMod(zip, game);

        var pluginCfg = Path.Combine(game, "BepInEx", "config", "Volkolak.cfg");
        var bepinexCfg = Path.Combine(game, "BepInEx", "config", "BepInEx.cfg");
        var otherCfg = Path.Combine(game, "BepInEx", "config", "com.someone.else.cfg");
        File.WriteAllText(pluginCfg, "from first launch");
        File.WriteAllText(bepinexCfg, "Enabled = true");
        File.WriteAllText(otherCfg, "keep me");

        var id = Assert.Single(ModTracker.List(game)).Id;
        ZipInstaller.UninstallMod(game, id);

        Assert.False(File.Exists(Path.Combine(game, "BepInEx", "plugins", "Volkolak.dll")));
        Assert.False(File.Exists(pluginCfg));
        Assert.True(File.Exists(bepinexCfg));
        Assert.True(File.Exists(otherCfg));
        Assert.Equal("Enabled = true", File.ReadAllText(bepinexCfg));
    }

    [Fact]
    public void Uninstall_deletes_cfg_named_after_the_BepInPlugin_guid()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        var plugins = Path.Combine(game, "BepInEx", "plugins");
        var config = Path.Combine(game, "BepInEx", "config");
        Directory.CreateDirectory(plugins);
        Directory.CreateDirectory(config);

        var dll = Path.Combine(plugins, "Volkolak.dll");
        EmitPluginDll(dll, "com.mattd.volkolak");
        ModTracker.SaveMod(game, new InstalledMod
        {
            Name = "Volkolak",
            Kind = ModPackageKind.PluginsFolder,
            Files = ["BepInEx/plugins/Volkolak.dll"]
        });

        var guidCfg = Path.Combine(config, "com.mattd.volkolak.cfg");
        File.WriteAllText(guidCfg, "[General]\nEnabled = true");

        var id = Assert.Single(ModTracker.List(game)).Id;
        ZipInstaller.UninstallMod(game, id);

        Assert.False(File.Exists(dll));
        Assert.False(File.Exists(guidCfg));
    }

    [Fact]
    public void Uninstall_deletes_cfg_named_after_the_plugin_folder()
    {
        using var temp = new TempDir();
        var game = temp.Combine("Game");
        var pluginDir = Path.Combine(game, "BepInEx", "plugins", "VolkolakMod");
        var config = Path.Combine(game, "BepInEx", "config");
        Directory.CreateDirectory(pluginDir);
        Directory.CreateDirectory(config);
        File.WriteAllBytes(Path.Combine(pluginDir, "plugin.dll"), [1]);
        File.WriteAllText(Path.Combine(config, "VolkolakMod.cfg"), "runtime");

        ModTracker.SaveMod(game, new InstalledMod
        {
            Name = "VolkolakMod",
            Kind = ModPackageKind.PluginsFolder,
            Files = ["BepInEx/plugins/VolkolakMod/plugin.dll"]
        });

        ZipInstaller.UninstallMod(game, Assert.Single(ModTracker.List(game)).Id);

        Assert.False(File.Exists(Path.Combine(config, "VolkolakMod.cfg")));
        Assert.False(File.Exists(Path.Combine(pluginDir, "plugin.dll")));
    }

    [Fact]
    public void ReadBepInPluginGuids_returns_the_constructor_guid()
    {
        using var temp = new TempDir();
        var dll = temp.Combine("Volkolak.dll");
        EmitPluginDll(dll, "com.mattd.volkolak");

        var guids = PluginConfigCleanup.ReadBepInPluginGuids(dll);
        Assert.Equal("com.mattd.volkolak", Assert.Single(guids));
    }

    [Fact]
    public void ReadBepInPluginGuids_ignores_a_non_managed_file()
    {
        using var temp = new TempDir();
        var dll = temp.Combine("not-a-plugin.dll");
        File.WriteAllText(dll, "not pe");
        Assert.Empty(PluginConfigCleanup.ReadBepInPluginGuids(dll));
    }

    private static void EmitPluginDll(string path, string guid)
    {
        var name = new AssemblyName(Path.GetFileNameWithoutExtension(path));
        var assembly = new PersistedAssemblyBuilder(name, typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("Main");

        var attrType = module.DefineType(
            "BepInPlugin",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(Attribute));
        var ctor = attrType.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [typeof(string), typeof(string), typeof(string)]);
        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ret);
        var attr = attrType.CreateType();

        var plugin = module.DefineType("TestPlugin", TypeAttributes.Public | TypeAttributes.Class);
        plugin.SetCustomAttribute(new CustomAttributeBuilder(
            attr.GetConstructor([typeof(string), typeof(string), typeof(string)])!,
            [guid, "Volkolak", "1.0.0"]));
        plugin.CreateType();

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        assembly.Save(path);
    }
}
