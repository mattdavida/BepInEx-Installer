using System.IO.Compression;

namespace BepInExInstaller.Services;

public static class ZipInstaller
{
    private static readonly string[] DoorstopFiles =
    [
        "winhttp.dll",
        "version.dll",
        "winmm.dll",
        "doorstop_config.ini",
        ".doorstop_version",
        "changelog.txt",
        "libdoorstop.so",
        "libdoorstop.dylib",
        "run_bepinex.sh"
    ];

    /// <summary>
    /// Extracts the BepInEx zip into the Unity game root, then removes files from the previous
    /// installer-owned manifest that are not in this zip. A single wrapper folder is stripped
    /// so <c>winhttp.dll</c> / <c>doorstop_config.ini</c> and <c>BepInEx/</c> land next to the exe.
    /// Same-channel updates keep <c>doorstop_config.ini</c> and <c>BepInEx/config/</c>.
    /// </summary>
    public static void InstallBepInEx(string zipPath, string gamePath, BepInExChannel channel)
    {
        Directory.CreateDirectory(gamePath);

        var previous = InstallTracker.TryLoad(gamePath);
        var preserveConfig = previous is not null && previous.Channel == channel;

        var extracted = ExtractZip(zipPath, gamePath, preserveConfig);
        var extractedSet = new HashSet<string>(extracted, StringComparer.OrdinalIgnoreCase);

        if (previous is not null)
            RemoveOrphans(gamePath, previous.Files, extractedSet);

        foreach (var leftover in previous?.Files ?? [])
        {
            if (extractedSet.Contains(leftover))
                continue;

            var full = SafeCombine(gamePath, leftover);
            if (full is not null && File.Exists(full))
                extractedSet.Add(NormalizeRelative(leftover));
        }

        InstallTracker.Save(gamePath, new InstallerManifest
        {
            Channel = channel,
            Files = extractedSet.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
        });
    }

    /// <summary>
    /// Removes BepInEx: deletes <c>BepInEx/</c> (plugins and config included) and Doorstop
    /// files such as <c>winhttp.dll</c>. Extra root files from a managed manifest are
    /// deleted too.
    /// </summary>
    public static void UninstallBepInEx(string gamePath)
    {
        var manifest = InstallTracker.TryLoad(gamePath);
        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gameFull = Path.GetFullPath(gamePath);

        if (manifest is not null)
        {
            foreach (var relative in manifest.Files)
            {
                var norm = NormalizeRelative(relative);
                if (norm.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(norm, "BepInEx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dest = SafeCombine(gamePath, norm);
                if (dest is null)
                    continue;

                TryDeleteFile(dest);
                var parent = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(parent))
                    parents.Add(parent);
            }
        }

        foreach (var name in DoorstopFiles)
            TryDeleteFile(Path.Combine(gamePath, name));

        TryDeleteFile(Path.Combine(gamePath, InstallerManifest.FileName));
        TryDeleteFile(Path.Combine(gamePath, ModsManifest.FileName));

        var bepinexDir = Path.Combine(gamePath, "BepInEx");
        if (Directory.Exists(bepinexDir))
            Directory.Delete(bepinexDir, recursive: true);

        foreach (var dir in parents.OrderByDescending(p => p.Length))
            TryDeleteEmptyAncestors(dir, gameFull);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            throw new IOException($"Could not delete {path}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Installs a user zip. Full BepInEx packs and <c>BepInEx/</c> overlays extract into the
    /// game root. Everything else extracts into <c>BepInEx/plugins</c>.
    /// Recorded in <c>.bepinex-installer-plugins.json</c> for per-plugin uninstall.
    /// </summary>
    public static ModInstallResult InstallMod(string zipPath, string gamePath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var layout = InspectModZip(archive);

        var destination = layout.Kind == ModPackageKind.GameDirectory
            ? gamePath
            : ResolvePluginsDirectory(gamePath);

        Directory.CreateDirectory(destination);
        var extracted = ExtractMapped(archive, destination, layout.StripPrefix);
        var relativeToGame = extracted
            .Select(relative => RelativizeToGame(gamePath, destination, relative))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var name = Path.GetFileNameWithoutExtension(zipPath);
        if (string.IsNullOrWhiteSpace(name))
            name = "Plugin";

        var previous = ModTracker.Load(gamePath).Mods
            .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        if (previous is not null)
        {
            var keep = new HashSet<string>(relativeToGame, StringComparer.OrdinalIgnoreCase);
            keep.UnionWith(BepInExOwnedFiles(gamePath));
            RemoveOrphans(gamePath, previous.Files, keep);
        }

        ModTracker.SaveMod(gamePath, new InstalledMod
        {
            Name = name,
            Kind = layout.Kind,
            Files = relativeToGame
        });

        return new ModInstallResult(layout.Kind, destination, name, relativeToGame);
    }

    public static void UninstallMod(string gamePath, string modId)
    {
        var mod = ModTracker.List(gamePath).FirstOrDefault(m => m.Id == modId)
                  ?? throw new InvalidOperationException("That plugin is not in the installer list.");

        var keep = BepInExOwnedFiles(gamePath);
        var configs = PluginConfigCleanup.FindAssociatedConfigs(gamePath, mod.Files);
        var toRemove = mod.Files.Concat(configs).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        RemoveOrphans(gamePath, toRemove, keep);

        foreach (var relative in toRemove)
        {
            if (keep.Contains(NormalizeRelative(relative)))
                continue;

            var full = SafeCombine(gamePath, relative);
            if (full is not null && File.Exists(full))
            {
                throw new IOException(
                    $"Could not delete {full}. Close the game and try again.");
            }
        }

        ModTracker.Remove(gamePath, modId);
    }

    private static HashSet<string> BepInExOwnedFiles(string gamePath)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifest = InstallTracker.TryLoad(gamePath);
        if (manifest is not null)
        {
            foreach (var file in manifest.Files)
                owned.Add(NormalizeRelative(file));
        }

        owned.Add(NormalizeRelative(Path.Combine("BepInEx", InstallerManifest.FileName)));
        owned.Add(NormalizeRelative(Path.Combine("BepInEx", ModsManifest.FileName)));
        owned.Add(NormalizeRelative(Path.Combine("BepInEx", "config", "BepInEx.cfg")));
        owned.Add(InstallerManifest.FileName);
        owned.Add(ModsManifest.FileName);
        return owned;
    }

    private static string RelativizeToGame(string gamePath, string destination, string relativeToDest)
    {
        var destFile = SafeCombine(destination, relativeToDest);
        if (destFile is null)
            return NormalizeRelative(relativeToDest);

        var gameFull = Path.GetFullPath(gamePath);
        var prefix = gameFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destFile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return NormalizeRelative(relativeToDest);

        return NormalizeRelative(destFile[prefix.Length..]);
    }

    public static string ResolvePluginsDirectory(string gamePath)
    {
        var bepinexDir = Path.Combine(gamePath, "BepInEx");
        if (Directory.Exists(bepinexDir))
            return Path.Combine(bepinexDir, "plugins");

        return Path.Combine(gamePath, "BepInEx", "plugins");
    }

    private static List<string> ExtractZip(string zipPath, string gamePath, bool preserveConfig)
    {
        var extracted = new List<string>();
        using var archive = ZipFile.OpenRead(zipPath);
        var layout = InspectModZip(archive);
        var stripPrefix = layout.Kind == ModPackageKind.GameDirectory ? layout.StripPrefix : null;

        foreach (var entry in archive.Entries)
        {
            var relative = NormalizeRelative(entry.FullName);
            if (relative.Length == 0)
                continue;

            relative = StripPrefix(relative, stripPrefix);
            if (relative.Length == 0 || IsJunkPath(relative))
                continue;

            var dest = SafeCombine(gamePath, relative);
            if (dest is null)
                continue;

            var isDirectory = string.IsNullOrEmpty(entry.Name)
                              || entry.FullName.EndsWith('/')
                              || entry.FullName.EndsWith('\\');
            if (isDirectory)
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            if (preserveConfig && IsPreservedConfig(relative) && File.Exists(dest))
            {
                extracted.Add(relative);
                continue;
            }

            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            entry.ExtractToFile(dest, overwrite: true);
            extracted.Add(relative);
        }

        return extracted;
    }

    private static void RemoveOrphans(string gamePath, IEnumerable<string> previousFiles, HashSet<string> keep)
    {
        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relative in previousFiles)
        {
            if (keep.Contains(relative) || IsManifestFile(relative))
                continue;

            var full = SafeCombine(gamePath, relative);
            if (full is null)
                continue;

            try
            {
                if (File.Exists(full))
                    File.Delete(full);
            }
            catch
            {
                continue;
            }

            var parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent))
                parents.Add(parent);
        }

        var gameFull = Path.GetFullPath(gamePath);
        foreach (var dir in parents.OrderByDescending(p => p.Length))
            TryDeleteEmptyAncestors(dir, gameFull);
    }

    private static void TryDeleteEmptyAncestors(string directory, string gameFull)
    {
        var current = directory;
        while (!string.IsNullOrEmpty(current)
               && current.StartsWith(gameFull, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(current, gameFull, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
                    Directory.Delete(current);
                else
                    break;
            }
            catch
            {
                break;
            }

            current = Path.GetDirectoryName(current) ?? "";
        }
    }

    private static bool IsPreservedConfig(string relative)
        => relative.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)
           || relative.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);

    private static bool IsManifestFile(string relative)
    {
        var fileName = Path.GetFileName(relative);
        return string.Equals(fileName, InstallerManifest.FileName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fileName, ModsManifest.FileName, StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeRelative(string relative)
        => relative.Replace('\\', '/').Trim('/');

    internal static string? SafeCombine(string root, string relative)
    {
        var rootFull = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, rootFull, StringComparison.OrdinalIgnoreCase))
            return null;

        return combined;
    }

    internal static ModPackageKind PeekModZipKind(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return InspectModZip(archive).Kind;
    }

    internal static ModZipLayout InspectModZip(ZipArchive archive)
    {
        var relatives = archive.Entries
            .Select(entry => NormalizeRelative(entry.FullName))
            .Where(relative => relative.Length > 0 && !IsJunkPath(relative))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var strip = DetectWrapperPrefix(relatives);
        var effective = StripPrefix(relatives, strip);

        if (LooksLikeGameDirectoryPackage(effective))
            return new ModZipLayout(ModPackageKind.GameDirectory, strip);

        var pluginsPrefix = CombinePrefix(strip, "plugins");
        if (HasRootFolder(effective, "plugins"))
            return new ModZipLayout(ModPackageKind.PluginsFolder, pluginsPrefix);

        if (HasRootFolder(effective, "BepInEx/plugins"))
            return new ModZipLayout(ModPackageKind.GameDirectory, strip);

        return new ModZipLayout(ModPackageKind.PluginsFolder, strip);
    }

    private static List<string> ExtractMapped(ZipArchive archive, string destination, string? stripPrefix)
    {
        var extracted = new List<string>();
        foreach (var entry in archive.Entries)
        {
            var relative = NormalizeRelative(entry.FullName);
            if (relative.Length == 0 || IsJunkPath(relative))
                continue;

            relative = StripPrefix(relative, stripPrefix);
            if (relative.Length == 0)
                continue;

            if (IsManifestFile(relative))
                continue;

            var dest = SafeCombine(destination, relative);
            if (dest is null)
                continue;

            var isDirectory = string.IsNullOrEmpty(entry.Name)
                              || entry.FullName.EndsWith('/')
                              || entry.FullName.EndsWith('\\');
            if (isDirectory)
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            entry.ExtractToFile(dest, overwrite: true);
            extracted.Add(NormalizeRelative(relative));
        }

        return extracted;
    }

    private static bool LooksLikeGameDirectoryPackage(IReadOnlyList<string> relatives)
    {
        var hasDoorstop = relatives.Any(path =>
            path.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)
            || path.Equals("version.dll", StringComparison.OrdinalIgnoreCase)
            || path.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)
            || path.Equals("libdoorstop.so", StringComparison.OrdinalIgnoreCase));
        var hasBepInEx = HasRootFolder(relatives, "BepInEx");

        return hasBepInEx && (hasDoorstop || HasRootFolder(relatives, "BepInEx"));
    }

    private static string? DetectWrapperPrefix(IReadOnlyList<string> relatives)
    {
        var tops = relatives
            .Select(FirstSegment)
            .Where(segment => segment.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tops.Count != 1)
            return null;

        var prefix = tops[0];
        if (prefix.Equals("BepInEx", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("plugins", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains('.'))
            return null;

        if (!relatives.Any(path => path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
            return null;

        var stripped = StripPrefix(relatives, prefix);
        if (LooksLikeGameDirectoryPackage(stripped)
            || HasRootFolder(stripped, "plugins")
            || HasRootFolder(stripped, "BepInEx"))
            return prefix;

        return null;
    }

    private static bool HasRootFolder(IEnumerable<string> relatives, string folder)
        => relatives.Any(path =>
            path.Equals(folder, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));

    private static string FirstSegment(string relative)
    {
        var slash = relative.IndexOf('/');
        return slash < 0 ? relative : relative[..slash];
    }

    private static List<string> StripPrefix(IEnumerable<string> relatives, string? prefix)
        => relatives.Select(path => StripPrefix(path, prefix))
            .Where(path => path.Length > 0)
            .ToList();

    private static string StripPrefix(string relative, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return relative;

        if (relative.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var head = prefix.Trim('/') + "/";
        return relative.StartsWith(head, StringComparison.OrdinalIgnoreCase)
            ? relative[head.Length..]
            : relative;
    }

    private static string? CombinePrefix(string? first, string second)
        => string.IsNullOrEmpty(first) ? second : $"{first.Trim('/')}/{second}";

    private static bool IsJunkPath(string relative)
    {
        var first = FirstSegment(relative);
        return first.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)
               || first.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
               || relative.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase)
               || relative.EndsWith("Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }
}

public enum ModPackageKind
{
    GameDirectory,
    PluginsFolder
}

public sealed record ModInstallResult(
    ModPackageKind Kind,
    string Destination,
    string Name,
    IReadOnlyList<string> Files);

internal sealed record ModZipLayout(ModPackageKind Kind, string? StripPrefix);
