using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NusaLite.Models;
using NusaLite.ViewModels;

namespace NusaLite.Views;

public partial class ShelfView : UserControl
{
    public ShelfView()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ShelfItem item })
        {
            Vm?.OpenShelfItem(item);
            e.Handled = true;
        }
    }

    private void Card_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ShelfItem item } element || Vm is null)
        {
            return;
        }

        var menu = new ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 20, 28)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 239, 247)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255))
        };

        var open = new MenuItem { Header = "Buka" };
        open.Click += (_, _) => Vm.OpenShelfItem(item);

        var reveal = new MenuItem { Header = "Tampilkan di Explorer" };
        reveal.Click += (_, _) => Vm.RevealShelfItem(item);

        var remove = new MenuItem { Header = "Hapus dari shelf" };
        remove.Click += (_, _) => Vm.RemoveShelfItem(item);

        menu.Items.Add(open);
        menu.Items.Add(reveal);
        menu.Items.Add(new Separator());
        menu.Items.Add(remove);

        menu.PlacementTarget = element;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ShelfItem item })
        {
            Vm?.RemoveShelfItem(item);
            e.Handled = true;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => Vm?.ClearShelf();

    private void Empty_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Tambahkan ke shelf",
            Multiselect = true,
            Filter = "Semua file|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            Vm?.AddShelfFiles(dialog.FileNames);
        }
    }
}
