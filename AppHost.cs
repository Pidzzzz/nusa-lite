using System.Windows;
using NusaLite.Services;
using NusaLite.ViewModels;

namespace NusaLite;

public sealed class AppHost : IDisposable
{
    private SettingsStore? _settings;
    private MediaService? _media;
    private LyricsService? _lyrics;
    private Interop.CoreAudio.AudioService? _audio;
    private PowerService? _power;
    private PomodoroService? _pomodoro;
    private WaterService? _water;
    private ShelfService? _shelf;
    private TrayService? _tray;
    private MainViewModel? _viewModel;
    private MainWindow? _window;

    public void Start()
    {
        try
        {
            _settings = new SettingsStore();
            _media = new MediaService();
            _lyrics = new LyricsService();
            _audio = new Interop.CoreAudio.AudioService();
            _power = new PowerService();
            _pomodoro = new PomodoroService(_settings);
            _water = new WaterService(_settings);
            _shelf = new ShelfService();

            _viewModel = new MainViewModel(_settings, _media, _lyrics, _audio, _power, _pomodoro, _water, _shelf);

            _tray = new TrayService(_settings, () => _viewModel.IsPinned);
            _tray.OpenRequested += view => _window?.OpenView(view);
            _tray.PinToggleRequested += () => _viewModel.IsPinned = !_viewModel.IsPinned;
            _tray.ExitRequested += () => Application.Current.Shutdown();

            _viewModel.NotifyRequested += (title, message) => _tray?.ShowNotification(title, message);
            _viewModel.PinRequested += pinned => _tray?.SetPinned(pinned);

            _window = new MainWindow(_viewModel);
            _window.Show();

            _ = _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Infrastructure.Log.Write("NusaLite failed to start.", ex);
            MessageBox.Show(ex.Message, "NusaLite", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    public void Dispose()
    {
        _tray?.Dispose();
        _viewModel?.Dispose();
    }
}
