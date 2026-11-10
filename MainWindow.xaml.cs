using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NusaLite.Interop;
using NusaLite.Models;
using NusaLite.ViewModels;
using NusaLite.Views;

namespace NusaLite;

public partial class MainWindow : Window
{
    private static readonly IslandView[] Carousel =
    {
        IslandView.Player,
        IslandView.Lyrics,
        IslandView.Focus,
        IslandView.Water,
        IslandView.Shelf
    };

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _peekTimer;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _clickTimer;
    private readonly DispatcherTimer _fullscreenTimer;
    private readonly DispatcherTimer _ghostTimer;
    private readonly Dictionary<IslandView, UserControl> _viewCache = new();
    private readonly List<Border> _dots = new();
    private readonly SolidColorBrush _dotIdleBrush = new(Color.FromArgb(140, 255, 255, 255));
    private readonly FontFamily _uiFont;
    private readonly FontFamily _displayFont;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _hotkeysRegistered;
    private bool _isFullscreenSuppressed;
    private bool _autoLyricsActive;
    private IslandView _view = IslandView.Idle;
    private IslandView _viewBeforePeek = IslandView.Idle;
    private IslandView _viewBeforeDrag = IslandView.Hub;
    private bool _hover;
    private bool _dwellPending;
    private bool _clickGuard;
    private bool _autoGuarded;
    private bool _ghostFaded;
    private IslandView _viewBeforeGuard = IslandView.Idle;
    private DispatcherTimer? _dwellTimer;
    private bool _peekActive;
    private bool _dragActive;
    private bool _dotsVisible;
    private bool _singleClickPending;
    private bool _suppressClickUp;
    private int _transition;
    private IslandSpec _spec = new(198, 34, 12, 0);
    private bool _browserForeground;
    private bool _browserArmed;
    private long _insideSince;
    private const double CompactUiScale = 0.86;
    private const int BrowserArmHoldMs = 140;
    private const int BrowserHintDwellMs = 420;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        _uiFont = TryFindResource("UiFont") as FontFamily ?? new FontFamily("Segoe UI");
        _displayFont = TryFindResource("DisplayFont") as FontFamily ?? _uiFont;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1150) };
        _collapseTimer.Tick += CollapseTimer_Tick;

        _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _peekTimer.Tick += PeekTimer_Tick;

        _hoverTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };
        _hoverTimer.Tick += HoverTimer_Tick;
        _hoverTimer.Start();

        _clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _clickTimer.Tick += ClickTimer_Tick;

        _fullscreenTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _fullscreenTimer.Tick += FullscreenTimer_Tick;
        _fullscreenTimer.Start();

        _ghostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _ghostTimer.Tick += GhostTimer_Tick;

        _viewModel.PeekRequested += OnPeekRequested;
        _viewModel.ViewRequested += OnViewRequested;
        _viewModel.PinRequested += OnPinRequested;
        _viewModel.AutoLyricsRequested += OnAutoLyricsRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        IslandHost.SizeChanged += (_, _) => UpdateWindowRegion();
        DotsBar.SizeChanged += (_, _) => UpdateWindowRegion();
        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            RepositionWindow();
            UpdateWindowRegion();
        }));
        Closed += (_, _) =>
        {
            _hoverTimer.Stop();
            _dwellTimer?.Stop();
            _clickTimer.Stop();
            _fullscreenTimer.Stop();
            _ghostTimer.Stop();
            UnregisterHotkeys();
            _hwndSource?.RemoveHook(HwndHook);
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    public void OpenView(IslandView view)
    {
        if (_isFullscreenSuppressed)
        {
            _isFullscreenSuppressed = false;
            Visibility = Visibility.Visible;
            RepositionWindow();
            UpdateWindowRegion();
        }

        if (!IsVisible)
        {
            Show();
        }

        _viewModel.IsPinned = true;
        SwitchTo(view, force: true, explicitRequest: true);
    }

    public void ToggleIsland()
    {
        if (_isFullscreenSuppressed)
        {
            _isFullscreenSuppressed = false;
            Visibility = Visibility.Visible;
            RepositionWindow();
            UpdateWindowRegion();
        }

        if (!IsVisible)
        {
            Show();
        }

        if (_view == IslandView.Idle)
        {
            _viewModel.IsPinned = true;
            SwitchTo(_viewModel.IsPlaying && _viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Hub, force: true);
        }
        else
        {
            _autoLyricsActive = false;
            _viewModel.IsPinned = false;
            SwitchTo(IslandView.Idle, force: true);
        }
    }

    public void SetAutoLyricsEnabled(bool enabled)
    {
        if (!enabled && _autoLyricsActive)
        {
            _autoLyricsActive = false;
            if (!_viewModel.IsPinned && !_hover)
            {
                SwitchTo(IslandView.Idle);
            }
        }
        else if (enabled && _viewModel.IsPlaying && _viewModel.HasLyrics)
        {
            _autoLyricsActive = true;
            SwitchTo(IslandView.Lyrics);
        }
    }

    private void OnAutoLyricsRequested(bool playing)
    {
        if (!_viewModel.Settings.Current.AutoLyricsOnPlay || _isFullscreenSuppressed)
        {
            return;
        }

        if (playing)
        {
            if (_view is IslandView.Focus or IslandView.Shelf)
            {
                return;
            }

            if (!IsVisible)
            {
                Show();
            }

            _autoLyricsActive = true;
            SwitchTo(IslandView.Lyrics);
        }
        else
        {
            if (_autoLyricsActive)
            {
                _autoLyricsActive = false;
                if (!_viewModel.IsPinned && !_hover)
                {
                    SwitchTo(IslandView.Idle);
                }
            }
        }
    }

    public void WakeIsland()
    {
        if (_isFullscreenSuppressed)
        {
            _isFullscreenSuppressed = false;
            Visibility = Visibility.Visible;
            RepositionWindow();
            UpdateWindowRegion();
        }

        if (!IsVisible)
        {
            Show();
        }

        if (_view == IslandView.Idle)
        {
            SwitchTo(IslandView.Hub, force: true);
        }
        else
        {
            SwitchTo(_view, force: true);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyler.MakeOverlay(_hwnd);
        RepositionWindow();

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(HwndHook);

        _browserForeground = WindowStyler.IsForegroundBrowser(_hwnd) ?? false;
        UpdateClickThrough();
        RegisterHotkeys();
    }

    private void RepositionWindow()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        WindowStyler.PositionTopCenter(
            _hwnd,
            ActualWidth > 0 ? ActualWidth : Width,
            ActualHeight > 0 ? ActualHeight : Height);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RepositionWindow));
        BuildDots();
        ContentHost.Content = GetView(IslandView.Idle);
        SetImmediate(ResolveSpec(IslandView.Idle));
        SetDotsVisible(false, fast: true);

        if (_viewModel.IsPinned)
        {
            SwitchTo(IslandView.Hub, force: true);
        }

        UpdateWindowRegion();
    }

    // ----- Views ---------------------------------------------------------

    private void SwitchTo(IslandView view, bool fast = false, bool force = false, bool explicitRequest = false)
    {
        if (_clickGuard && !explicitRequest &&
            view != IslandView.Idle && view != IslandView.Peek)
        {
            return;
        }

        if (_peekActive && view != IslandView.Peek)
        {
            _peekActive = false;
            _peekTimer.Stop();
        }

        if (!force && _view == view)
        {
            return;
        }

        _view = view;
        UpdateDots();
        SetDotsVisible(view is not (IslandView.Idle or IslandView.Peek), fast);

        if (_hwnd != IntPtr.Zero)
        {
            _browserForeground = WindowStyler.IsForegroundBrowser(_hwnd) ?? _browserForeground;
        }

        AnimateTo(ResolveSpec(view), fast);
        UpdateClickThrough();
    }

    private void AnimateTo(IslandSpec spec, bool fast)
    {
        var generation = ++_transition;
        var fadeOutMs = fast ? 60 : 90;
        var fadeInMs = fast ? 180 : 260;
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

        var fadeOut = new DoubleAnimation(
            ContentHost.Opacity,
            0,
            TimeSpan.FromMilliseconds(fadeOutMs))
        {
            EasingFunction = easeIn
        };

        var slideOut = new DoubleAnimation(
            ContentSlide.Y,
            -4,
            TimeSpan.FromMilliseconds(fadeOutMs))
        {
            EasingFunction = easeIn,
            FillBehavior = FillBehavior.Stop
        };

        fadeOut.Completed += (_, _) =>
        {
            if (generation != _transition)
            {
                return;
            }

            ContentHost.BeginAnimation(OpacityProperty, null);
            ContentHost.Opacity = 0;
            ContentSlide.BeginAnimation(TranslateTransform.YProperty, null);
            ContentSlide.Y = 7;
            ContentHost.Content = GetView(_view);
            ApplySpec(spec, fast);

            var beginTime = TimeSpan.FromMilliseconds(fast ? 20 : 60);
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeInMs))
            {
                BeginTime = beginTime,
                EasingFunction = easeOut
            };
            ContentHost.BeginAnimation(OpacityProperty, fadeIn);

            var slideIn = new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(fast ? 220 : 320))
            {
                BeginTime = beginTime,
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            ContentSlide.BeginAnimation(TranslateTransform.YProperty, slideIn);
        };

        ContentHost.BeginAnimation(OpacityProperty, fadeOut);
        ContentSlide.BeginAnimation(TranslateTransform.YProperty, slideOut);
    }

    private void ApplySpec(IslandSpec spec, bool fast)
    {
        if (!IsLoaded)
        {
            SetImmediate(spec);
            return;
        }

        ApplyContentScale();

        var generation = _transition;
        var duration = TimeSpan.FromMilliseconds(fast ? 190 : 320);
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

        var width = IslandHost.ActualWidth > 0 ? IslandHost.ActualWidth : spec.Width;
        var height = IslandHost.ActualHeight > 0 ? IslandHost.ActualHeight : spec.Height;
        var radius = Notch.CornerRadius;
        var ear = Notch.EarRadius;

        IslandHost.Width = spec.Width;
        IslandHost.Height = spec.Height;
        Notch.CornerRadius = spec.Radius;
        Notch.EarRadius = spec.Ear;

        IslandHost.BeginAnimation(WidthProperty, Animate(width, spec.Width, duration, ease, generation));
        IslandHost.BeginAnimation(HeightProperty, Animate(height, spec.Height, duration, ease, generation));
        Notch.BeginAnimation(Controls.NotchShape.CornerRadiusProperty, Animate(radius, spec.Radius, duration, ease, generation));
        Notch.BeginAnimation(Controls.NotchShape.EarRadiusProperty, Animate(ear, spec.Ear, duration, ease, generation));

        _spec = spec;
        ApplyActiveViewLayout(spec);
    }

    private void ApplyActiveViewLayout(IslandSpec spec)
    {
        if (ContentHost.Content is LyricsView lyrics)
        {
            lyrics.ApplyLayout(_browserForeground && _view == IslandView.Lyrics, spec.Width, spec.Height);
        }
        else if (ContentHost.Content is IdleView idle)
        {
            var scale = _browserForeground ? CompactUiScale : 1.0;
            idle.Width = spec.Width / scale;
            idle.Height = spec.Height / scale;
        }
    }

    private void ApplyContentScale()
    {
        var scale = _view == IslandView.Lyrics || !_browserForeground ? 1.0 : CompactUiScale;
        ContentHost.LayoutTransform = Math.Abs(scale - 1.0) < 0.001
            ? Transform.Identity
            : new ScaleTransform(scale, scale);
    }

    private void SetImmediate(IslandSpec spec)
    {
        IslandHost.BeginAnimation(WidthProperty, null);
        IslandHost.BeginAnimation(HeightProperty, null);
        Notch.BeginAnimation(Controls.NotchShape.CornerRadiusProperty, null);
        Notch.BeginAnimation(Controls.NotchShape.EarRadiusProperty, null);

        ApplyContentScale();

        IslandHost.Width = spec.Width;
        IslandHost.Height = spec.Height;
        Notch.CornerRadius = spec.Radius;
        Notch.EarRadius = spec.Ear;

        _spec = spec;
        ApplyActiveViewLayout(spec);
    }

    private DoubleAnimation Animate(
        double from, double to, TimeSpan duration, IEasingFunction ease, int generation)
    {
        var animation = new DoubleAnimation(from, to, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            if (generation == _transition)
            {
                UpdateWindowRegion();
            }
        };

        return animation;
    }

    private UserControl GetView(IslandView view)
    {
        if (_viewCache.TryGetValue(view, out var cached))
        {
            return cached;
        }

        UserControl created = view switch
        {
            IslandView.Idle => new IdleView(),
            IslandView.Hub => new HubView(),
            IslandView.Player => new PlayerView(),
            IslandView.Lyrics => new LyricsView(),
            IslandView.Focus => new FocusView(),
            IslandView.Water => new WaterView(),
            IslandView.Shelf => new ShelfView(),
            _ => new PeekView()
        };

        _viewCache[view] = created;
        return created;
    }

    private IslandSpec ResolveSpec(IslandView view)
    {
        if (view == IslandView.Lyrics)
        {
            return BuildLyricsSpec(_browserForeground);
        }

        var spec = view == IslandView.Idle ? BuildIdleSpec() : SpecFor(view);
        if (!_browserForeground)
        {
            return spec;
        }

        return new IslandSpec(
            Math.Ceiling(spec.Width * CompactUiScale),
            Math.Ceiling(spec.Height * CompactUiScale),
            Math.Ceiling(spec.Radius * CompactUiScale),
            spec.Ear);
    }

    private IslandSpec BuildIdleSpec()
    {
        var widest = MeasureTextWidth(_viewModel.IdleLine, _uiFont, 13.5, FontWeights.Medium);
        var width = Math.Ceiling((widest + 88 + (_viewModel.HasSourceIcon ? 24 : 0)) / 12) * 12;
        width = Math.Clamp(width, 216, 400);
        return new IslandSpec(width, 40, 20, 0);
    }

    private IslandSpec BuildLyricsSpec(bool compact)
    {        var widest = MeasureTextWidth(_viewModel.LyricCurrent, _displayFont, compact ? 13.5 : 15.5, FontWeights.SemiBold);

        if (compact)
        {
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.TrackArtist, _uiFont, 8.5, FontWeights.Normal));
        }
        else
        {
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.LyricPrev, _uiFont, 11, FontWeights.Normal));
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.LyricNext, _uiFont, 11, FontWeights.Normal));
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.TrackTitle, _displayFont, 9.5, FontWeights.SemiBold));
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.TrackArtist, _uiFont, 8.5, FontWeights.Normal));
        }

        var fixedWidth = compact ? 97.0 : 121.0;
        var min = compact ? 292.0 : 360.0;
        var max = compact ? 430.0 : 560.0;
        var width = Math.Ceiling((fixedWidth + widest + 10) / 24) * 24;
        width = Math.Clamp(width, min, max);
        var height = compact ? 76.0 : 108.0;
        var radius = compact ? 16.0 : 20.0;

        return new IslandSpec(width, height, radius, 0);
    }

    private double MeasureTextWidth(string? text, FontFamily font, double fontSize, FontWeight weight)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var probe = new TextBlock
        {
            Text = text,
            FontFamily = font,
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None
        };

        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_clickGuard && _view != IslandView.Idle && _view != IslandView.Peek &&
            e.PropertyName is nameof(MainViewModel.TrackTitle) or nameof(MainViewModel.TrackArtist)
                or nameof(MainViewModel.HasMedia) or nameof(MainViewModel.HasLyrics)
                or nameof(MainViewModel.LyricCurrent) or nameof(MainViewModel.IdleLine))
        {
            SwitchTo(IslandView.Idle, force: true);
            return;
        }

        if (_view == IslandView.Idle)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IdleLine):
                case nameof(MainViewModel.TrackTitle):
                case nameof(MainViewModel.HasMedia):
                    UpdateIdleLayout();
                    break;
            }

            return;
        }

        if (_view != IslandView.Lyrics)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.LyricCurrent):
            case nameof(MainViewModel.LyricPrev):
            case nameof(MainViewModel.LyricNext):
            case nameof(MainViewModel.HasLyrics):
            case nameof(MainViewModel.TrackTitle):
            case nameof(MainViewModel.TrackArtist):
                UpdateLyricsLayout();
                break;
        }
    }

    private void UpdateIdleLayout()
    {
        if (_view != IslandView.Idle || _hwnd == IntPtr.Zero || !IsVisible)
        {
            return;
        }

        var spec = ResolveSpec(IslandView.Idle);

        if (Math.Abs(spec.Width - _spec.Width) < 0.5 &&
            Math.Abs(spec.Height - _spec.Height) < 0.5)
        {
            return;
        }

        ApplySpec(spec, fast: false);
    }

    private void UpdateLyricsLayout()
    {
        if (_view != IslandView.Lyrics || _hwnd == IntPtr.Zero || !IsVisible)
        {
            return;
        }

        var compact = _browserForeground;
        var spec = BuildLyricsSpec(compact);

        if (Math.Abs(spec.Width - _spec.Width) < 0.5 &&
            Math.Abs(spec.Height - _spec.Height) < 0.5)
        {
            return;
        }

        ApplySpec(spec, fast: false);
    }

    private static IslandSpec SpecFor(IslandView view) => view switch
    {
        IslandView.Idle => new IslandSpec(198, 34, 12, 0),
        IslandView.Hub => new IslandSpec(410, 88, 18, 0),
        IslandView.Lyrics => new IslandSpec(360, 108, 20, 0),
        IslandView.Peek => new IslandSpec(362, 68, 16, 0),
        _ => new IslandSpec(554, 158, 22, 0)
    };

    // ----- Pointer -------------------------------------------------------

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        UpdateGhostByCursor();

        var inside = IsCursorInside();

        if (!inside)
        {
            _insideSince = 0;
            if (_hover)
            {
                PointerLeave();
            }
            return;
        }

        if (_hover)
        {
            return;
        }

        if (_insideSince == 0)
        {
            _insideSince = Environment.TickCount64;
        }

        var dwell = _browserForeground ? 220 : 0;
        if (Environment.TickCount64 - _insideSince >= dwell)
        {
            PointerEnter();
        }
    }

    private bool IsCursorInside()
    {
        return IsCursorWithinRect(2);
    }

    private bool IsCursorNearIsland()
    {
        return IsCursorWithinRect(24);
    }

    private bool IsCursorWithinRect(int margin)
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.GetCursorPos(out var cursor))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return false;
        }

        var scale = NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

        if (IsInside(cursor, IslandHost, window, scale, margin))
        {
            return true;
        }

        return _dotsVisible && DotsBar.ActualWidth > 0 && IsInside(cursor, DotsBar, window, scale, margin);
    }

    private bool IsInside(NativeMethods.Point cursor, FrameworkElement element, NativeMethods.Rect window, double scale, int margin = 2)
    {
        var origin = element.TranslatePoint(new Point(0, 0), Root);
        var left = window.Left + origin.X * scale;
        var top = window.Top + origin.Y * scale;
        var right = left + element.ActualWidth * scale;
        var bottom = top + element.ActualHeight * scale;

        return cursor.X >= left - margin && cursor.X <= right + margin && cursor.Y >= top - margin && cursor.Y <= bottom + margin;
    }

    private void PointerEnter()
    {
        if (IsEffectiveGuard)
        {
            return;
        }

        _collapseTimer.Stop();
        if (_hover)
        {
            return;
        }

        var delay = _viewModel.Settings.Current.HoverDelayMs;
        if (delay <= 0)
        {
            if (_dwellPending)
            {
                _dwellPending = false;
                _dwellTimer?.Stop();
            }

            ExpandFromHover();
            return;
        }

        if (_dwellPending)
        {
            return;
        }

        _dwellPending = true;
        _dwellTimer ??= CreateDwellTimer();
        _dwellTimer.Stop();
        _dwellTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _dwellTimer.Start();
    }

    private DispatcherTimer CreateDwellTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Input);
        timer.Tick += DwellTimer_Tick;
        return timer;
    }

    private void DwellTimer_Tick(object? sender, EventArgs e)
    {
        _dwellTimer?.Stop();
        _dwellPending = false;

        if (IsEffectiveGuard || _hover || !IsCursorInside())
        {
            return;
        }

        ExpandFromHover();
    }

    private void ExpandFromHover()
    {
        _collapseTimer.Stop();
        if (IsEffectiveGuard || _hover)
        {
            return;
        }

        _hover = true;
        UpdateClickThrough();

        if (_peekActive)
        {
            _peekActive = false;
            _peekTimer.Stop();
            SwitchTo(_viewModel.IsPinned ? _viewBeforePeek : IslandView.Hub, force: true);
            return;
        }

        if (_view == IslandView.Idle)
        {
            SwitchTo(IslandView.Hub);
        }
    }

    private void PointerLeave()
    {
        if (_dwellPending)
        {
            _dwellPending = false;
            _dwellTimer?.Stop();
        }

        _hover = false;
        UpdateClickThrough();
        if (_viewModel.IsPinned || _dragActive)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (_hover || _viewModel.IsPinned)
        {
            return;
        }

        if (IsCursorInside())
        {
            return;
        }

        SwitchTo(IslandView.Idle);
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            _suppressClickUp = false;
        }

        if (e.ClickCount == 2)
        {
            _clickTimer.Stop();
            _singleClickPending = false;
            _suppressClickUp = true;
            _viewModel.IsPinned = !_viewModel.IsPinned;
            e.Handled = true;
        }
    }

    private void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressClickUp)
        {
            _suppressClickUp = false;
            return;
        }

        if (e.ClickCount > 1)
        {
            return;
        }

        _singleClickPending = true;
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void ClickTimer_Tick(object? sender, EventArgs e)
    {
        _clickTimer.Stop();
        if (!_singleClickPending)
        {
            return;
        }

        _singleClickPending = false;

        if (_view == IslandView.Hub)
        {
            SwitchTo(_viewModel.IsPlaying && _viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Player);
        }
        else if (_view == IslandView.Idle)
        {
            SwitchTo(_viewModel.IsPlaying && _viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Hub);
        }
        else if (_view == IslandView.Lyrics)
        {
            SwitchTo(IslandView.Player);
        }
        else if (_view == IslandView.Player)
        {
            SwitchTo(_viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Hub);
        }
    }

    private void Island_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var direction = e.Delta > 0 ? -1 : 1;
        var index = Array.IndexOf(Carousel, _view);

        IslandView target;
        if (index < 0)
        {
            target = direction > 0 ? Carousel[0] : Carousel[^1];
        }
        else
        {
            target = Carousel[(index + direction + Carousel.Length) % Carousel.Length];
        }

        SwitchTo(target);
        e.Handled = true;
    }

    // ----- Peek / pin ----------------------------------------------------

    private void OnPinRequested(bool pinned)
    {
        if (pinned)
        {
            _collapseTimer.Stop();
            if (_view is IslandView.Idle or IslandView.Peek)
            {
                SwitchTo(IslandView.Hub, force: true);
            }
        }
        else if (!_hover)
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private void OnViewRequested(IslandView view)
    {
        if (view == IslandView.Idle)
        {
            _viewModel.IsPinned = false;
            SwitchTo(IslandView.Idle, force: true, explicitRequest: true);
            return;
        }

        _viewModel.IsPinned = true;
        SwitchTo(view, force: true, explicitRequest: true);
    }

    // ----- Window region -------------------------------------------------

    private void UpdateWindowRegion()
    {
        if (_hwnd == IntPtr.Zero || !IsLoaded)
        {
            return;
        }

        var scale = NativeMethods.GetDpiForWindow(_hwnd) / 96.0;
        var origin = IslandHost.TranslatePoint(new Point(0, 0), Root);
        var width = (int)Math.Ceiling(IslandHost.ActualWidth * scale);
        var height = (int)Math.Ceiling(IslandHost.ActualHeight * scale);
        var x = (int)Math.Round(origin.X * scale);
        var y = (int)Math.Round(origin.Y * scale);

        if (width < 4 || height < 4)
        {
            return;
        }

        var ear = (int)Math.Round(Notch.EarRadius * scale);
        var radius = (int)Math.Round(Notch.CornerRadius * scale);

        var region = NativeMethods.CreateRoundRectRgn(
            x + ear, y, x + width - ear + 1, y + height + 1, radius * 2, radius * 2);
        var topHeight = ear > 0 ? ear : radius;
        var top = NativeMethods.CreateRectRgn(x, y, x + width + 1, y + topHeight + 1);
        NativeMethods.CombineRgn(region, region, top, NativeMethods.RgnOr);
        NativeMethods.DeleteObject(top);

        if (_dotsVisible && DotsBar.ActualWidth > 0)
        {
            var dotsOrigin = DotsBar.TranslatePoint(new Point(0, 0), Root);
            var dotsWidth = (int)Math.Ceiling(DotsBar.ActualWidth * scale);
            var dotsHeight = (int)Math.Ceiling(DotsBar.ActualHeight * scale);
            var dotsX = (int)Math.Round(dotsOrigin.X * scale);
            var dotsY = (int)Math.Round(dotsOrigin.Y * scale);

            var dotsRegion = NativeMethods.CreateRoundRectRgn(
                dotsX, dotsY, dotsX + dotsWidth + 1, dotsY + dotsHeight + 1, dotsHeight, dotsHeight);
            NativeMethods.CombineRgn(region, region, dotsRegion, NativeMethods.RgnOr);
            NativeMethods.DeleteObject(dotsRegion);
        }

        if (NativeMethods.SetWindowRgn(_hwnd, region, true) == 0)
        {
            NativeMethods.DeleteObject(region);
        }
    }

    private readonly record struct IslandSpec(double Width, double Height, double Radius, double Ear);
}
