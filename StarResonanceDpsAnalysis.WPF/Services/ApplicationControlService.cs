using System.Windows;

namespace StarResonanceDpsAnalysis.WPF.Services;

public class ApplicationControlService : IApplicationControlService
{
    public void Shutdown()
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            app.Shutdown();
            return;
        }

        app.Dispatcher.Invoke(app.Shutdown);
    }
}