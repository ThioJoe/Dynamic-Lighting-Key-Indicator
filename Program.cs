using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

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

            // If this isn't the main instance and it's a protocol activation
            if (!mainInstance.IsCurrent && activatedEventArgs?.Kind == ExtendedActivationKind.Protocol)
            {
                Debug.WriteLine("Secondary instance with protocol - sending via WM");

                var protocolArgs = activatedEventArgs.Data as IProtocolActivatedEventArgs;
                if (protocolArgs?.Uri != null)
                {
                    // Send the protocol data via Windows Message
                    ProtocolMessage.SendProtocolData(protocolArgs.Uri.ToString());
                }

                // Redirect activation (for window focus)
                Task.Run(() => mainInstance.RedirectActivationToAsync(activatedEventArgs)).Wait();
                return 0;
            }

            // Main instance
            Application.Start((p) =>
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