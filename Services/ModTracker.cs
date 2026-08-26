using System.Text.Json;
using System.Text.Json.Serialization;

namespace BepInExInstaller.Services;

public sealed class InstalledMod
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public ModPackageKind Kind { get; set; }

    /// <summary>Paths relative to the game root.</summary>
    public List<string> Files { get; set; } = [];
}

public sealed class ModsManifest
{
    public const string FileName = ".bepinex-installer-plugins.json";

    public List<InstalledMod> Mods { get; set; } = [];
}

public static class ModTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<InstalledMod> List(string gamePath)
        => Load(gamePath).Mods;

    public static void SaveMod(string gamePath, InstalledMod mod)
    {
        var manifest = Load(gamePath);
        var existing = manifest.Mods.FindIndex(m =>
            string.Equals(m.Name, mod.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            mod.Id = manifest.Mods[existing].Id;
            manifest.Mods[existing] = mod;
        }
        else
        {
            manifest.Mods.Add(mod);
        }

        manifest.Mods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Save(gamePath, manifest);
    }

    public static InstalledMod? Remove(string gamePath, string modId)
    {
        var manifest = Load(gamePath);
        var index = manifest.Mods.FindIndex(m => m.Id == modId);
        if (index < 0)
            return null;

        var removed = manifest.Mods[index];
        manifest.Mods.RemoveAt(index);
        Save(gamePath, manifest);
        return removed;
    }

    public static ModsManifest Load(string gamePath)
    {
        var merged = new ModsManifest();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ManifestPaths(gamePath))
        {
            if (!File.Exists(path))
                continue;

            ModsManifest? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<ModsManifest>(File.ReadAllText(path), JsonOptions);
            }
            catch
            {
                // Treat a corrupt list as empty; the next save overwrites it.
            }

            if (parsed is null)
                continue;

            foreach (var mod in parsed.Mods)
            {
                if (string.IsNullOrWhiteSpace(mod.Name) || !seen.Add(mod.Name))
                    continue;

                merged.Mods.Add(mod);
            }
        }

        merged.Mods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return merged;
    }

    private static void Save(string gamePath, ModsManifest manifest)
    {
        var path = PreferredPath(gamePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));

        foreach (var extra in ManifestPaths(gamePath))
        {
            if (string.Equals(extra, path, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (File.Exists(extra))
                    File.Delete(extra);
            }
            catch
            {
                // Best-effort consolidation.
            }
        }
    }

    public static string PreferredPath(string gamePath)
    {
        var bepinex = Path.Combine(gamePath, "BepInEx");
        if (Directory.Exists(bepinex))
            return Path.Combine(bepinex, ModsManifest.FileName);

        return Path.Combine(gamePath, ModsManifest.FileName);
    }

    private static IEnumerable<string> ManifestPaths(string gamePath)
    {
        yield return Path.Combine(gamePath, "BepInEx", ModsManifest.FileName);
        yield return Path.Combine(gamePath, ModsManifest.FileName);
    }
}
