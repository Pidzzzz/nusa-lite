using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class PlayerView : UserControl
{
    private bool _seeking;

    public PlayerView()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void Play_Click(object sender, RoutedEventArgs e) => Vm?.TogglePlay();

    private void Next_Click(object sender, RoutedEventArgs e) => Vm?.NextTrack();

    private void Previous_Click(object sender, RoutedEventArgs e) => Vm?.PreviousTrack();

    private void Mute_Click(object sender, RoutedEventArgs e) => Vm?.ToggleMute();

    private void SeekArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _seeking = true;
        SeekArea.CaptureMouse();
        ApplySeek(e.GetPosition(SeekArea).X);
        e.Handled = true;
    }

    private void SeekArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_seeking && e.LeftButton == MouseButtonState.Pressed)
        {
            ApplySeek(e.GetPosition(SeekArea).X);
        }
    }

    private void SeekArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_seeking)
        {
            return;
        }

        ApplySeek(e.GetPosition(SeekArea).X);
        _seeking = false;
        SeekArea.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void ApplySeek(double x)
    {
        var width = SeekArea.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        Vm?.SeekToFraction(Math.Clamp(x / width, 0, 1));
    }
}
