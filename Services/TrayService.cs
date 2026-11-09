using System.IO;
using Microsoft.Win32;
using NusaLite.Models;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace NusaLite.Services;

public sealed class TrayService : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly Func<bool> _isPinned;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _pinItem;
    private readonly WinForms.ToolStripMenuItem _autoLyricsItem;
    private readonly WinForms.ToolStripMenuItem _fullscreenItem;
    private readonly WinForms.ToolStripMenuItem _clickGuardItem;
    private readonly WinForms.ToolStripMenuItem _hotkeyItem;
    private readonly WinForms.ToolStripMenuItem _startupItem;
    private Stream? _iconStream;

    public TrayService(SettingsStore settings, Func<bool> isPinned)
    {
        _settings = settings;
        _isPinned = isPinned;

        _iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/nusa.ico"))?.Stream;

        var icon = _iconStream is not null
            ? new Drawing.Icon(_iconStream)
            : Drawing.SystemIcons.Application;

        _menu = new WinForms.ContextMenuStrip
        {
            Renderer = new WinForms.ToolStripProfessionalRenderer(new DarkColorTable()),
            BackColor = Drawing.Color.FromArgb(16, 18, 26),
            ForeColor = Drawing.Color.FromArgb(236, 239, 247),
            Font = new Drawing.Font("Segoe UI", 9f),
            ShowImageMargin = false,
            Padding = new WinForms.Padding(4)
        };

        _menu.Items.Add(Item("Buka pemutar", (_, _) => OpenRequested?.Invoke(IslandView.Player)));
        _menu.Items.Add(Item("Ringkasan", (_, _) => OpenRequested?.Invoke(IslandView.Hub)));
        _menu.Items.Add(Item("Lirik", (_, _) => OpenRequested?.Invoke(IslandView.Lyrics)));
        _menu.Items.Add(Item("Fokus", (_, _) => OpenRequested?.Invoke(IslandView.Focus)));
        _menu.Items.Add(Item("Air", (_, _) => OpenRequested?.Invoke(IslandView.Water)));
        _menu.Items.Add(Item("Shelf", (_, _) => OpenRequested?.Invoke(IslandView.Shelf)));
        _menu.Items.Add(new WinForms.ToolStripSeparator());

        _pinItem = Item("Sematkan di layar", (_, _) => PinToggleRequested?.Invoke());
        _pinItem.CheckOnClick = false;
        _menu.Items.Add(_pinItem);

        _autoLyricsItem = Item("Lirik otomatis saat memutar musik", (_, _) => ToggleAutoLyrics());
        _autoLyricsItem.CheckOnClick = false;
        _menu.Items.Add(_autoLyricsItem);

        _fullscreenItem = Item("Sembunyikan saat layar penuh", (_, _) => ToggleFullscreenAutoHide());
        _fullscreenItem.CheckOnClick = false;
        _menu.Items.Add(_fullscreenItem);

        _clickGuardItem = Item("Click Guard (tembus klik)", (_, _) => ToggleClickGuard());
        _clickGuardItem.CheckOnClick = false;
        _menu.Items.Add(_clickGuardItem);

        _hotkeyItem = Item("Shortcut global (Win+Alt+N)", (_, _) => ToggleGlobalHotkeys());
        _hotkeyItem.CheckOnClick = false;
        _menu.Items.Add(_hotkeyItem);

        _startupItem = Item("Mulai bersama Windows", (_, _) => ToggleStartup());
        _startupItem.CheckOnClick = false;
        _menu.Items.Add(_startupItem);

        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(Item("Pengaturan...", (_, _) => SettingsRequested?.Invoke()));

        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(Item("Keluar", (_, _) => ExitRequested?.Invoke()));

        _menu.Opening += (_, _) =>
        {
            _pinItem.Checked = _isPinned();
            _autoLyricsItem.Checked = _settings.Current.AutoLyricsOnPlay;
            _fullscreenItem.Checked = _settings.Current.FullscreenAutoHide;
            _clickGuardItem.Checked = _settings.Current.ClickGuardEnabled;
            _hotkeyItem.Checked = _settings.Current.EnableGlobalHotkeys;
            _startupItem.Checked = Autostart.IsEnabled();
        };

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = icon,
            Text = "NusaLite",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(IslandView.Player);

        if (_settings.Current.StartWithWindows)
        {
            Autostart.SetEnabled(true);
        }
    }

    public event Action<IslandView>? OpenRequested;

    public event Action? PinToggleRequested;

    public event Action<bool>? AutoLyricsToggled;

    public event Action<bool>? FullscreenAutoHideToggled;

    public event Action<bool>? ClickGuardToggled;

    public event Action<bool>? GlobalHotkeysToggled;

    public event Action? ExitRequested;

    public event Action? SettingsRequested;

    public void SetPinned(bool pinned)
    {
        _pinItem.Checked = pinned;
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = WinForms.ToolTipIcon.None;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private void ToggleAutoLyrics()
    {
        var enabled = !_settings.Current.AutoLyricsOnPlay;
        _settings.Current.AutoLyricsOnPlay = enabled;
        _settings.Save();
        _autoLyricsItem.Checked = enabled;
        AutoLyricsToggled?.Invoke(enabled);
    }

    private void ToggleFullscreenAutoHide()
    {
        var enabled = !_settings.Current.FullscreenAutoHide;
        _settings.Current.FullscreenAutoHide = enabled;
        _settings.Save();
        _fullscreenItem.Checked = enabled;
        FullscreenAutoHideToggled?.Invoke(enabled);
    }

    private void ToggleClickGuard()
    {
        var enabled = !_settings.Current.ClickGuardEnabled;
        _settings.Current.ClickGuardEnabled = enabled;
        _settings.Save();
        _clickGuardItem.Checked = enabled;
        ClickGuardToggled?.Invoke(enabled);
    }

    private void ToggleGlobalHotkeys()
    {
        var enabled = !_settings.Current.EnableGlobalHotkeys;
        _settings.Current.EnableGlobalHotkeys = enabled;
        _settings.Save();
        _hotkeyItem.Checked = enabled;
        GlobalHotkeysToggled?.Invoke(enabled);
    }

    private void ToggleStartup()
    {
        var enabled = !Autostart.IsEnabled();
        Autostart.SetEnabled(enabled);
        _settings.Current.StartWithWindows = enabled;
        _settings.Save();
        _startupItem.Checked = enabled;
    }

    private static WinForms.ToolStripMenuItem Item(string text, EventHandler onClick)
    {
        var item = new WinForms.ToolStripMenuItem(text)
        {
            ForeColor = Drawing.Color.FromArgb(236, 239, 247),
            Padding = new WinForms.Padding(4, 3, 4, 3)
        };
        item.Click += onClick;
        return item;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _iconStream?.Dispose();
    }
}

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NusaLite";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var value = key?.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null)
            {
                return;
            }

            if (enable)
            {
                var exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                key.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            Infrastructure.Log.Write("Autostart setting could not be updated.", ex);
        }
    }
}

internal sealed class DarkColorTable : WinForms.ProfessionalColorTable
{
    public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(40, 44, 58);
    public override Drawing.Color MenuItemBorder => Drawing.Color.Transparent;
    public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(34, 38, 52);
    public override Drawing.Color MenuItemSelectedGradientBegin => Drawing.Color.FromArgb(34, 38, 52);
    public override Drawing.Color MenuItemSelectedGradientEnd => Drawing.Color.FromArgb(34, 38, 52);
    public override Drawing.Color MenuStripGradientBegin => Drawing.Color.FromArgb(16, 18, 26);
    public override Drawing.Color MenuStripGradientEnd => Drawing.Color.FromArgb(16, 18, 26);
    public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(16, 18, 26);
    public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(36, 40, 52);
    public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(36, 40, 52);
    public override Drawing.Color CheckBackground => Drawing.Color.FromArgb(50, 60, 85);
    public override Drawing.Color CheckSelectedBackground => Drawing.Color.FromArgb(60, 72, 100);
    public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(16, 18, 26);
    public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(16, 18, 26);
}
