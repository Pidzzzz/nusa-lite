using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class LyricsView : UserControl
{
    private const bool LyricsActiveGlowEnabled = false;

    private MainViewModel? _vm;
    private bool? _compact;

    public LyricsView()
    {
        InitializeComponent();
        ApplyActiveGlow();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private void ApplyActiveGlow()
    {
        if (LyricsActiveGlowEnabled)
        {
            ActiveGlow.Visibility = Visibility.Visible;
            ActiveGlow.Background = (System.Windows.Media.Brush)FindResource("LyricsGlowFillBrush");
            ActiveGlow.BorderBrush = (System.Windows.Media.Brush)FindResource("LyricsGlowBorderBrush");
            ActiveGlow.BorderThickness = new Thickness(1);
            if (ActiveGlow.Effect is null)
            {
                ActiveGlow.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = (System.Windows.Media.Color)FindResource("AccentShadowColor"),
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 0.2
                };
            }

            FadeTop.Visibility = Visibility.Visible;
            FadeBottom.Visibility = Visibility.Visible;
            return;
        }

        ActiveGlow.Visibility = Visibility.Collapsed;
        ActiveGlow.Background = System.Windows.Media.Brushes.Transparent;
        ActiveGlow.BorderBrush = System.Windows.Media.Brushes.Transparent;
        ActiveGlow.BorderThickness = new Thickness(0);
        ActiveGlow.Effect = null;

        FadeTop.Visibility = Visibility.Collapsed;
        FadeBottom.Visibility = Visibility.Collapsed;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        Attach();
    }

    private void Attach()
    {
        if (_vm is not null || DataContext is not MainViewModel vm)
        {
            return;
        }

        _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void Detach()
    {
        if (_vm is null)
        {
            return;
        }

        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MainViewModel.LyricCurrent) or nameof(MainViewModel.HasLyrics)))
        {
            return;
        }

        var fade = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };

        if (CurrentLine.RenderTransform is not System.Windows.Media.TranslateTransform)
        {
            CurrentLine.RenderTransform = new System.Windows.Media.TranslateTransform();
        }

        CurrentLine.BeginAnimation(OpacityProperty, fade);
        ((System.Windows.Media.TranslateTransform)CurrentLine.RenderTransform).BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty, slide);
    }

    private void OffsetMinus_Click(object sender, RoutedEventArgs e) => _vm?.ShiftLyricOffset(-500);

    private void OffsetPlus_Click(object sender, RoutedEventArgs e) => _vm?.ShiftLyricOffset(500);

    public void ApplyLayout(bool compact, double width, double height)
    {
        if (!double.IsNaN(width) && width > 0)
        {
            Width = width;
        }

        if (!double.IsNaN(height) && height > 0)
        {
            Height = height;
        }

        if (_compact == compact)
        {
            return;
        }

        _compact = compact;
        ApplyCompact(compact);
    }

    private void ApplyCompact(bool compact)
    {
        ArtColumn.Width = compact ? 46 : 56;
        ArtBox.Width = 40;
        ArtBox.Height = 40;

        var radius = new CornerRadius(10);
        ArtPlaceholder.CornerRadius = radius;
        ArtImage.CornerRadius = radius;
        ArtOutline.CornerRadius = radius;

        TitleLine.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        PrevLine.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        NextLine.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

        Root.Margin = compact ? new Thickness(16, 8, 16, 8) : new Thickness(16);
        Divider.Margin = compact ? new Thickness(0, 2, 10, 2) : new Thickness(0, 4, 16, 4);
        LyricHost.Margin = compact ? new Thickness(12, 0, 0, 0) : new Thickness(16, 0, 0, 0);

        CurrentLine.FontSize = compact ? 13.5 : 15;
        CurrentLine.LineHeight = compact ? 17 : 19;
        CurrentLine.Margin = compact ? new Thickness(0, 2, 0, 2) : new Thickness(0);
    }
}
