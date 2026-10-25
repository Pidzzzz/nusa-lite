using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class LyricsView : UserControl
{
    private MainViewModel? _vm;

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
}
