using System.Windows;
using NusaLite.Models;

namespace NusaLite;

public partial class MainWindow
{
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
}
