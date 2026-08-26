namespace BepInExInstaller.Services;

/// <summary>
/// Turns a folder the user picked into list-row fields (name, Steam install root, optional app id).
/// </summary>
public static class ManualGameResolver
{
    public readonly record struct Identity(string Name, string InstallPath, string AppId);

    public static Identity Resolve(string pickedPath, string gamePath)
    {
        var game = Path.GetFullPath(gamePath);
        var installPath = InferInstallPath(pickedPath, game);
        var name = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var appId = string.Empty;

        if (SteamScanner.TryIdentifyCommonInstall(installPath, out var manifestName, out var manifestAppId))
        {
            if (!string.IsNullOrWhiteSpace(manifestName))
                name = manifestName;
            appId = manifestAppId;
        }

        if (string.IsNullOrWhiteSpace(name))
            name = "Added game";

        return new Identity(name, installPath, appId);
    }

    internal static string InferInstallPath(string pickedPath, string gamePath)
    {
        var game = Path.GetFullPath(gamePath);
        var fromSteam = SteamCommonGameRoot(game);
        if (fromSteam is not null)
            return fromSteam;

        var picked = Path.GetFullPath(pickedPath.Trim().Trim('"'));
        if (Directory.Exists(picked) && IsUnderOrEqual(game, picked))
            return picked;

        return game;
    }

    internal static string? SteamCommonGameRoot(string path)
    {
        var full = Path.GetFullPath(path);
        var parts = full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 2; i++)
        {
            if (!parts[i].Equals("steamapps", StringComparison.OrdinalIgnoreCase)
                || !parts[i + 1].Equals("common", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var take = i + 3;
            return Path.GetFullPath(string.Join(Path.DirectorySeparatorChar, parts[..take]));
        }

        return null;
    }

    private static bool IsUnderOrEqual(string child, string parent)
    {
        var a = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var b = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = b + Path.DirectorySeparatorChar;
        return a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
