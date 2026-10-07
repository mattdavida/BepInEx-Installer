using System.Text;

namespace BepInExInstaller.Services;

/// <summary>
/// Reads and writes <c>BepInEx/config/BepInEx.cfg</c>. Creates a minimal file when
/// the game has not generated one yet (that normally happens on first launch).
/// </summary>
public static class BepInExCfgPatcher
{
    public const string Section = "Logging.Console";
    public const string Key = "Enabled";
    public const string UnityLogSection = "Logging";
    public const string UnityLogKey = "UnityLogListening";

    public static string ConfigPath(string gamePath)
        => Path.Combine(gamePath, "BepInEx", "config", "BepInEx.cfg");

    public static bool ReadConsoleEnabled(string gamePath)
    {
        var path = ConfigPath(gamePath);
        if (!File.Exists(path))
            return false;

        try
        {
            var lines = File.ReadAllLines(path);
            var section = IndexOfSection(lines, Section);
            if (section < 0)
                return false;

            var value = FindKeyValue(lines, section, Key);
            return value is not null && IsTrue(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetConsoleEnabled(string gamePath, bool enabled)
    {
        var path = ConfigPath(gamePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        List<string> lines;
        if (File.Exists(path))
        {
            lines = File.ReadAllLines(path).ToList();
        }
        else
        {
            lines =
            [
                $"[{Section}]",
                "",
                "## Enables showing a console for log output.",
                "# Setting type: Boolean",
                "# Default value: false",
                $"{Key} = {(enabled ? "true" : "false")}"
            ];
            Write(path, lines);
            return;
        }

        var section = IndexOfSection(lines, Section);
        if (section < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add("");
            lines.Add($"[{Section}]");
            section = lines.Count - 1;
        }

        SetKeyInSection(lines, section, Key, enabled ? "true" : "false");
        Write(path, lines);
    }

    public static bool ReadUnityLogListening(string gamePath)
    {
        var path = ConfigPath(gamePath);
        if (!File.Exists(path))
            return true;

        try
        {
            var lines = File.ReadAllLines(path);
            var section = IndexOfSection(lines, UnityLogSection);
            if (section < 0)
                return true;

            var value = FindKeyValue(lines, section, UnityLogKey);
            return value is null || IsTrue(value);
        }
        catch
        {
            return true;
        }
    }

    public static void SetUnityLogListening(string gamePath, bool enabled)
    {
        var path = ConfigPath(gamePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        List<string> lines;
        if (File.Exists(path))
            lines = File.ReadAllLines(path).ToList();
        else
            lines = [];

        var section = IndexOfSection(lines, UnityLogSection);
        if (section < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add("");
            lines.Add($"[{UnityLogSection}]");
            lines.Add("");
            lines.Add("## Enables showing unity log messages in the BepInEx logging system.");
            lines.Add("# Setting type: Boolean");
            lines.Add("# Default value: true");
            lines.Add($"{UnityLogKey} = {(enabled ? "true" : "false")}");
            Write(path, lines);
            return;
        }

        SetKeyInSection(lines, section, UnityLogKey, enabled ? "true" : "false");
        Write(path, lines);
    }

    private static void Write(string path, List<string> lines)
        => File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

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

    private static string? FindKeyValue(IReadOnlyList<string> lines, int sectionIndex, string key)
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

            return trimmed[(equals + 1)..].Trim();
        }

        return null;
    }

    private static void SetKeyInSection(List<string> lines, int sectionIndex, string key, string value)
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
            return;
        }

        lines.Insert(sectionIndex + 1, $"{key} = {value}");
    }

    private static bool IsTrue(string value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase)
           || value.Equals("1", StringComparison.OrdinalIgnoreCase)
           || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}
