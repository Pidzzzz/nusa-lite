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

        _startupItem = Item("Mulai bersama Windows", (_, _) => ToggleStartup());
        _startupItem.CheckOnClick = false;
        _menu.Items.Add(_startupItem);

        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(Item("Keluar", (_, _) => ExitRequested?.Invoke()));

        _menu.Opening += (_, _) =>
        {
            _pinItem.Checked = _isPinned();
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

    public event Action? ExitRequested;

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
        _iconStream = null;
    }

    private sealed class DarkColorTable : WinForms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(16, 18, 26);

        public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(16, 18, 26);

        public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(16, 18, 26);

        public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(16, 18, 26);

        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(42, 46, 60);

        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(45, 91, 255);

        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(38, 44, 62);

        public override Drawing.Color MenuItemSelectedGradientBegin => Drawing.Color.FromArgb(38, 44, 62);

        public override Drawing.Color MenuItemSelectedGradientEnd => Drawing.Color.FromArgb(38, 44, 62);

        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(40, 44, 58);

        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(40, 44, 58);
    }
}

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NusaLite";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                key.SetValue(ValueName, $"\"{path}\" --background");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
