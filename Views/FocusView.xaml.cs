using System.Windows;
using System.Windows.Controls;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class FocusView : UserControl
{
    public FocusView()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void Start_Click(object sender, RoutedEventArgs e) => Vm?.TogglePomodoro();

    private void Reset_Click(object sender, RoutedEventArgs e) => Vm?.ResetPomodoro();

    private void Skip_Click(object sender, RoutedEventArgs e) => Vm?.SkipPomodoro();
}
