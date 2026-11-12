using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class LyricsView : UserControl
{
    private MainViewModel? _vm;
    private bool? _compact;

    public LyricsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
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
        var artSize = compact ? 40.0 : 56.0;
        ArtColumn.Width = compact ? 46 : 66;
        ArtBox.Width = artSize;
        ArtBox.Height = artSize;

        var radius = new CornerRadius(compact ? 12 : 16);
        ArtPlaceholder.CornerRadius = radius;
        ArtImage.CornerRadius = radius;
        ArtOutline.CornerRadius = radius;

        TitleLine.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        PrevLine.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        NextLine.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

        Divider.Margin = compact ? new Thickness(0, 2, 10, 2) : new Thickness(0, 2, 12, 2);
        LyricHost.Margin = compact ? new Thickness(12, 0, 0, 0) : new Thickness(14, 0, 0, 0);

        CurrentLine.FontSize = compact ? 13.5 : 15.5;
        CurrentLine.LineHeight = compact ? 17 : 19;
        CurrentLine.Margin = compact ? new Thickness(0, 2, 0, 2) : new Thickness(0, 3, 0, 3);
    }
}
