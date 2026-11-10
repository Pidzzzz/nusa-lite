using NusaLite.Models;

namespace NusaLite;

public partial class MainWindow
{
    private void OnPeekRequested(PeekKind kind)
    {
        if (kind == PeekKind.None || _hover || _viewModel.IsPinned || _dragActive)
        {
            return;
        }

        _viewModel.FillPeek(kind);

        if (_clickGuard)
        {
            BumpGhost();
        }

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
        UpdateGhostByCursor();
    }
}
