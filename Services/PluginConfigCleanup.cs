using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace BepInExInstaller.Services;

/// <summary>
/// Finds <c>BepInEx/config/{guid}.cfg</c> files created on first launch, which are not
/// in the plugin zip and therefore not in the installer manifest.
/// </summary>
public static class PluginConfigCleanup
{
    internal static IReadOnlyList<string> FindAssociatedConfigs(string gamePath, IEnumerable<string> modFiles)
    {
        var configDir = Path.Combine(gamePath, "BepInEx", "config");
        if (!Directory.Exists(configDir))
            return [];

        var names = CollectConfigNames(gamePath, modFiles);
        if (names.Count == 0)
            return [];

        var matches = new List<string>();
        IEnumerable<string> cfgs;
        try
        {
            cfgs = Directory.EnumerateFiles(configDir, "*.cfg");
        }
        catch
        {
            return [];
        }

        var gameFull = Path.GetFullPath(gamePath);
        var prefix = gameFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var cfg in cfgs)
        {
            var stem = Path.GetFileNameWithoutExtension(cfg);
            if (stem.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!names.Contains(stem))
                continue;

            var full = Path.GetFullPath(cfg);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            matches.Add(ZipInstaller.NormalizeRelative(full[prefix.Length..]));
        }

        return matches;
    }

    internal static HashSet<string> CollectConfigNames(string gamePath, IEnumerable<string> modFiles)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relative in modFiles)
        {
            var full = ZipInstaller.SafeCombine(gamePath, relative);
            if (full is null || !File.Exists(full))
                continue;

            if (!full.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            names.Add(Path.GetFileNameWithoutExtension(full));

            var parent = Path.GetFileName(Path.GetDirectoryName(full));
            if (!string.IsNullOrEmpty(parent)
                && !parent.Equals("plugins", StringComparison.OrdinalIgnoreCase)
                && !parent.Equals("patchers", StringComparison.OrdinalIgnoreCase)
                && !parent.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(parent);
            }

            foreach (var guid in ReadBepInPluginGuids(full))
                names.Add(guid);
        }

        names.Remove("BepInEx");
        return names;
    }

    internal static IReadOnlyList<string> ReadBepInPluginGuids(string dllPath)
    {
        var guids = new List<string>();
        try
        {
            using var stream = File.OpenRead(dllPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return guids;

            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.CustomAttributes)
            {
                var attr = reader.GetCustomAttribute(handle);
                if (!TryGetAttributeTypeName(reader, attr, out var typeName))
                    continue;

                if (!typeName.Equals("BepInPlugin", StringComparison.Ordinal)
                    && !typeName.Equals("BepInPluginAttribute", StringComparison.Ordinal))
                {
                    continue;
                }

                var guid = TryReadFirstConstructorString(reader, attr);
                if (!string.IsNullOrWhiteSpace(guid))
                    guids.Add(guid);
            }
        }
        catch
        {
            // Native files or truncated DLLs are ignored.
        }

        return guids;
    }

    private static bool TryGetAttributeTypeName(MetadataReader reader, CustomAttribute attr, out string name)
    {
        name = "";
        var ctor = attr.Constructor;
        EntityHandle typeHandle;
        if (ctor.Kind == HandleKind.MemberReference)
        {
            typeHandle = reader.GetMemberReference((MemberReferenceHandle)ctor).Parent;
        }
        else if (ctor.Kind == HandleKind.MethodDefinition)
        {
            typeHandle = reader.GetMethodDefinition((MethodDefinitionHandle)ctor).GetDeclaringType();
        }
        else
        {
            return false;
        }

        if (typeHandle.Kind == HandleKind.TypeReference)
        {
            name = reader.GetString(reader.GetTypeReference((TypeReferenceHandle)typeHandle).Name);
            return true;
        }

        if (typeHandle.Kind == HandleKind.TypeDefinition)
        {
            name = reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle).Name);
            return true;
        }

        return false;
    }

    private static string? TryReadFirstConstructorString(MetadataReader reader, CustomAttribute attr)
    {
        try
        {
            var blob = reader.GetBlobReader(attr.Value);
            if (blob.RemainingBytes < 2 || blob.ReadUInt16() != 1)
                return null;

            return blob.ReadSerializedString();
        }
        catch
        {
            return null;
        }
    }
}
