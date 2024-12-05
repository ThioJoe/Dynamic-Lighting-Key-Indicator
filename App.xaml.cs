using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using System;
using Microsoft.UI;

namespace Dynamic_Lighting_Key_Indicator
{
    public partial class App : Application
    {
        private Window? m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Create and activate window first
            m_window = new MainWindow();

            // Initialize ProtocolMessage before activation
            if (m_window != null)
            {
                ProtocolMessage.Initialize(m_window);
            }

            // Now activate the window
            m_window.Activate();

            // Initialize URL handler after window is active
            URLHandler.Initialize();

            // Process any command line arguments
            string arguments = args.Arguments;
            if (!string.IsNullOrEmpty(arguments))
            {
                ProcessArguments(arguments);
            }

            // Handle window resizing
            if (m_window != null)
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(m_window);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.Resize(new SizeInt32(1200, 1200));
            }
        }

        private void ProcessArguments(string arguments)
        {
            string[] argsArray = arguments.Split(' ');
            foreach (string arg in argsArray)
            {
                // Nothing here yet
            }
        }
    }
}