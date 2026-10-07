using System.Text;

namespace BepInExInstaller.Services;

/// <summary>
/// Patches <c>doorstop_config.ini</c>. Doorstop 4 uses
/// <c>dll_search_path_override</c>; older packs used <c>dllSearchPathOverride</c>.
/// </summary>
public static class DoorstopConfigPatcher
{
    public const string FileName = "doorstop_config.ini";
    public const string Section = "UnityMono";
    public const string Key = "dll_search_path_override";
    public const string LegacyKey = "dllSearchPathOverride";
    public const string GeneralSection = "General";
    public const string IgnoreDisableSwitchKey = "ignore_disable_switch";
    public const string LegacyIgnoreDisableSwitchKey = "ignoreDisableSwitch";

    public static string ConfigPath(string gamePath)
        => Path.Combine(gamePath, FileName);

    public static string? ReadDllSearchPathOverride(string gamePath)
    {
        var path = ConfigPath(gamePath);
        if (!File.Exists(path))
            return null;

        try
        {
            var lines = File.ReadAllLines(path);
            var section = IndexOfSection(lines, Section);
            if (section < 0)
                return FindKeyValue(lines, 0, Key) ?? FindKeyValue(lines, 0, LegacyKey);

            return FindKeyValue(lines, section, Key) ?? FindKeyValue(lines, section, LegacyKey);
        }
        catch
        {
            return null;
        }
    }

    public static void SetDllSearchPathOverride(string gamePath, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)
            || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || folder.Contains('/')
            || folder.Contains('\\'))
        {
            throw new ArgumentException("Doorstop override must be a single folder name.", nameof(folder));
        }

        var path = ConfigPath(gamePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("doorstop_config.ini was not found. Install BepInEx first.", path);

        var lines = File.ReadAllLines(path).ToList();
        var section = IndexOfSection(lines, Section);
        if (section < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add("");
            lines.Add($"[{Section}]");
            section = lines.Count - 1;
        }

        if (!SetKeyInSection(lines, section, Key, folder)
            && !SetKeyInSection(lines, section, LegacyKey, folder))
        {
            lines.Insert(section + 1, $"{Key} = {folder}");
        }

        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static bool ReadIgnoreDisableSwitch(string gamePath)
    {
        var path = ConfigPath(gamePath);
        if (!File.Exists(path))
            return false;

        try
        {
            var lines = File.ReadAllLines(path);
            var section = IndexOfSection(lines, GeneralSection);
            var start = section < 0 ? 0 : section;
            var value = FindKeyValue(lines, start, IgnoreDisableSwitchKey)
                        ?? FindKeyValue(lines, start, LegacyIgnoreDisableSwitchKey);
            return value is not null && IsTrue(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetIgnoreDisableSwitch(string gamePath, bool enabled)
    {
        var path = ConfigPath(gamePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("doorstop_config.ini was not found. Install BepInEx first.", path);

        var lines = File.ReadAllLines(path).ToList();
        var section = IndexOfSection(lines, GeneralSection);
        if (section < 0)
        {
            lines.Insert(0, $"[{GeneralSection}]");
            section = 0;
        }

        var value = enabled ? "true" : "false";
        if (!SetKeyInSection(lines, section, IgnoreDisableSwitchKey, value)
            && !SetKeyInSection(lines, section, LegacyIgnoreDisableSwitchKey, value))
        {
            lines.Insert(section + 1, $"{IgnoreDisableSwitchKey} = {value}");
        }

        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static int IndexOfSection(IReadOnlyList<string> lines, string section)
    {
        var header = $"[{section}]";
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim().Equals(header, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string? FindKeyValue(IReadOnlyList<string> lines, int start, string key)
    {
        for (var i = start; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (i > start && trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                break;

            if (trimmed.StartsWith('#') || trimmed.StartsWith(';') || trimmed.Length == 0)
                continue;

            var equals = trimmed.IndexOf('=');
            if (equals <= 0)
                continue;

            if (!trimmed[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            return trimmed[(equals + 1)..].Trim().Trim('"');
        }

        return null;
    }

    private static bool SetKeyInSection(List<string> lines, int sectionIndex, string key, string value)
    {
        for (var i = sectionIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                break;

            if (trimmed.StartsWith('#') || trimmed.StartsWith(';') || trimmed.Length == 0)
                continue;

            var equals = trimmed.IndexOf('=');
            if (equals <= 0)
                continue;

            if (!trimmed[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            var prefix = lines[i][..lines[i].IndexOf('=')];
            lines[i] = $"{prefix}= {value}";
            return true;
        }

        return false;
    }

    private static bool IsTrue(string value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase)
           || value.Equals("1", StringComparison.OrdinalIgnoreCase)
           || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}
