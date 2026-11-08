using System.Globalization;
using System.Threading;
using System.Windows;

namespace NusaLite;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\NusaLite.SingleInstance";
    private const string WakeEventName = @"Local\NusaLite.WakeEvent";

    private Mutex? _mutex;
    private EventWaitHandle? _wakeEvent;
    private RegisteredWaitHandle? _registeredWait;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                using var wakeHandle = EventWaitHandle.OpenExisting(WakeEventName);
                wakeHandle.Set();
            }
            catch
            {
                // Ignore if wake event couldn't be opened
            }

            Shutdown();
            return;
        }

        try
        {
            _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                _wakeEvent,
                OnWakeSignaled,
                null,
                -1,
                false);
        }
        catch (Exception ex)
        {
            Infrastructure.Log.Write("Could not register single-instance wake event.", ex);
        }

        var culture = CultureInfo.GetCultureInfo("id-ID");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Infrastructure.Log.Write("Unhandled UI exception.", args.Exception);
            args.Handled = true;
        };

        _host = new AppHost();
        _host.Start();
    }

    private void OnWakeSignaled(object? state, bool timedOut)
    {
        _host?.Wake();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _registeredWait?.Unregister(null);
        _wakeEvent?.Dispose();
        _host?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
