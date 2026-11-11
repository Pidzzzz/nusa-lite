using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using NusaLite.Services;

namespace NusaLite.Views;

public partial class SettingsWindow : Window
{
    private const int LyricOffsetMin = -2000;
    private const int LyricOffsetMax = 2000;

    private readonly SettingsStore _settings;
    private readonly Action<bool>? _clickGuardChanged;
    private readonly Action<bool>? _autoLyricsChanged;
    private readonly Action<bool>? _fullscreenChanged;
    private readonly Action<bool>? _hotkeysChanged;
    private readonly Action? _guardGhostRefresh;
    private bool _loading = true;

    public SettingsWindow(
        SettingsStore settings,
        Action<bool>? clickGuardChanged = null,
        Action<bool>? autoLyricsChanged = null,
        Action<bool>? fullscreenChanged = null,
        Action<bool>? hotkeysChanged = null,
        Action? guardGhostRefresh = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clickGuardChanged = clickGuardChanged;
        _autoLyricsChanged = autoLyricsChanged;
        _fullscreenChanged = fullscreenChanged;
        _hotkeysChanged = hotkeysChanged;
        _guardGhostRefresh = guardGhostRefresh;

        InitializeComponent();

        VersionText.Text = $"NusaLite v{GetVersion()}";

        SectionList.SelectedIndex = 0;
        LoadFromSettings();
        _loading = false;
    }

    private static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? "1.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void LoadFromSettings()
    {
        var s = _settings.Current;

        StartWithWindowsCheck.IsChecked = Autostart.IsEnabled();
        HotkeysCheck.IsChecked = s.EnableGlobalHotkeys;
        FullscreenCheck.IsChecked = s.FullscreenAutoHide;
        AutoLyricsCheck.IsChecked = s.AutoLyricsOnPlay;
        LyricOffsetBox.Text = s.LyricOffsetMs.ToString();

        ClickGuardCheck.IsChecked = s.ClickGuardEnabled;
        AutoClickGuardCheck.IsChecked = s.AutoClickGuard;

        HoverDelaySlider.Value = s.HoverDelayMs;
        HoverDelayValue.Text = $"{s.HoverDelayMs} ms";

        var percent = (int)Math.Round(Math.Clamp(s.GuardOpacity, 0.15, 1.0) * 100);
        GuardOpacitySlider.Value = percent;
        GuardOpacityValue.Text = $"{percent} %";

        FocusMinutesBox.Text = s.FocusMinutes.ToString();
        BreakMinutesBox.Text = s.BreakMinutes.ToString();
        WaterGoalBox.Text = s.WaterGoalMl.ToString();
        ReminderBox.Text = s.WaterReminderMinutes.ToString();

        ThemeGraphiteOption.IsChecked = string.Equals(s.Theme, "Graphite", StringComparison.OrdinalIgnoreCase);
        ThemeVoidOption.IsChecked = string.Equals(s.Theme, "Void", StringComparison.OrdinalIgnoreCase);
        ThemeLightOption.IsChecked = string.Equals(s.Theme, "Light", StringComparison.OrdinalIgnoreCase);

        if (ThemeGraphiteOption.IsChecked != true
            && ThemeVoidOption.IsChecked != true
            && ThemeLightOption.IsChecked != true)
        {
            ThemeGraphiteOption.IsChecked = true;
        }
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelUmum is null)
        {
            return;
        }

        var selected = SectionList.SelectedIndex;

        PanelUmum.Visibility = selected == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelNotch.Visibility = selected == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelFokusAir.Visibility = selected == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelTentang.Visibility = selected == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StartWithWindows_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = StartWithWindowsCheck.IsChecked == true;
        Autostart.SetEnabled(enabled);
        _settings.Current.StartWithWindows = enabled;
        _settings.Save();
    }

    private void Hotkeys_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = HotkeysCheck.IsChecked == true;
        _settings.Current.EnableGlobalHotkeys = enabled;
        _settings.Save();
        _hotkeysChanged?.Invoke(enabled);
    }

    private void Fullscreen_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = FullscreenCheck.IsChecked == true;
        _settings.Current.FullscreenAutoHide = enabled;
        _settings.Save();
        _fullscreenChanged?.Invoke(enabled);
    }

    private void AutoLyrics_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = AutoLyricsCheck.IsChecked == true;
        _settings.Current.AutoLyricsOnPlay = enabled;
        _settings.Save();
        _autoLyricsChanged?.Invoke(enabled);
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton option || option.Tag is not string theme)
        {
            return;
        }

        ThemeService.Apply(theme);
        _settings.Current.Theme = ThemeService.Current;
        _settings.Save();
    }

    private void ClickGuard_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = ClickGuardCheck.IsChecked == true;
        _settings.Current.ClickGuardEnabled = enabled;
        _settings.Save();
        _clickGuardChanged?.Invoke(enabled);
    }

    private void AutoClickGuard_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.AutoClickGuard = AutoClickGuardCheck.IsChecked == true;
        _settings.Save();
    }

    private void HoverDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)Math.Round(e.NewValue);
        if (HoverDelayValue is not null)
        {
            HoverDelayValue.Text = $"{value} ms";
        }

        if (_loading)
        {
            return;
        }

        _settings.Current.HoverDelayMs = value;
        _settings.Save();
    }

    private void GuardOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var percent = (int)Math.Round(e.NewValue);
        if (GuardOpacityValue is not null)
        {
            GuardOpacityValue.Text = $"{percent} %";
        }

        if (_loading)
        {
            return;
        }

        _settings.Current.GuardOpacity = Math.Clamp(percent / 100.0, 0.15, 1.0);
        _settings.Save();
        _guardGhostRefresh?.Invoke();
    }

    private void LyricOffset_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || LyricOffsetOffsetInvalid())
        {
            return;
        }

        CommitLyricOffset();
    }

    private void LyricOffset_LostFocus(object sender, RoutedEventArgs e)
    {
        if (LyricOffsetOffsetInvalid())
        {
            LyricOffsetBox.Text = _settings.Current.LyricOffsetMs.ToString();
            return;
        }

        CommitLyricOffset();
    }

    private void LyricOffset_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        if (LyricOffsetOffsetInvalid())
        {
            LyricOffsetBox.Text = _settings.Current.LyricOffsetMs.ToString();
            return;
        }

        CommitLyricOffset();
        e.Handled = true;
    }

    private bool LyricOffsetOffsetInvalid()
    {
        return !int.TryParse(LyricOffsetBox.Text, out var value)
            || value < LyricOffsetMin
            || value > LyricOffsetMax;
    }

    private void CommitLyricOffset()
    {
        if (!int.TryParse(LyricOffsetBox.Text, out var value))
        {
            return;
        }

        if (value == _settings.Current.LyricOffsetMs)
        {
            return;
        }

        _settings.Current.LyricOffsetMs = value;
        _settings.Save();
    }

    private void Number_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || sender is not TextBox box)
        {
            return;
        }

        TryApplyNumber(box);
    }

    private void Number_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && !TryApplyNumber(box))
        {
            box.Text = StoredNumberText(box);
        }
    }

    private void Number_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || sender is not TextBox box)
        {
            return;
        }

        if (!TryApplyNumber(box))
        {
            box.Text = StoredNumberText(box);
        }

        e.Handled = true;
    }

    private bool TryApplyNumber(TextBox box)
    {
        if (!int.TryParse(box.Text, out var value))
        {
            return false;
        }

        if (box == FocusMinutesBox)
        {
            if (value < 1 || value > 180)
            {
                return false;
            }

            _settings.Current.FocusMinutes = value;
        }
        else if (box == BreakMinutesBox)
        {
            if (value < 1 || value > 60)
            {
                return false;
            }

            _settings.Current.BreakMinutes = value;
        }
        else if (box == WaterGoalBox)
        {
            if (value < 500 || value > 5000 || value % 50 != 0)
            {
                return false;
            }

            _settings.Current.WaterGoalMl = value;
        }
        else if (box == ReminderBox)
        {
            if (value < 5 || value > 240)
            {
                return false;
            }

            _settings.Current.WaterReminderMinutes = value;
        }
        else
        {
            return false;
        }

        _settings.Save();
        return true;
    }

    private string StoredNumberText(TextBox box)
    {
        var s = _settings.Current;

        if (box == FocusMinutesBox)
        {
            return s.FocusMinutes.ToString();
        }

        if (box == BreakMinutesBox)
        {
            return s.BreakMinutes.ToString();
        }

        if (box == WaterGoalBox)
        {
            return s.WaterGoalMl.ToString();
        }

        if (box == ReminderBox)
        {
            return s.WaterReminderMinutes.ToString();
        }

        return box.Text;
    }

    private void OpenSettingsFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_settings.FilePath))
            {
                _settings.Save();
            }

            Process.Start(new ProcessStartInfo(_settings.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Tidak dapat membuka settings.json.\n{ex.Message}",
                "Pengaturan NusaLite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReleaseLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Tidak dapat membuka tautan.\n{ex.Message}",
                "Pengaturan NusaLite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        e.Handled = true;
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this,
            "Kembalikan semua pengaturan NusaLite ke nilai bawaan?",
            "Kembalikan ke bawaan", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.ResetToDefaults();

        Autostart.SetEnabled(_settings.Current.StartWithWindows);

        _loading = true;
        try
        {
            LoadFromSettings();
        }
        finally
        {
            _loading = false;
        }

        ThemeService.Apply(_settings.Current.Theme);

        _clickGuardChanged?.Invoke(_settings.Current.ClickGuardEnabled);
        _autoLyricsChanged?.Invoke(_settings.Current.AutoLyricsOnPlay);
        _fullscreenChanged?.Invoke(_settings.Current.FullscreenAutoHide);
        _hotkeysChanged?.Invoke(_settings.Current.EnableGlobalHotkeys);
        _guardGhostRefresh?.Invoke();
    }
}
