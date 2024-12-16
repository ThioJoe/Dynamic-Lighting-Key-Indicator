using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;


#pragma warning disable IDE0060 // Remove unused parameter

namespace Dynamic_Lighting_Key_Indicator
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            var currentInstance = AppInstance.GetCurrent();
            var activatedEventArgs = currentInstance.GetActivatedEventArgs();
            var mainInstance = AppInstance.FindOrRegisterForKey("Dynamic_Lighting_Key_Indicator");

            // If this isn't the main instance and it's a protocol activation, redirect it to the main instance.
            // It will be captured by the MainInstance_Activated handler we created in MainWindow.xaml.cs attached to the main instance.
            if (!mainInstance.IsCurrent && activatedEventArgs?.Kind == ExtendedActivationKind.Protocol)
            {
                // Redirect activation (for window focus)
                Task.Run(() => mainInstance.RedirectActivationToAsync(activatedEventArgs)).Wait();

                return 0;
            }

            // Main instance
            Application.Start((application_Initialization_Callback_Params) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);

                var app = new App();

                // Handle direct protocol activation if present
                if (activatedEventArgs?.Kind == ExtendedActivationKind.Protocol)
                {
                    var protocolArgs = activatedEventArgs.Data as IProtocolActivatedEventArgs;
                    if (protocolArgs?.Uri != null)
                    {
                        URLHandler.ProcessUri(protocolArgs.Uri);
                    }
                }
            });

            return 0;
        }
    }
}