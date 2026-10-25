using System.Globalization;
using System.Threading;
using System.Windows;

namespace NusaLite;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, @"Local\NusaLite.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
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

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
