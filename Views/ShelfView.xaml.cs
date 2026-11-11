using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        var panel = TryFindResource("PanelBrush") as Brush;
        var line = TryFindResource("LineBrush") as Brush;
        var ink = TryFindResource("InkBrush") as Brush;
        var hover = TryFindResource("FillMediumBrush") as Brush;

        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        if (ink is not null)
        {
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
        }
        if (hover is not null)
        {
            var highlighted = new Trigger
            {
                Property = MenuItem.IsHighlightedProperty,
                Value = true
            };
            highlighted.Setters.Add(new Setter(Control.BackgroundProperty, hover));
            itemStyle.Triggers.Add(highlighted);
        }

        var separatorStyle = new Style(typeof(Separator));
        if (line is not null)
        {
            separatorStyle.Setters.Add(new Setter(Control.BackgroundProperty, line));
            separatorStyle.Setters.Add(new Setter(Control.BorderBrushProperty, line));
        }

        var menu = new ContextMenu
        {
            Background = panel,
            Foreground = ink,
            BorderBrush = line
        };

        var open = new MenuItem { Header = "Buka", Style = itemStyle };
        open.Click += (_, _) => Vm.OpenShelfItem(item);

        var reveal = new MenuItem { Header = "Tampilkan di Explorer", Style = itemStyle };
        reveal.Click += (_, _) => Vm.RevealShelfItem(item);

        var remove = new MenuItem { Header = "Hapus dari shelf", Style = itemStyle };
        remove.Click += (_, _) => Vm.RemoveShelfItem(item);

        menu.Items.Add(open);
        menu.Items.Add(reveal);
        menu.Items.Add(new Separator { Style = separatorStyle });
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
