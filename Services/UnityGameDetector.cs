using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace BepInExInstaller.Services;

public enum ScriptingBackend
{
    Unknown,
    Mono,
    Il2Cpp
}

public enum GameArch
{
    Unknown,
    X86,
    X64
}

public enum GameOs
{
    Windows,
    Linux,
    MacOs
}

public sealed record UnityGameInfo(
    string GamePath,
    string? ExePath,
    ScriptingBackend Backend,
    GameArch Architecture,
    GameOs Os,
    string? UnityVersion);

/// <summary>
/// Finds a Unity game root the same way MelonLoader.Installer does: a <c>*_Data</c>
/// folder whose companion executable exists. Also classifies Mono vs IL2CPP and
/// reads x86/x64 from the PE/ELF header.
/// </summary>
public static class UnityGameDetector
{
    private static readonly HashSet<string> SkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BepInEx",
        "MelonLoader",
        "EasyAntiCheat",
        "MonoBleedingEdge",
        "Mono",
        ".git",
        "dotnet",
        "Engine",
        "Content",
        "Intermediate",
        "Saved",
        "DerivedDataCache",
        "Movies",
        "Plugins",
        "_CommonRedist",
        "Redistributables",
        "__Installer"
    };

    /// <summary>
    /// Stop walking a Steam install after this many folders. Unity titles have
    /// <c>*_Data</c> at the install root; 30 is already past a realistic tree.
    /// Unreal / RE Engine installs used to stall the scan.
    /// </summary>
    internal const int MaxDirectoriesToVisit = 30;

    public static string? FindGameRoot(string gameInstallPath)
    {
        var startDir = ResolveStartDirectory(gameInstallPath);
        if (startDir is null)
            return null;

        if (TryInspect(startDir, out _))
            return startDir;

        // Steam's game root is usually the Unity exe + *_Data folder. Walk a
        // little in case of a wrapper folder, but cap visits so a huge
        // non-Unity install cannot stall the Steam scan.
        return FindUnityUnder(startDir, maxDepth: 6, MaxDirectoriesToVisit);
    }

    public static UnityGameInfo? Inspect(string gameInstallPath)
    {
        var root = FindGameRoot(gameInstallPath);
        if (root is null || !TryInspect(root, out var info))
            return null;

        return info;
    }

    public static string? FindGameExecutable(string gamePath)
        => TryInspect(gamePath, out var info) ? info.ExePath : null;

    internal static bool TryInspect(string gamePath, out UnityGameInfo info)
    {
        info = null!;
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            return false;

        if (!TryResolveExecutable(gamePath, out var exePath, out var os))
            return false;

        var dataDir = FindDataDirectory(gamePath, exePath);
        var backend = DetectBackend(gamePath, dataDir);
        var arch = DetectArchitecture(gamePath, exePath, os);
        var version = ReadUnityVersion(gamePath, exePath);

        info = new UnityGameInfo(Path.GetFullPath(gamePath), exePath, backend, arch, os, version);
        return true;
    }

    internal static ScriptingBackend DetectBackend(string gamePath, string? dataDir)
    {
        if (HasAny(gamePath, "GameAssembly.dll", "GameAssembly.so", "GameAssembly.dylib")
            || (dataDir is not null && Directory.Exists(Path.Combine(dataDir, "il2cpp_data")))
            || (dataDir is not null && File.Exists(Path.Combine(dataDir, "il2cpp_data", "Metadata", "global-metadata.dat"))))
        {
            return ScriptingBackend.Il2Cpp;
        }

        if ((dataDir is not null && Directory.Exists(Path.Combine(dataDir, "Managed")))
            || Directory.Exists(Path.Combine(gamePath, "MonoBleedingEdge"))
            || Directory.Exists(Path.Combine(gamePath, "Mono")))
        {
            return ScriptingBackend.Mono;
        }

        return ScriptingBackend.Unknown;
    }

    internal static GameArch ReadPeArchitecture(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var pe = new PEReader(stream);
            return pe.PEHeaders.CoffHeader.Machine switch
            {
                Machine.I386 => GameArch.X86,
                Machine.Amd64 => GameArch.X64,
                _ => GameArch.Unknown
            };
        }
        catch
        {
            return GameArch.Unknown;
        }
    }

    internal static GameArch ReadElfArchitecture(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) < 5)
                return GameArch.Unknown;

            if (header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
                return GameArch.Unknown;

            return header[4] switch
            {
                1 => GameArch.X86,
                2 => GameArch.X64,
                _ => GameArch.Unknown
            };
        }
        catch
        {
            return GameArch.Unknown;
        }
    }

    internal static string FormatArch(GameArch arch)
        => arch == GameArch.X86 ? "x86" : "x64";

    internal static string FormatBackend(ScriptingBackend backend)
        => backend switch
        {
            ScriptingBackend.Il2Cpp => "IL2CPP",
            ScriptingBackend.Mono => "Mono",
            _ => "Unity"
        };

    internal static string FormatOs(GameOs os)
        => os switch
        {
            GameOs.Linux => "linux",
            GameOs.MacOs => "macos",
            _ => "win"
        };

    private static string? FindUnityUnder(string directory, int maxDepth, int visitLimit)
    {
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((directory, 0));
        var visited = 0;

        while (pending.Count > 0)
        {
            var (current, depth) = pending.Dequeue();
            if (++visited > visitLimit)
                return null;

            if (depth > 0 && TryInspect(current, out _))
                return current;

            if (depth >= maxDepth)
                continue;

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(current);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (SkipDirectoryNames.Contains(name) || name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                    continue;

                pending.Enqueue((child, depth + 1));
            }
        }

        return null;
    }

    private static bool TryResolveExecutable(string gamePath, out string? exePath, out GameOs os)
    {
        exePath = null;
        os = GameOs.Windows;

        var matches = new List<(string Exe, GameOs Os)>();

        IEnumerable<string> dataDirs;
        try
        {
            dataDirs = Directory.EnumerateDirectories(gamePath, "*_Data");
        }
        catch
        {
            return false;
        }

        foreach (var dataDir in dataDirs)
        {
            var name = Path.GetFileName(dataDir);
            if (name.Length <= 5)
                continue;

            var baseName = name[..^5];
            foreach (var (suffix, candidateOs) in new (string, GameOs)[]
                     {
                         (".exe", GameOs.Windows),
                         (".x86_64", GameOs.Linux),
                         ("", GameOs.Linux)
                     })
            {
                var candidate = Path.Combine(gamePath, baseName + suffix);
                if (!File.Exists(candidate))
                    continue;

                matches.Add((candidate, candidateOs));
                break;
            }
        }

        IEnumerable<string> appDirs;
        try
        {
            appDirs = Directory.EnumerateDirectories(gamePath, "*.app");
        }
        catch
        {
            appDirs = [];
        }

        foreach (var app in appDirs)
            matches.Add((app, GameOs.MacOs));

        if (matches.Count == 0)
            return false;

        var folderName = Path.GetFileName(gamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var preferred = matches.FirstOrDefault(m =>
            Path.GetFileNameWithoutExtension(m.Exe).Equals(folderName, StringComparison.OrdinalIgnoreCase));
        var chosen = preferred.Exe is not null ? preferred : matches[0];

        exePath = chosen.Exe;
        os = chosen.Os;
        return true;
    }

    private static string? FindDataDirectory(string gamePath, string? exePath)
    {
        if (!string.IsNullOrEmpty(exePath))
        {
            var fromExe = Path.Combine(gamePath, Path.GetFileNameWithoutExtension(exePath) + "_Data");
            if (Directory.Exists(fromExe))
                return fromExe;
        }

        try
        {
            return Directory.EnumerateDirectories(gamePath, "*_Data").FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static GameArch DetectArchitecture(string gamePath, string? exePath, GameOs os)
    {
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            if (os == GameOs.Windows)
            {
                var fromExe = ReadPeArchitecture(exePath);
                if (fromExe != GameArch.Unknown)
                    return fromExe;
            }
            else if (os == GameOs.Linux)
            {
                if (exePath.EndsWith(".x86_64", StringComparison.OrdinalIgnoreCase))
                    return GameArch.X64;

                var fromElf = ReadElfArchitecture(exePath);
                if (fromElf != GameArch.Unknown)
                    return fromElf;
            }
        }

        var unityPlayerDll = Path.Combine(gamePath, "UnityPlayer.dll");
        if (File.Exists(unityPlayerDll))
        {
            var fromPlayer = ReadPeArchitecture(unityPlayerDll);
            if (fromPlayer != GameArch.Unknown)
                return fromPlayer;
        }

        var unityPlayerSo = Path.Combine(gamePath, "UnityPlayer.so");
        if (File.Exists(unityPlayerSo))
        {
            var fromSo = ReadElfArchitecture(unityPlayerSo);
            if (fromSo != GameArch.Unknown)
                return fromSo;
        }

        return GameArch.Unknown;
    }

    private static string? ReadUnityVersion(string gamePath, string? exePath)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(gamePath, "UnityPlayer.dll"),
                     exePath
                 })
        {
            if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
                continue;

            try
            {
                var info = FileVersionInfo.GetVersionInfo(candidate);
                var raw = FirstNonEmpty(info.ProductVersion, info.FileVersion);
                if (raw is null)
                    continue;

                var cleaned = raw.Split('+')[0].Trim();
                if (cleaned.Length > 0)
                    return cleaned;
            }
            catch
            {
                // Version resources are optional.
            }
        }

        return null;
    }

    private static bool HasAny(string directory, params string[] names)
        => names.Any(name => File.Exists(Path.Combine(directory, name)));

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? ResolveStartDirectory(string gameInstallPath)
    {
        if (string.IsNullOrWhiteSpace(gameInstallPath))
            return null;

        var full = Path.GetFullPath(gameInstallPath.Trim().Trim('"'));

        if (Directory.Exists(full))
        {
            var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(full);
                return Directory.Exists(parent) ? parent : null;
            }

            return full;
        }

        if (File.Exists(full))
        {
            var parent = Path.GetDirectoryName(full);
            return Directory.Exists(parent) ? parent : null;
        }

        return null;
    }
}
