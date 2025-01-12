using Microsoft.UI.Xaml;

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
            // Process any command line arguments
            string[] argsArray = [];
            if (!string.IsNullOrEmpty(args.Arguments))
            {
                argsArray = args.Arguments.Split(' ');
            }

            // Create and activate window first
            _ = new MainWindow(argsArray);

            // Initialize URL handler after window is active
            URLHandler.Initialize();
        }
    }
}