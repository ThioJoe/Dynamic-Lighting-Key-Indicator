using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

// NOTE: If Visual Studio yells at you about this file already existing, it's because this one is custom to allow a single-instance app.
//    See: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
// You need to disble the auto generation of the default Program.cs code in the project file (this is all described in the link above).

// As far as I'm aware this is the earliest entry point in the app. It calls App.xaml.cs which calls MainWindow.xaml.cs
// We modify this so that when the app is activated by URL protocol, instead of creating a new window, at this early stage we redirect the
//     activation data to the main instance of the app so it can use the URL parameters as commands while its running. Then close the new instance.

#pragma warning disable IDE0060 // Remove unused parameter

namespace Dynamic_Lighting_Key_Indicator
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Add global exception handler first thing
            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                Logging.WriteCrashLog(exception: (Exception)error.ExceptionObject);
            };

            try
            {
                WinRT.ComWrappersSupport.InitializeComWrappers();

                AppInstance currentInstance = AppInstance.GetCurrent();
                AppActivationArguments? activatedEventArgs = currentInstance.GetActivatedEventArgs();
                AppInstance mainInstance = AppInstance.FindOrRegisterForKey("Dynamic_Lighting_Key_Indicator");

                // If this isn't the main instance and it's a protocol activation, redirect it to the main instance.
                // It will be captured by the MainInstance_Activated handler we created in MainWindow.xaml.cs attached to the main instance.
                if (!mainInstance.IsCurrent)
                {
                    // Ensure the protocol activation data is present
                    if (
                        activatedEventArgs != null
                        && activatedEventArgs?.Kind == ExtendedActivationKind.Protocol
                        && activatedEventArgs.Data is IProtocolActivatedEventArgs protocolArgs 
                        && protocolArgs.Uri != null
                    )
                    {
                        try
                        {
                            // Redirect activation (for window focus)
                            Windows.Foundation.IAsyncAction redirectTask = mainInstance.RedirectActivationToAsync(activatedEventArgs);
                            redirectTask.AsTask().Wait(TimeSpan.FromSeconds(7)); // Timeout after 7 seconds (Arbitrarily chosen)
                            return 0;
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteCrashLog(exception: ex);
                            throw; // Re-throw to be caught by outer handler
                        }
                    }
                    else // If it's not a protocol activation, or they aren't valid, just exit because we only want to allow one instance.
                    {
                        Logging.WriteDebug("Non-protocol or invalid protocol activation detected. Exiting.");
                        return 0;
                    }
                }

                // Main instance
                Application.Start((application_Initialization_Callback_Params) =>
                {
                    try
                    {
                        DispatcherQueueSynchronizationContext context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                        SynchronizationContext.SetSynchronizationContext(context);
                        App app = new App();
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteCrashLog(exception: ex);
                        throw; // Re-throw to be caught by outer handler
                    }
                });

                return 0;
            }
            catch (Exception ex)
            {
                // Final catch-all for any unhandled exceptions
                try
                {
                    Logging.WriteCrashLog(exception: ex);
                }
                catch (Exception exOuter)
                {
                    Debug.WriteLine("Error:" + exOuter?.Message);
                }
                return -1; // Return error code
            }
        }
    }
}