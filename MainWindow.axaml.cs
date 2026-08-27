using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using BepInExInstaller.Services;

namespace BepInExInstaller;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ZipType = new("Zip archive")
    {
        Patterns = ["*.zip"],
        MimeTypes = ["application/zip"]
    };

    private readonly List<DetectedGame> _allGames = [];
    private string? _gamePath;
    private bool _busy;
    private bool _applyingSelection;
    private bool _applyingConsole;
    private bool _applyingConfigManager;
    private InstalledMod? _pendingModUninstall;
    private string? _pendingModZip;
    private bool _isHandheld;
    private bool _handheldShowActions;

    public MainWindow()
    {
        InitializeComponent();
        ApplyChromeInset(WindowDecorationMargin);
        if (HandheldLayout.ShouldForceExpanded(HandheldLayout.TryMeasurePrimaryDiagonalInches()))
            WindowState = WindowState.Maximized;
        PropertyChanged += OnWindowPropertyChanged;
        ApplyLayoutMode();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowDecorationMarginProperty)
            ApplyChromeInset(WindowDecorationMargin);
        else if (e.Property == WindowStateProperty)
            ApplyLayoutMode();
    }

    private void ApplyChromeInset(Thickness chrome)
    {
        ContentRoot.Margin = new Thickness(
            Math.Max(20, chrome.Left + 16),
            chrome.Top + 10,
            Math.Max(20, chrome.Right + 16),
            16);
    }

    private void ApplyLayoutMode()
    {
        var decision = HandheldLayout.Detect(WindowState);
        _isHandheld = decision.IsHandheld;
        Classes.Set("handheld", _isHandheld);

        if (GamesHint is not null)
        {
            GamesHint.Text = _isHandheld
                ? "Tap a game. If it isn't listed, add it manually."
                : "Click a game below. If it isn't listed, add it manually.";
        }

        ApplyHandheldPanes();
        ApplyChromeInset(WindowDecorationMargin);
        ApplyConfirmLayout();
    }

    private void ApplyHandheldPanes()
    {
        if (GamesSection is null)
            return;

        if (!_isHandheld)
            _handheldShowActions = false;

        var showActions = _isHandheld && _handheldShowActions && _gamePath is not null;
        var showGames = !showActions;

        if (HandheldChrome is not null)
            HandheldChrome.IsVisible = showActions;
        GamesSection.IsVisible = showGames;
        AddGameManuallyButton.IsVisible = showGames;
        AddGameManuallyButton.HorizontalAlignment = _isHandheld
            ? Avalonia.Layout.HorizontalAlignment.Stretch
            : Avalonia.Layout.HorizontalAlignment.Right;

        VersionSection.IsVisible = !_isHandheld || showActions;
        ActionButtons.IsVisible = !_isHandheld || showActions;
        StatusSection.IsVisible = !_isHandheld || showActions;

        if (showActions)
            ManualAddPanel.IsVisible = false;

        ContentRoot.RowDefinitions = showActions
            ? new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto")
            : new RowDefinitions("*,Auto,Auto,Auto,Auto,Auto,Auto");

        ApplyInstalledModsVisibility();
        RefreshConsoleToggle();
        RefreshConfigManagerToggle();
        if (showActions)
            RefreshSelectedGameTitle();
    }

    private void ApplyInstalledModsVisibility()
    {
        if (InstalledModsPanel is null)
            return;

        var showActions = _isHandheld && _handheldShowActions && _gamePath is not null;
        if (_isHandheld && !showActions)
        {
            InstalledModsPanel.IsVisible = false;
            return;
        }

        InstalledModsPanel.IsVisible = _gamePath is not null;
    }

    private void ApplyConfirmLayout()
    {
        if (ConfirmCard is null || ConfirmButtons is null)
            return;

        if (_isHandheld)
        {
            ConfirmCard.Width = double.NaN;
            ConfirmCard.Margin = new Thickness(16);
            ConfirmCard.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            ConfirmButtons.Orientation = Avalonia.Layout.Orientation.Vertical;
            ConfirmButtons.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            foreach (var child in ConfirmButtons.Children)
            {
                if (child is Control control)
                    control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            }
        }
        else
        {
            ConfirmCard.Width = 380;
            ConfirmCard.Margin = new Thickness(0);
            ConfirmCard.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            ConfirmButtons.Orientation = Avalonia.Layout.Orientation.Horizontal;
            ConfirmButtons.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            foreach (var child in ConfirmButtons.Children)
            {
                if (child is Control control)
                    control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            }
        }
    }

    private void OnHandheldBackClick(object? sender, RoutedEventArgs e)
    {
        _handheldShowActions = false;
        ApplyHandheldPanes();
    }

    private void ShowHandheldActions()
    {
        if (!_isHandheld || _gamePath is null)
            return;

        _handheldShowActions = true;
        RefreshSelectedGameTitle();
        ApplyHandheldPanes();
    }

    private void RefreshSelectedGameTitle()
    {
        if (SelectedGameTitle is null)
            return;

        var game = SelectedGame();
        SelectedGameTitle.Text = game?.Name ?? "Selected game";

        var hasIcon = game?.Icon is not null;
        SelectedGameIcon.Source = game?.Icon;
        SelectedGameIconBorder.IsVisible = hasIcon;
        SelectedGameInitialBorder.IsVisible = !hasIcon;
        SelectedGameInitial.Text = game?.Initial ?? "?";
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        SetStatus("Scanning Steam library...");
        EmptyGamesText.Text = "Scanning Steam library...";

        try
        {
            var games = await Task.Run(SteamScanner.FindUnityGames);
            _allGames.Clear();
            _allGames.AddRange(games);
            ApplyGameFilter();
            SetStatus($"Found {games.Count} Unity games.");
        }
        catch (Exception ex)
        {
            _allGames.Clear();
            GamesListBox.ItemsSource = Array.Empty<DetectedGame>();
            EmptyGamesText.Text = "Steam scan failed. Add a game manually using the button below.";
            EmptyGamesText.IsVisible = true;
            SetStatus($"Steam scan failed: {ex.Message}");
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        => ApplyGameFilter();

    private void OnGamesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_applyingSelection || _busy)
            return;

        if (GamesListBox.SelectedItem is not DetectedGame game)
            return;

        _applyingSelection = true;
        try
        {
            PathTextBox.Text = game.GamePath;
            ApplyGameFolder(game.GamePath);
        }
        finally
        {
            _applyingSelection = false;
        }
    }

    private void OnGamesListTapped(object? sender, TappedEventArgs e)
    {
        if (!_isHandheld || _busy || _applyingSelection)
            return;

        if (e.Source is not Visual visual
            || visual.FindAncestorOfType<ListBoxItem>(includeSelf: true) is null)
        {
            return;
        }

        if (GamesListBox.SelectedItem is DetectedGame)
            ShowHandheldActions();
    }

    private async void OnAddGameManuallyClick(object? sender, RoutedEventArgs e)
    {
        ManualAddPanel.IsVisible = true;
        await PickGameFolderAsync();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        => await PickGameFolderAsync();

    private async Task PickGameFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select game install folder",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var folderPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            SetStatus("Could not resolve a local path for that folder.");
            return;
        }

        ClearGameSelection();
        PathTextBox.Text = folderPath;
        ApplyGameFolder(folderPath);
    }

    private void OnPathTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        ClearGameSelection();
        ApplyGameFolder(PathTextBox.Text);
        e.Handled = true;
    }

    private void OnPathTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_applyingSelection)
            return;

        ApplyGameFolder(PathTextBox.Text);
    }

    private void ApplyGameFolder(string? gameInstallPath)
    {
        if (_busy)
            return;

        if (string.IsNullOrWhiteSpace(gameInstallPath))
        {
            if (_gamePath is not null)
            {
                _gamePath = null;
                _handheldShowActions = false;
                SyncListSelection();
                RefreshInstalledMods();
                UpdateActionButtons();
                SetStatus("Ready");
                ApplyHandheldPanes();
            }

            return;
        }

        if (string.Equals(gameInstallPath.Trim().Trim('"'), _gamePath, StringComparison.OrdinalIgnoreCase))
        {
            RefreshInstalledMods();
            ShowInstallStatus();
            ShowHandheldActions();
            return;
        }

        var info = UnityGameDetector.Inspect(gameInstallPath);
        if (info is null)
        {
            _gamePath = null;
            _handheldShowActions = false;
            SyncListSelection();
            RefreshInstalledMods();
            UpdateActionButtons();
            SetStatus("No Unity game was found. Select the game's Steam folder (Manage → Browse local files).");
            ApplyHandheldPanes();
            return;
        }

        _gamePath = info.GamePath;
        PathTextBox.Text = info.GamePath;
        EnsureGameInList(gameInstallPath, info);
        ApplyRecommendedChannel(info.Backend);
        SyncListSelection();
        RefreshInstalledMods();
        UpdateActionButtons();
        ShowInstallStatus();
        ShowHandheldActions();
    }

    private void EnsureGameInList(string pickedPath, UnityGameInfo info)
    {
        var existing = _allGames.FirstOrDefault(game =>
            string.Equals(game.GamePath, info.GamePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return;

        var identity = ManualGameResolver.Resolve(pickedPath, info.GamePath);
        var steamPath = SteamScanner.TryFindSteamPath();
        var artwork = steamPath is null
            ? null
            : GameIconLoader.FindSteamArtwork(steamPath, identity.AppId);
        var state = InstallTracker.Detect(info.GamePath);
        var channelLabel = state.Kind == InstallKind.Managed
            ? SteamScanner.FormatChannel(state.Channel)
            : null;

        _allGames.Add(new DetectedGame
        {
            Name = identity.Name,
            InstallPath = identity.InstallPath,
            GamePath = info.GamePath,
            ExePath = info.ExePath,
            AppId = string.IsNullOrEmpty(identity.AppId) ? null : identity.AppId,
            Icon = GameIconLoader.Load(info.ExePath, artwork),
            ChannelLabel = channelLabel,
            Backend = info.Backend,
            Architecture = info.Architecture,
            Os = info.Os,
            UnityVersion = info.UnityVersion
        });
        _allGames.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
            SearchTextBox.Text = string.Empty;

        ApplyGameFilter();
    }

    private async void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (_gamePath is null || _busy)
            return;

        var gamePath = _gamePath;
        var game = SelectedGame() ?? _allGames.FirstOrDefault(g =>
            string.Equals(g.GamePath, gamePath, StringComparison.OrdinalIgnoreCase));
        var info = game is null
            ? UnityGameDetector.Inspect(gamePath)
            : new UnityGameInfo(game.GamePath, game.ExePath, game.Backend, game.Architecture, game.Os, game.UnityVersion);
        if (info is null)
        {
            SetStatus("Could not re-detect that Unity game.");
            return;
        }

        var channel = VersionComboBox.SelectedIndex == 1
            ? BepInExChannel.BleedingEdge
            : BepInExChannel.Stable;
        var pack = FindGamePack(game, gamePath);
        if (pack?.HasCustomSource == true)
            channel = BepInExChannel.BleedingEdge;

        await RunBusyAsync("Downloading...", async () =>
        {
            var zipPath = await GitHubFetcher.DownloadAsync(
                channel, info.Os, info.Architecture, info.Backend, pack?.Source);
            try
            {
                SetStatus("Extracting...");
                await Task.Run(() => ZipInstaller.InstallBepInEx(zipPath, gamePath, channel));
            }
            finally
            {
                TryDelete(zipPath);
            }

            MarkManagedChannel(gamePath, channel);
            RefreshConsoleToggle();
            RefreshConfigManagerToggle();
            if (pack?.HasCustomSource == true)
            {
                var source = pack.Source?.Tag ?? pack.DisplayName;
                var hint = string.IsNullOrWhiteSpace(pack.InstallHint) ? "" : $" {pack.InstallHint}";
                SetStatus($"Installed Bleeding Edge from {source}.{hint}");
                return;
            }

            SetStatus($"Installed {FormatChannel(channel)} ({UnityGameDetector.FormatBackend(info.Backend)} {UnityGameDetector.FormatArch(info.Architecture == GameArch.Unknown ? GameArch.X64 : info.Architecture)}). {InstallTracker.Detect(gamePath).StatusText}");
        });
    }

    private void OnUninstallClick(object? sender, RoutedEventArgs e)
    {
        if (_gamePath is null || _busy)
            return;

        _pendingModUninstall = null;
        _pendingModZip = null;
        ConfirmTitle.Text = "Uninstall BepInEx?";
        ConfirmBody.Text =
            "This deletes the BepInEx folder (including plugins and config) and Doorstop files such as winhttp.dll.";
        ConfirmActionButton.Content = "Uninstall";
        UninstallConfirmOverlay.IsVisible = true;
    }

    private void OnConfirmCancelClick(object? sender, RoutedEventArgs e)
    {
        UninstallConfirmOverlay.IsVisible = false;
        _pendingModUninstall = null;
        _pendingModZip = null;
    }

    private async void OnConfirmActionClick(object? sender, RoutedEventArgs e)
    {
        UninstallConfirmOverlay.IsVisible = false;
        if (_gamePath is null || _busy)
            return;

        var gamePath = _gamePath;
        var zipPath = _pendingModZip;
        var mod = _pendingModUninstall;
        _pendingModZip = null;
        _pendingModUninstall = null;

        if (zipPath is not null)
        {
            await RunBusyAsync("Installing...", async () =>
            {
                var result = await Task.Run(() => ZipInstaller.InstallMod(zipPath, gamePath));
                RefreshInstalledMods();
                var where = result.Kind == ModPackageKind.GameDirectory
                    ? "the game folder (BepInEx pack / overlay)"
                    : "BepInEx/plugins";
                SetStatus($"Installed {result.Name} into {where}.");
            });
            return;
        }

        if (mod is not null)
        {
            var name = mod.Name;
            await RunBusyAsync($"Removing {name}...", async () =>
            {
                await Task.Run(() => ZipInstaller.UninstallMod(gamePath, mod.Id));
                RefreshInstalledMods();
                SetStatus($"Removed {name}.");
            });
            return;
        }

        await RunBusyAsync("Uninstalling BepInEx...", async () =>
        {
            await Task.Run(() => ZipInstaller.UninstallBepInEx(gamePath));
            ClearManagedChannel(gamePath);
            RefreshInstalledMods();
            RefreshConsoleToggle();
            RefreshConfigManagerToggle();
            SetStatus("BepInEx was removed from this game.");
        });
    }

    private async void OnInstallModClick(object? sender, RoutedEventArgs e)
    {
        if (_gamePath is null || _busy)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Plugin Zip",
            AllowMultiple = false,
            FileTypeFilter = [ZipType]
        });

        if (files.Count == 0)
            return;

        var zipPath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(zipPath))
        {
            SetStatus("Could not resolve a local path for that zip.");
            return;
        }

        _pendingModUninstall = null;
        _pendingModZip = zipPath;
        var name = Path.GetFileNameWithoutExtension(zipPath);
        if (string.IsNullOrWhiteSpace(name))
            name = "this plugin";

        ConfirmTitle.Text = $"Install {name}?";
        ConfirmBody.Text = DescribeModZip(zipPath, name);
        ConfirmActionButton.Content = "Install";
        UninstallConfirmOverlay.IsVisible = true;
    }

    private static string DescribeModZip(string zipPath, string name)
    {
        try
        {
            var kind = ZipInstaller.PeekModZipKind(zipPath);
            return kind == ModPackageKind.GameDirectory
                ? $"This installs {name} into the game folder. You can uninstall it later from Installed plugins."
                : $"This installs {name} into BepInEx/plugins. You can uninstall it later from Installed plugins.";
        }
        catch
        {
            return $"This installs {name} into the selected game. You can uninstall it later from Installed plugins.";
        }
    }

    private void OnUninstallModClick(object? sender, RoutedEventArgs e)
    {
        if (_gamePath is null || _busy)
            return;

        if (InstalledModsCombo.SelectedItem is not InstalledMod mod)
            return;

        _pendingModZip = null;
        _pendingModUninstall = mod;
        ConfirmTitle.Text = $"Remove {mod.Name}?";
        ConfirmBody.Text =
            "This deletes the files this app installed for that plugin, plus any matching BepInEx/config/*.cfg created on first launch. BepInEx itself is left alone.";
        ConfirmActionButton.Content = "Uninstall";
        UninstallConfirmOverlay.IsVisible = true;
    }

    private async Task RunBusyAsync(string status, Func<Task> work)
    {
        _busy = true;
        UpdateActionButtons();
        BusyBar.IsVisible = true;
        SetStatus(status);

        try
        {
            await work();
        }
        catch (Exception ex)
        {
            SetStatus($"Failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
            BusyBar.IsVisible = false;
            UpdateActionButtons();
        }
    }

    private void OnInstalledModsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        => UpdateActionButtons();

    private void ApplyGameFilter()
    {
        var query = SearchTextBox.Text?.Trim() ?? string.Empty;
        IEnumerable<DetectedGame> filtered = _allGames;
        if (query.Length > 0)
            filtered = _allGames.Where(game => game.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();
        var selected = _gamePath is null
            ? null
            : list.FirstOrDefault(game =>
                string.Equals(game.GamePath, _gamePath, StringComparison.OrdinalIgnoreCase));

        _applyingSelection = true;
        try
        {
            GamesListBox.ItemsSource = list;
            GamesListBox.SelectedItem = selected;
        }
        finally
        {
            _applyingSelection = false;
        }

        EmptyGamesText.IsVisible = list.Count == 0;
        if (list.Count > 0)
            return;

        EmptyGamesText.Text = _allGames.Count > 0
            ? "No matching games."
            : "No Unity games found in Steam. Add one manually using the button below.";
    }

    private void MarkManagedChannel(string gamePath, BepInExChannel channel)
    {
        var label = FormatChannel(channel);
        foreach (var game in _allGames)
        {
            if (string.Equals(game.GamePath, gamePath, StringComparison.OrdinalIgnoreCase))
                game.ChannelLabel = label;
        }

        ApplyGameFilter();
    }

    private void ClearManagedChannel(string gamePath)
    {
        foreach (var game in _allGames)
        {
            if (string.Equals(game.GamePath, gamePath, StringComparison.OrdinalIgnoreCase))
                game.ChannelLabel = null;
        }

        ApplyGameFilter();
    }

    private void SyncListSelection()
    {
        DetectedGame? match = null;
        if (_gamePath is not null)
        {
            match = _allGames.FirstOrDefault(game =>
                string.Equals(game.GamePath, _gamePath, StringComparison.OrdinalIgnoreCase));
        }

        if (ReferenceEquals(GamesListBox.SelectedItem, match))
            return;

        _applyingSelection = true;
        try
        {
            GamesListBox.SelectedItem = match;
        }
        finally
        {
            _applyingSelection = false;
        }
    }

    private void ClearGameSelection()
    {
        if (GamesListBox.SelectedItem is null)
            return;

        _applyingSelection = true;
        try
        {
            GamesListBox.SelectedItem = null;
        }
        finally
        {
            _applyingSelection = false;
        }
    }

    private void UpdateActionButtons()
    {
        var canAct = !_busy && _gamePath is not null;
        var canUninstall = canAct
                           && InstallTracker.Detect(_gamePath!).Kind != InstallKind.None;
        InstallButton.IsEnabled = canAct;
        InstallModButton.IsEnabled = canAct;
        UninstallButton.IsEnabled = canUninstall;
        BrowseButton.IsEnabled = !_busy;
        AddGameManuallyButton.IsEnabled = !_busy;
        VersionComboBox.IsEnabled = !_busy;
        GamesListBox.IsEnabled = !_busy;
        SearchTextBox.IsEnabled = !_busy;
        PathTextBox.IsEnabled = !_busy;
        UninstallModButton.IsEnabled = canAct
                                       && InstalledModsCombo.SelectedItem is InstalledMod;
        InstalledModsCombo.IsEnabled = !_busy;
        if (ConsoleCheckBox is not null)
            ConsoleCheckBox.IsEnabled = canAct && ConsolePanel.IsVisible;
        if (ConfigManagerCheckBox is not null)
            ConfigManagerCheckBox.IsEnabled = canAct && ConfigManagerPanel is { IsVisible: true };
    }

    private void RefreshInstalledMods()
    {
        InstalledModsCombo.SelectedItem = null;
        InstalledModsCombo.ItemsSource = null;

        if (_gamePath is null)
        {
            InstalledModsPanel.IsVisible = false;
            return;
        }

        var mods = ModTracker.List(_gamePath).ToList();
        InstalledModsCombo.ItemsSource = mods;
        InstalledModsCombo.SelectedIndex = mods.Count > 0 ? 0 : -1;
        ApplyInstalledModsVisibility();
        RefreshConsoleToggle();
        RefreshConfigManagerToggle();
    }

    private void ShowInstallStatus()
    {
        if (_gamePath is null)
        {
            SetStatus("Ready");
            return;
        }

        var game = SelectedGame();
        var pack = FindGamePack(game, _gamePath);
        var detected = game is null
            ? null
            : $"{UnityGameDetector.FormatBackend(game.Backend)} {UnityGameDetector.FormatArch(game.Architecture == GameArch.Unknown ? GameArch.X64 : game.Architecture)}";
        var hint = pack?.InstallHint is { Length: > 0 } installHint
            ? $" {installHint}"
            : game?.Backend == ScriptingBackend.Il2Cpp
                ? " IL2CPP needs Bleeding Edge (BepInEx 6)."
                : "";
        SetStatus($"{InstallTracker.Detect(_gamePath).StatusText}{(detected is null ? "" : $" Detected {detected}.")}{hint}");
    }

    private void RefreshConsoleToggle()
    {
        RefreshConfigManagerToggle();

        if (ConsolePanel is null || ConsoleCheckBox is null)
            return;

        var showActions = _isHandheld && _handheldShowActions && _gamePath is not null;
        if (_isHandheld && !showActions)
        {
            ConsolePanel.IsVisible = false;
            return;
        }

        var installed = _gamePath is not null
                        && InstallTracker.Detect(_gamePath).Kind != InstallKind.None;
        ConsolePanel.IsVisible = installed;
        ConsoleCheckBox.IsEnabled = installed && !_busy;

        _applyingConsole = true;
        try
        {
            ConsoleCheckBox.IsChecked = installed && BepInExCfgPatcher.ReadConsoleEnabled(_gamePath!);
        }
        finally
        {
            _applyingConsole = false;
        }
    }

    private void OnConsoleCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_applyingConsole || _busy || _gamePath is null)
            return;

        var enabled = ConsoleCheckBox.IsChecked == true;
        try
        {
            BepInExCfgPatcher.SetConsoleEnabled(_gamePath, enabled);
            SetStatus(enabled
                ? "Log console enabled in BepInEx.cfg. Launch the game to see it."
                : "Log console disabled in BepInEx.cfg.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not update BepInEx.cfg: {ex.Message}");
            RefreshConsoleToggle();
        }
    }

    private void RefreshConfigManagerToggle()
    {
        if (ConfigManagerPanel is null || ConfigManagerCheckBox is null)
            return;

        var showActions = _isHandheld && _handheldShowActions && _gamePath is not null;
        if (_isHandheld && !showActions)
        {
            ConfigManagerPanel.IsVisible = false;
            return;
        }

        var bepinex = _gamePath is not null
                      && InstallTracker.Detect(_gamePath).Kind != InstallKind.None;
        var backend = CurrentBackend();
        var channel = CurrentInstalledChannel();
        var offer = bepinex && ConfigurationManagerSupport.CanOffer(backend, channel);
        ConfigManagerPanel.IsVisible = offer;
        ConfigManagerCheckBox.IsEnabled = offer && !_busy;

        if (ConfigManagerHint is not null)
            ConfigManagerHint.Text = ConfigurationManagerSupport.Hint(backend);

        _applyingConfigManager = true;
        try
        {
            ConfigManagerCheckBox.IsChecked = offer
                                              && _gamePath is not null
                                              && ConfigurationManagerSupport.IsInstalled(_gamePath);
        }
        finally
        {
            _applyingConfigManager = false;
        }
    }

    private async void OnConfigManagerCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_applyingConfigManager || _busy || _gamePath is null)
            return;

        var gamePath = _gamePath;
        var want = ConfigManagerCheckBox.IsChecked == true;
        var have = ConfigurationManagerSupport.IsInstalled(gamePath);
        if (want == have)
            return;

        var il2cpp = CurrentBackend() == ScriptingBackend.Il2Cpp;

        if (want)
        {
            await RunBusyAsync("Downloading Configuration Manager...", async () =>
            {
                var zipPath = await GitHubFetcher.DownloadConfigurationManagerAsync(il2cpp);
                try
                {
                    SetStatus("Installing Configuration Manager...");
                    await Task.Run(() => ZipInstaller.InstallMod(
                        zipPath, gamePath, ConfigurationManagerSupport.DisplayName));
                }
                finally
                {
                    TryDelete(zipPath);
                }

                RefreshInstalledMods();
                SetStatus(il2cpp
                    ? "Installed Configuration Manager. Press F1 in-game. On some IL2CPP titles the menu never appears (Unity IMGUI is stripped)."
                    : "Installed Configuration Manager. Press F1 in-game.");
            });
            RefreshConfigManagerToggle();
            return;
        }

        var tracked = ConfigurationManagerSupport.FindTracked(gamePath);
        if (tracked is null)
        {
            SetStatus("Configuration Manager is present but was not installed by this app. Remove BepInEx/plugins/ConfigurationManager by hand, or uninstall it from Installed plugins.");
            RefreshConfigManagerToggle();
            return;
        }

        await RunBusyAsync("Removing Configuration Manager...", async () =>
        {
            await Task.Run(() => ZipInstaller.UninstallMod(gamePath, tracked.Id));
            RefreshInstalledMods();
            SetStatus("Removed Configuration Manager.");
        });
        RefreshConfigManagerToggle();
    }

    private ScriptingBackend CurrentBackend()
    {
        var game = SelectedGame();
        if (game is not null)
            return game.Backend;

        if (_gamePath is null)
            return ScriptingBackend.Unknown;

        return UnityGameDetector.Inspect(_gamePath)?.Backend ?? ScriptingBackend.Unknown;
    }

    private BepInExChannel CurrentInstalledChannel()
    {
        if (_gamePath is not null)
        {
            var channel = InstallTracker.Detect(_gamePath).Channel;
            if (channel is not null)
                return channel.Value;
        }

        return VersionComboBox.SelectedIndex == 1
            ? BepInExChannel.BleedingEdge
            : BepInExChannel.Stable;
    }

    private void ApplyRecommendedChannel(ScriptingBackend backend)
    {
        if (VersionComboBox is null)
            return;

        var pack = FindGamePack(SelectedGame(), _gamePath);
        VersionComboBox.SelectedIndex = backend == ScriptingBackend.Il2Cpp || pack?.HasCustomSource == true
            ? 1
            : 0;
    }

    private static KnownGamePack? FindGamePack(DetectedGame? game, string? gamePath)
        => game is not null
            ? KnownGameCatalog.Find(game)
            : KnownGameCatalog.Find(null, null, null, gamePath);

    private DetectedGame? SelectedGame()
        => _gamePath is null
            ? null
            : _allGames.FirstOrDefault(g =>
                string.Equals(g.GamePath, _gamePath, StringComparison.OrdinalIgnoreCase));

    private static string FormatChannel(BepInExChannel channel)
        => channel == BepInExChannel.BleedingEdge ? "Bleeding Edge" : "Stable";

    private void SetStatus(string message) => StatusText.Text = message;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Temp cleanup is best-effort.
        }
    }
}
