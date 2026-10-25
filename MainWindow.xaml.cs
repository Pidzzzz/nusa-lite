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
    private readonly Dictionary<IslandView, UserControl> _viewCache = new();
    private readonly List<Border> _dots = new();
    private readonly SolidColorBrush _dotIdleBrush = new(Color.FromArgb(140, 255, 255, 255));

    private IntPtr _hwnd;
    private IslandView _view = IslandView.Idle;
    private IslandView _viewBeforePeek = IslandView.Idle;
    private IslandView _viewBeforeDrag = IslandView.Hub;
    private bool _hover;
    private bool _peekActive;
    private bool _dragActive;
    private bool _dotsVisible;
    private bool _singleClickPending;
    private bool _suppressClickUp;
    private int _transition;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

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

        _viewModel.PeekRequested += OnPeekRequested;
        _viewModel.ViewRequested += OnViewRequested;
        _viewModel.PinRequested += OnPinRequested;

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
            _clickTimer.Stop();
        };
    }

    public void OpenView(IslandView view)
    {
        if (!IsVisible)
        {
            Show();
        }

        _viewModel.IsPinned = true;
        SwitchTo(view, force: true);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyler.MakeOverlay(_hwnd);
        RepositionWindow();
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
        SetImmediate(SpecFor(IslandView.Idle));
        SetDotsVisible(false, fast: true);

        if (_viewModel.IsPinned)
        {
            SwitchTo(IslandView.Hub, force: true);
        }

        UpdateWindowRegion();
    }

    // ----- Views ---------------------------------------------------------

    private void SwitchTo(IslandView view, bool fast = false, bool force = false)
    {
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
        AnimateTo(SpecFor(view), fast);
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
    }

    private void SetImmediate(IslandSpec spec)
    {
        IslandHost.BeginAnimation(WidthProperty, null);
        IslandHost.BeginAnimation(HeightProperty, null);
        Notch.BeginAnimation(Controls.NotchShape.CornerRadiusProperty, null);
        Notch.BeginAnimation(Controls.NotchShape.EarRadiusProperty, null);

        IslandHost.Width = spec.Width;
        IslandHost.Height = spec.Height;
        Notch.CornerRadius = spec.Radius;
        Notch.EarRadius = spec.Ear;
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

    private static IslandSpec SpecFor(IslandView view) => view switch
    {
        IslandView.Idle => new IslandSpec(198, 34, 12, 0),
        IslandView.Hub => new IslandSpec(410, 88, 18, 0),
        IslandView.Lyrics => new IslandSpec(602, 158, 22, 0),
        IslandView.Peek => new IslandSpec(362, 68, 16, 0),
        _ => new IslandSpec(554, 158, 22, 0)
    };

    // ----- Dots ----------------------------------------------------------

    private void BuildDots()
    {
        foreach (var view in Carousel)
        {
            var dot = new Border
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = _dotIdleBrush,
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = view,
                ToolTip = LabelFor(view)
            };

            dot.MouseLeftButtonDown += (sender, e) =>
            {
                SwitchTo((IslandView)((Border)sender).Tag);
                e.Handled = true;
            };

            DotsPanel.Children.Add(dot);
            _dots.Add(dot);
        }
    }

    private void UpdateDots()
    {
        foreach (var dot in _dots)
        {
            var dotView = dot.Tag is IslandView view ? view : IslandView.Idle;
            var active = dotView == _view;
            var target = active ? 16.0 : 4.0;
            var from = dot.ActualWidth > 0 ? dot.ActualWidth : target;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            dot.Width = target;
            dot.Background = active ? AccentFor(dotView) : _dotIdleBrush;
            dot.BeginAnimation(WidthProperty, new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            });
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(
                dot.Opacity,
                active ? 1.0 : 0.7,
                TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = ease
            });
        }
    }

    private Brush AccentFor(IslandView view) => view switch
    {
        IslandView.Lyrics => (Brush)FindResource("GoldBrush"),
        IslandView.Focus => (Brush)FindResource("EmberBrush"),
        IslandView.Water => (Brush)FindResource("AquaBrush"),
        IslandView.Shelf => (Brush)FindResource("LimeBrush"),
        _ => (Brush)FindResource("AccentBrush")
    };

    private void SetDotsVisible(bool visible, bool fast)
    {
        if (_dotsVisible != visible)
        {
            _dotsVisible = visible;
            DotsBar.IsHitTestVisible = visible;
            var beginTime = visible ? TimeSpan.FromMilliseconds(fast ? 40 : 190) : TimeSpan.Zero;
            var duration = TimeSpan.FromMilliseconds(visible ? 220 : 140);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fromY = DotsSlide.Y;

            DotsSlide.BeginAnimation(TranslateTransform.YProperty, null);
            if (visible)
            {
                DotsSlide.Y = 4;
                fromY = 4;
            }
            else
            {
                DotsSlide.Y = 0;
            }

            DotsSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(
                fromY,
                visible ? 0 : 4,
                duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            });

            var animation = new DoubleAnimation(visible ? 1 : 0, duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease
            };
            DotsBar.BeginAnimation(OpacityProperty, animation);
            UpdateWindowRegion();
        }
    }

    private static string LabelFor(IslandView view) => view switch
    {
        IslandView.Player => "Pemutar",
        IslandView.Lyrics => "Lirik",
        IslandView.Focus => "Fokus",
        IslandView.Water => "Air",
        _ => "Shelf"
    };

    // ----- Pointer -------------------------------------------------------

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        var inside = IsCursorInside();
        if (inside == _hover)
        {
            return;
        }

        if (inside)
        {
            PointerEnter();
        }
        else
        {
            PointerLeave();
        }
    }

    private bool IsCursorInside()
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

        if (IsInside(cursor, IslandHost, window, scale))
        {
            return true;
        }

        return _dotsVisible && DotsBar.ActualWidth > 0 && IsInside(cursor, DotsBar, window, scale);
    }

    private bool IsInside(NativeMethods.Point cursor, FrameworkElement element, NativeMethods.Rect window, double scale)
    {
        var origin = element.TranslatePoint(new Point(0, 0), Root);
        var left = window.Left + origin.X * scale;
        var top = window.Top + origin.Y * scale;
        var right = left + element.ActualWidth * scale;
        var bottom = top + element.ActualHeight * scale;

        return cursor.X >= left - 2 && cursor.X <= right + 2 && cursor.Y >= top - 2 && cursor.Y <= bottom + 2;
    }

    private void PointerEnter()
    {
        _collapseTimer.Stop();
        if (_hover)
        {
            return;
        }

        _hover = true;

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
        _hover = false;
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
            SwitchTo(IslandView.Player);
        }
        else if (_view == IslandView.Idle)
        {
            SwitchTo(IslandView.Hub);
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

    private void OnPeekRequested(PeekKind kind)
    {
        if (kind == PeekKind.None || _hover || _viewModel.IsPinned || _dragActive)
        {
            return;
        }

        _viewModel.FillPeek(kind);

        if (_peekActive)
        {
            _peekTimer.Stop();
            _peekTimer.Start();
            return;
        }

        _viewBeforePeek = _view;
        _peekActive = true;
        SwitchTo(IslandView.Peek, fast: true, force: true);
        _peekTimer.Stop();
        _peekTimer.Start();
    }

    private void PeekTimer_Tick(object? sender, EventArgs e)
    {
        _peekTimer.Stop();
        if (!_peekActive)
        {
            return;
        }

        _peekActive = false;
        var target = _hover ? IslandView.Hub : _viewBeforePeek;
        if (_viewModel.IsPinned)
        {
            target = _viewBeforePeek;
        }

        SwitchTo(target, force: true);
    }

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
            SwitchTo(IslandView.Idle, force: true);
            return;
        }

        _viewModel.IsPinned = true;
        SwitchTo(view, force: true);
    }

    // ----- Drag and drop -------------------------------------------------

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        _collapseTimer.Stop();

        if (!_dragActive)
        {
            _dragActive = true;
            _viewBeforeDrag = _view;
        }

        _viewModel.ShelfDropHover = true;
        SwitchTo(IslandView.Shelf, fast: true);
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        if (!_dragActive)
        {
            return;
        }

        _dragActive = false;
        _viewModel.ShelfDropHover = false;

        if (_view == IslandView.Shelf)
        {
            if (_viewModel.IsPinned)
            {
                SwitchTo(_viewBeforeDrag == IslandView.Peek ? IslandView.Hub : _viewBeforeDrag, force: true);
            }
            else
            {
                SwitchTo(_hover ? IslandView.Hub : IslandView.Idle);
            }
        }

        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        _dragActive = false;
        _viewModel.ShelfDropHover = false;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _viewModel.AddShelfFiles(files);
            SwitchTo(IslandView.Shelf, force: true);
        }

        e.Handled = true;
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
