using BepInExInstaller.Services;

namespace BepInExInstaller.Tests;

public sealed class BepInExCfgPatcherTests
{
    [Fact]
    public void Missing_file_reads_as_disabled()
    {
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Path);
        Assert.False(BepInExCfgPatcher.ReadConsoleEnabled(temp.Path));
    }

    [Fact]
    public void Creates_cfg_when_missing()
    {
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Path);

        BepInExCfgPatcher.SetConsoleEnabled(temp.Path, true);

        var path = BepInExCfgPatcher.ConfigPath(temp.Path);
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("[Logging.Console]", text);
        Assert.Contains("Enabled = true", text);
        Assert.True(BepInExCfgPatcher.ReadConsoleEnabled(temp.Path));
    }

    [Fact]
    public void Updates_existing_Enabled_and_keeps_comments()
    {
        using var temp = new TempDir();
        var path = BepInExCfgPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            [Logging.Console]

            ## Enables showing a console for log output.
            # Setting type: Boolean
            # Default value: false
            Enabled = false

            [Logging.Disk]
            Enabled = true
            """);

        BepInExCfgPatcher.SetConsoleEnabled(temp.Path, true);

        var text = File.ReadAllText(path);
        Assert.Contains("Enabled = true", text);
        Assert.Contains("# Default value: false", text);
        Assert.Contains("[Logging.Disk]", text);
        Assert.Contains("Enabled = true", text[text.IndexOf("[Logging.Disk]", StringComparison.Ordinal)..]);
        Assert.True(BepInExCfgPatcher.ReadConsoleEnabled(temp.Path));

        BepInExCfgPatcher.SetConsoleEnabled(temp.Path, false);
        Assert.False(BepInExCfgPatcher.ReadConsoleEnabled(temp.Path));
        Assert.Contains("Enabled = false", File.ReadAllText(path));
    }

    [Fact]
    public void Adds_section_when_cfg_exists_without_console()
    {
        using var temp = new TempDir();
        var path = BepInExCfgPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[Logging.Disk]\nEnabled = true\n");

        BepInExCfgPatcher.SetConsoleEnabled(temp.Path, true);

        var text = File.ReadAllText(path);
        Assert.Contains("[Logging.Disk]", text);
        Assert.Contains("[Logging.Console]", text);
        Assert.True(BepInExCfgPatcher.ReadConsoleEnabled(temp.Path));
    }

    [Fact]
    public void Adds_key_when_section_exists_without_Enabled()
    {
        using var temp = new TempDir();
        var path = BepInExCfgPatcher.ConfigPath(temp.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[Logging.Console]\nLogLevels = Info\n");

        BepInExCfgPatcher.SetConsoleEnabled(temp.Path, true);

        var text = File.ReadAllText(path);
        Assert.Contains("Enabled = true", text);
        Assert.Contains("LogLevels = Info", text);
    }
}
