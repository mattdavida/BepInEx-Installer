using Avalonia.Media.Imaging;

namespace BepInExInstaller.Services;

public sealed class DetectedGame
{
    public required string Name { get; init; }
    public required string InstallPath { get; init; }
    public required string GamePath { get; init; }
    public string? AppId { get; init; }
    public string? ExePath { get; init; }
    public Bitmap? Icon { get; init; }
    public string? ChannelLabel { get; set; }
    public ScriptingBackend Backend { get; init; }
    public GameArch Architecture { get; init; }
    public GameOs Os { get; init; }
    public string? UnityVersion { get; init; }

    public string Initial
    {
        get
        {
            foreach (var c in Name)
            {
                if (char.IsLetterOrDigit(c))
                    return char.ToUpperInvariant(c).ToString();
            }

            return "?";
        }
    }

    public bool HasIcon => Icon is not null;
    public bool HasChannelLabel => !string.IsNullOrEmpty(ChannelLabel);

    public string? SupportBadge
    {
        get
        {
            var backend = UnityGameDetector.FormatBackend(Backend);
            var arch = Architecture == GameArch.Unknown
                ? null
                : UnityGameDetector.FormatArch(Architecture);
            return arch is null ? backend : $"{backend} · {arch}";
        }
    }

    public bool HasSupportBadge => !string.IsNullOrEmpty(SupportBadge);

    public string? PackBadge => KnownGameCatalog.Find(this)?.BadgeText;

    public bool HasPackBadge => !string.IsNullOrEmpty(PackBadge);
}
