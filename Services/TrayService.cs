using System.IO;
using Microsoft.Win32;
using NusaLite.Models;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
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
    private string? _appliedTheme;

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

        ApplyMenuTheme();

        _menu.Opening += (_, _) =>
        {
            ApplyMenuTheme();
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

        ThemeService.ThemeChanged += OnThemeChanged;
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

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyMenuTheme();
    }

    private void ApplyMenuTheme()
    {
        var theme = ThemeService.Current;
        if (string.Equals(theme, _appliedTheme, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _appliedTheme = theme;
        var palette = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? TrayThemePalettes.CreateLight()
            : TrayThemePalettes.Dark;

        _menu.BackColor = palette.Background;
        _menu.ForeColor = palette.Foreground;
        _menu.Renderer = new TrayRenderer(palette);

        foreach (WinForms.ToolStripItem item in _menu.Items)
        {
            if (item is WinForms.ToolStripMenuItem menuItem)
            {
                menuItem.ForeColor = palette.Foreground;
            }
        }

        _menu.Invalidate();
    }

    private static WinForms.ToolStripMenuItem Item(string text, EventHandler onClick)
    {
        var item = new WinForms.ToolStripMenuItem(text)
        {
            Padding = new WinForms.Padding(4, 3, 4, 3)
        };
        item.Click += onClick;
        return item;
    }

    public void Dispose()
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
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

internal sealed class DarkColorTable : TrayColorTable
{
    public DarkColorTable()
        : base(TrayThemePalettes.Dark)
    {
    }
}

internal sealed record TrayPalette(
    bool IsLight,
    Drawing.Color Background,
    Drawing.Color Foreground,
    Drawing.Color DisabledForeground,
    Drawing.Color Border,
    Drawing.Color SelectionBorder,
    Drawing.Color Hover,
    Drawing.Color Separator,
    Drawing.Color CheckBackground,
    Drawing.Color CheckSelectedBackground,
    Drawing.Color CheckMark);

internal static class TrayThemePalettes
{
    public static TrayPalette Dark { get; } = new(
        false,
        Drawing.Color.FromArgb(16, 18, 26),
        Drawing.Color.FromArgb(236, 239, 247),
        Drawing.SystemColors.GrayText,
        Drawing.Color.FromArgb(40, 44, 58),
        Drawing.Color.FromArgb(0x7C, 0x5C, 0xFF),
        Drawing.Color.FromArgb(0x2B, 0x25, 0x54),
        Drawing.Color.FromArgb(36, 40, 52),
        Drawing.Color.FromArgb(0x26, 0x21, 0x48),
        Drawing.Color.FromArgb(0x3B, 0x30, 0x76),
        Drawing.Color.White);

    public static TrayPalette CreateLight()
    {
        var background = ResourceColor("IslandColor", Drawing.Color.FromArgb(0xF3, 0xF1, 0xF8));
        var foreground = ResourceColor("InkColor", Drawing.Color.FromArgb(0x1B, 0x17, 0x26));
        var border = Blend(ResourceColor("LineStrongColor", Drawing.Color.FromArgb(0x33, 0, 0, 0)), background);
        var separator = Blend(ResourceColor("LineSoft1FColor", Drawing.Color.FromArgb(0x1F, 0, 0, 0)), background);
        var hoverTint = ResourceColor("AccentTint2EColor", Drawing.Color.FromArgb(0x2E, 0x6A, 0x5A, 0xE0));
        var hover = Blend(hoverTint, background);
        var check = Blend(ResourceColor("AccentTint1EColor", Drawing.Color.FromArgb(0x1E, 0x6A, 0x5A, 0xE0)), background);
        var checkSelected = Blend(ResourceColor("AccentTint40Color", Drawing.Color.FromArgb(0x40, 0x6A, 0x5A, 0xE0)), background);
        var disabled = Blend(ResourceColor("TextGhost59Color", Drawing.Color.FromArgb(0x7A, 0, 0, 0)), background);
        var checkMark = ResourceColor("PrimaryColor", Drawing.Color.FromArgb(0x6A, 0x43, 0xC4));
        var selectionBorder = Drawing.Color.FromArgb(255, hoverTint.R, hoverTint.G, hoverTint.B);

        return new TrayPalette(
            true,
            background,
            foreground,
            disabled,
            border,
            selectionBorder,
            hover,
            separator,
            check,
            checkSelected,
            checkMark);
    }

    private static Drawing.Color ResourceColor(string key, Drawing.Color fallback)
    {
        var resource = System.Windows.Application.Current?.TryFindResource(key);
        return resource switch
        {
            System.Windows.Media.Color color => Drawing.Color.FromArgb(color.A, color.R, color.G, color.B),
            System.Windows.Media.SolidColorBrush brush => Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B),
            _ => fallback
        };
    }

    private static Drawing.Color Blend(Drawing.Color overlay, Drawing.Color background)
    {
        if (overlay.A >= 255)
        {
            return Drawing.Color.FromArgb(255, overlay.R, overlay.G, overlay.B);
        }

        var alpha = overlay.A / 255.0;
        return Drawing.Color.FromArgb(
            255,
            (int)Math.Round(overlay.R * alpha + background.R * (1 - alpha)),
            (int)Math.Round(overlay.G * alpha + background.G * (1 - alpha)),
            (int)Math.Round(overlay.B * alpha + background.B * (1 - alpha)));
    }
}

internal class TrayColorTable : WinForms.ProfessionalColorTable
{
    protected TrayPalette Palette { get; }

    public TrayColorTable(TrayPalette palette)
    {
        Palette = palette;
        UseSystemColors = false;
    }

    public override Drawing.Color MenuBorder => Palette.Border;
    public override Drawing.Color MenuItemBorder => Palette.SelectionBorder;
    public override Drawing.Color MenuItemSelected => Palette.Hover;
    public override Drawing.Color MenuItemSelectedGradientBegin => Palette.Hover;
    public override Drawing.Color MenuItemSelectedGradientEnd => Palette.Hover;
    public override Drawing.Color MenuItemPressedGradientBegin => Palette.Hover;
    public override Drawing.Color MenuItemPressedGradientMiddle => Palette.Hover;
    public override Drawing.Color MenuItemPressedGradientEnd => Palette.Hover;
    public override Drawing.Color MenuStripGradientBegin => Palette.Background;
    public override Drawing.Color MenuStripGradientEnd => Palette.Background;
    public override Drawing.Color ToolStripDropDownBackground => Palette.Background;
    public override Drawing.Color SeparatorDark => Palette.Separator;
    public override Drawing.Color SeparatorLight => Palette.Separator;
    public override Drawing.Color CheckBackground => Palette.CheckBackground;
    public override Drawing.Color CheckSelectedBackground => Palette.CheckSelectedBackground;
    public override Drawing.Color CheckPressedBackground => Palette.CheckSelectedBackground;
    public override Drawing.Color ImageMarginGradientBegin => Palette.Background;
    public override Drawing.Color ImageMarginGradientEnd => Palette.Background;
    public override Drawing.Color ButtonSelectedHighlight => Palette.Hover;
    public override Drawing.Color ButtonSelectedHighlightBorder => Palette.SelectionBorder;
    public override Drawing.Color ButtonSelectedBorder => Palette.SelectionBorder;
    public override Drawing.Color ButtonSelectedGradientBegin => Palette.Hover;
    public override Drawing.Color ButtonSelectedGradientMiddle => Palette.Hover;
    public override Drawing.Color ButtonSelectedGradientEnd => Palette.Hover;
    public override Drawing.Color ButtonPressedHighlight => Palette.Hover;
    public override Drawing.Color ButtonPressedHighlightBorder => Palette.SelectionBorder;
    public override Drawing.Color ButtonPressedBorder => Palette.SelectionBorder;
    public override Drawing.Color ButtonPressedGradientBegin => Palette.Hover;
    public override Drawing.Color ButtonPressedGradientMiddle => Palette.Hover;
    public override Drawing.Color ButtonPressedGradientEnd => Palette.Hover;
    public override Drawing.Color ButtonCheckedHighlight => Palette.CheckSelectedBackground;
    public override Drawing.Color ButtonCheckedHighlightBorder => Palette.SelectionBorder;
    public override Drawing.Color ButtonCheckedGradientBegin => Palette.CheckSelectedBackground;
    public override Drawing.Color ButtonCheckedGradientMiddle => Palette.CheckSelectedBackground;
    public override Drawing.Color ButtonCheckedGradientEnd => Palette.CheckSelectedBackground;
}

internal sealed class LightColorTable : TrayColorTable
{
    public LightColorTable(TrayPalette palette)
        : base(palette)
    {
    }

    public override Drawing.Color ToolStripBorder => Palette.Border;
    public override Drawing.Color ImageMarginGradientMiddle => Palette.Background;
}

internal sealed class TrayRenderer : WinForms.ToolStripProfessionalRenderer
{
    private readonly TrayPalette _palette;

    public TrayRenderer(TrayPalette palette)
        : base(palette.IsLight ? new LightColorTable(palette) : new DarkColorTable())
    {
        _palette = palette;
    }

    protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
    {
        if (_palette.IsLight && !e.Item.Enabled)
        {
            e.TextColor = _palette.DisabledForeground;
        }

        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(WinForms.ToolStripItemImageRenderEventArgs e)
    {
        if (!_palette.IsLight)
        {
            base.OnRenderItemCheck(e);
            return;
        }

        var rect = e.ImageRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var selected = e.Item.Selected || e.Item.Pressed;
        using (var background = new Drawing.SolidBrush(selected ? _palette.CheckSelectedBackground : _palette.CheckBackground))
        {
            e.Graphics.FillRectangle(background, rect);
        }

        using (var border = new Drawing.Pen(_palette.SelectionBorder))
        {
            e.Graphics.DrawRectangle(border, rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
        }

        var previous = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        try
        {
            using var check = new Drawing.Pen(_palette.CheckMark, Math.Max(1.5f, rect.Height / 8f))
            {
                StartCap = Drawing2D.LineCap.Round,
                EndCap = Drawing2D.LineCap.Round,
                LineJoin = Drawing2D.LineJoin.Round
            };
            e.Graphics.DrawLines(check, new[]
            {
                new Drawing.PointF(rect.Left + rect.Width * 0.24f, rect.Top + rect.Height * 0.52f),
                new Drawing.PointF(rect.Left + rect.Width * 0.43f, rect.Top + rect.Height * 0.70f),
                new Drawing.PointF(rect.Left + rect.Width * 0.78f, rect.Top + rect.Height * 0.30f)
            });
        }
        finally
        {
            e.Graphics.SmoothingMode = previous;
        }
    }
}
