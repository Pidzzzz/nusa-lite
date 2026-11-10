using NusaLite.Interop;
using NusaLite.Models;

namespace NusaLite;

public partial class MainWindow
{
    private const int HotkeyToggleId = 9001;
    private const int HotkeyPlayerId = 9002;
    private const int HotkeyShelfId = 9003;
    private const int HotkeyLyricsId = 9004;
    private const int HotkeyFocusId = 9005;
    private const int HotkeyClickGuardId = 9006;

    public void SetGlobalHotkeysEnabled(bool enabled)
    {
        if (enabled)
        {
            RegisterHotkeys();
        }
        else
        {
            UnregisterHotkeys();
        }
    }

    private void RegisterHotkeys()
    {
        if (!_viewModel.Settings.Current.EnableGlobalHotkeys || _hwnd == IntPtr.Zero || _hotkeysRegistered)
        {
            return;
        }

        try
        {
            uint mod = NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
            NativeMethods.RegisterHotKey(_hwnd, HotkeyToggleId, mod, 0x4E); // Win + Alt + N
            NativeMethods.RegisterHotKey(_hwnd, HotkeyPlayerId, mod, 0x50); // Win + Alt + P
            NativeMethods.RegisterHotKey(_hwnd, HotkeyShelfId,  mod, 0x53); // Win + Alt + S
            NativeMethods.RegisterHotKey(_hwnd, HotkeyLyricsId, mod, 0x4C); // Win + Alt + L
            NativeMethods.RegisterHotKey(_hwnd, HotkeyFocusId,  mod, 0x46); // Win + Alt + F
            NativeMethods.RegisterHotKey(_hwnd, HotkeyClickGuardId, mod, 0x47); // Win + Alt + G
            _hotkeysRegistered = true;
        }
        catch (Exception ex)
        {
            Infrastructure.Log.Write("Failed to register global hotkeys.", ex);
        }
    }

    private void UnregisterHotkeys()
    {
        if (!_hotkeysRegistered || _hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyToggleId);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyPlayerId);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyShelfId);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyLyricsId);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyFocusId);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyClickGuardId);
        }
        catch (Exception ex)
        {
            Infrastructure.Log.Write("Failed to unregister global hotkeys.", ex);
        }
        finally
        {
            _hotkeysRegistered = false;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            switch (id)
            {
                case HotkeyToggleId:
                    ToggleIsland();
                    handled = true;
                    break;
                case HotkeyPlayerId:
                    OpenView(IslandView.Player);
                    handled = true;
                    break;
                case HotkeyShelfId:
                    OpenView(IslandView.Shelf);
                    handled = true;
                    break;
                case HotkeyLyricsId:
                    OpenView(IslandView.Lyrics);
                    handled = true;
                    break;
                case HotkeyFocusId:
                    OpenView(IslandView.Focus);
                    handled = true;
                    break;
                case HotkeyClickGuardId:
                    ToggleClickGuard();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }
}
