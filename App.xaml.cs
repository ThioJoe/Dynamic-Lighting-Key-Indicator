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
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Create and activate window first
            _ = new MainWindow();

            // Initialize URL handler after window is active
            URLHandler.Initialize();

            // Process any command line arguments
            string arguments = args.Arguments;
            if (!string.IsNullOrEmpty(arguments))
            {
                ProcessArguments(arguments);
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