using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NusaLite.Models;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class IdleView : UserControl
{
    private const double TickerFadeWidth = 18.0;
    private const double TickerMaxWidth = 320.0;
    private const double SourceIconWidth = 14.0;
    private const double SourceIconGap = 8.0;

    private readonly LinearGradientBrush _tickerFade;
    private readonly GradientStop _fadeSolid;
    private readonly GradientStop _fadeClear;
    private MainViewModel? _viewModel;

    public static readonly DependencyProperty ShowAltHintProperty = DependencyProperty.Register(
        nameof(ShowAltHint), typeof(bool), typeof(IdleView), new PropertyMetadata(false));

    public bool ShowAltHint
    {
        get => (bool)GetValue(ShowAltHintProperty);
        set => SetValue(ShowAltHintProperty, value);
    }

    public IdleView()
    {
        InitializeComponent();

        _fadeSolid = new GradientStop(Color.FromArgb(0xFF, 0, 0, 0), 0.88);
        _fadeClear = new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1.0);
        _tickerFade = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        _tickerFade.GradientStops.Add(_fadeSolid);
        _tickerFade.GradientStops.Add(_fadeClear);

        TickerZone.SizeChanged += (_, _) => UpdateTickerLayout();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as MainViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateTickerLayout();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.PomodoroActive)
            or nameof(MainViewModel.HasSourceIcon)
            or nameof(MainViewModel.PomodoroTime)
            or nameof(MainViewModel.PomodoroIsBreak))
        {
            UpdateTickerLayout();
        }
    }

    private void UpdateTickerLayout()
    {
        if (_viewModel is not { PomodoroActive: true })
        {
            Grid.SetColumnSpan(TickerZone, 2);
            Grid.SetColumn(TrailingZone, 0);
            Grid.SetColumnSpan(TrailingZone, 2);
            TickerZone.OpacityMask = null;
            ContentGroup.Margin = new Thickness(0);
            IdleTicker.MaxWidth = TickerMaxWidth;
            return;
        }

        Grid.SetColumnSpan(TickerZone, 1);
        Grid.SetColumn(TrailingZone, 1);
        Grid.SetColumnSpan(TrailingZone, 1);
        ContentGroup.Margin = new Thickness(0, 0, TickerFadeWidth, 0);
        TickerZone.OpacityMask = _tickerFade;

        var width = TickerZone.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var fade = Math.Min(TickerFadeWidth, width);
        _fadeSolid.Offset = (width - fade) / width;
        _fadeClear.Offset = 1;

        var icon = SourceIcon.Visibility == Visibility.Visible
            ? (SourceIcon.ActualWidth > 0 ? SourceIcon.ActualWidth : SourceIconWidth) + SourceIconGap
            : 0;
        var available = width - fade - icon;
        IdleTicker.MaxWidth = Math.Clamp(available, 0, TickerMaxWidth);
    }

    private void FocusChip_Click(object sender, RoutedEventArgs e) =>
        (DataContext as MainViewModel)?.RequestView(IslandView.Focus);
}
