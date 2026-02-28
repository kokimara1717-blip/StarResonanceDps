using System.Windows.Threading;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public class BaseDispatcherSupportViewModel(Dispatcher dispatcher): BaseViewModel
{
    protected void InvokeOnDispatcher(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}