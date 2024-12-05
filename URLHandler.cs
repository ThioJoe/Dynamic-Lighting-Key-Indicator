using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace Dynamic_Lighting_Key_Indicator
{
    using static Dynamic_Lighting_Key_Indicator.KeyStatesHandler;
    using VK = KeyStatesHandler.ToggleAbleKeys;
    using RGBTuple = (int R, int G, int B);

    internal static class URLHandler
    {
        private const string PROTOCOL_NAME = "key-lighting-indicator";  // Your protocol name (e.g., dlki://)
        private static MainWindow? _mainWindow;
        private static UserConfig? _currentConfig;

        public static void Initialize()
        {
            // Get the current instance
            //var instance = AppInstance.GetCurrent();
            //// Register for activation redirection
            //AppInstance.FindOrRegisterForKey("mainInstance");

            //// If this isn't the main instance, redirect and exit
            //if (!instance.IsCurrent)
            //{
            //    // Redirect activation to the main instance
            //    var mainInstance = AppInstance.GetInstances()[0];
            //    var args = mainInstance.GetActivatedEventArgs();
            //    instance.RedirectActivationToAsync(args).GetAwaiter().GetResult();
            //    Process.GetCurrentProcess().Kill();
            //    return;
            //}

            // Register for protocol activation (existing code)
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup();
            RegisterForProtocolActivation();
        }

        private static void RegisterForProtocolActivation()
        {
            try
            {
                // Debug that received command
                System.Diagnostics.Debug.WriteLine($"Here1");
                // Get the current activation arguments
                var args = AppInstance.GetCurrent().GetActivatedEventArgs();
                if (args != null)
                {
                    // Handle the protocol activation
                    HandleActivation(args);
                }
            }
            catch (Exception ex)
            {
                // Handle any initialization errors
                System.Diagnostics.Debug.WriteLine($"Error in protocol registration: {ex.Message}");
            }
        }

        public static void HandleActivation(AppActivationArguments args)
        {
            if (args.Kind == ExtendedActivationKind.Protocol)
            {
                var protocolArgs = args.Data as IProtocolActivatedEventArgs;
                if (protocolArgs != null)
                {
                    // Process the URI
                    ProcessUri(protocolArgs.Uri);
                }
            }
        }

        //  -----------------------------------------------------------------

        public static void ProvideUserConfig(UserConfig config)
        {
            _currentConfig = config;
        }

        public static void ProvideWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public static readonly List<string> Commands = new List<string>
            {
                "set",
            };

        public static class ParameterNames
        {
            // Integer values from 0 to 100
            public const string GlobalBrightness = "global_brightness";
            // Tuples of RGB values comma separated with no spaces
            public const string ScrollLockOnColor = "scrollLockOnColor";
            public const string ScrollLockOffColor = "scrollLockOffColor";
            public const string NumLockOnColor = "numLockOnColor";
            public const string NumLockOffColor = "numLockOffColor";
            public const string CapsLockOnColor = "capsLockOnColor";
            public const string CapsLockOffColor = "capsLockOffColor";
            public const string StandardKeyColor = "standardKeyColor";
        }

        private static void ProcessUri(Uri uri)
        {
            try
            {
                // Example URI format: key-lighting-indicator://whateverCommand?param1=value1&param2=value2
                if (uri.Scheme.Equals(PROTOCOL_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the command from the URI
                    string command = uri.Host;

                    // Debug that received command
                    System.Diagnostics.Debug.WriteLine($"Received Command: {command}");

                    if (!Commands.Contains(command))
                    {
                        System.Diagnostics.Debug.WriteLine($"Unknown command: {command}");
                        return;
                    }

                    // Parse query parameters
                    System.Collections.Specialized.NameValueCollection queryParams;
                    queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                    // Handle different commands
                    switch (command.ToLower())
                    {
                        case "set":
                            HandleSetCommand(queryParams);
                            break;

                        // Add more command handlers as needed
                        default:
                            System.Diagnostics.Debug.WriteLine($"Unknown command: {command}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing URI: {ex.Message}");
            }
        }



        // Example command handlers
        private static void HandleSetCommand(System.Collections.Specialized.NameValueCollection queryParams)
        {
            if (_mainWindow == null)
            {
                System.Diagnostics.Debug.WriteLine("Main window not available");
                return;
            }

            UserConfig config;
            // Get the various keys from the config
            if (_currentConfig != null && _currentConfig.MonitoredKeysAndColors != null)
            {
                config = _currentConfig;
            }
            else
            {
                config = new UserConfig();
            }

            // ---------- Local Function -----------------
            void SetKeyColor(ToggleAbleKeys key_vk, bool on, string value)
            {
                MonitoredKey? key = config.GetMonitoredKey(key_vk);
                bool addKeyToConfig = false;

                if (key == null)
                {
                    key = new MonitoredKey(key_vk);
                    addKeyToConfig = true;
                }

                if (ParseColor(value) is RGBTuple color)
                {
                    if (on)
                        key.onColor = color;
                    else
                        key.offColor = color;

                    if (addKeyToConfig)
                    {
                        config.MonitoredKeysAndColors.Add(key);
                    }
                }
            }
            // --------------------------------------
            int setGlobalBrightness = -1;

            foreach (string? paramKey in queryParams.AllKeys)
            {
                if (queryParams[paramKey] is not string value)
                    continue;

                switch (paramKey)
                {
                    case ParameterNames.GlobalBrightness:
                        int? brightness = ParseBrightness(value);
                        if (brightness.HasValue)
                        {
                            setGlobalBrightness = brightness.Value;
                        }
                        break;

                    case ParameterNames.ScrollLockOnColor:
                        SetKeyColor(key_vk: VK.ScrollLock, on: true, value: value);
                        break;
                    case ParameterNames.ScrollLockOffColor:
                        SetKeyColor(key_vk: VK.ScrollLock, on: false, value: value);
                        break;
                    case ParameterNames.NumLockOnColor:
                        SetKeyColor(key_vk: VK.NumLock, on: true, value: value);
                        break;
                    case ParameterNames.NumLockOffColor:
                        SetKeyColor(key_vk: VK.NumLock, on: false, value: value);
                        break;
                    case ParameterNames.CapsLockOnColor:
                        SetKeyColor(key_vk: VK.CapsLock, on: true, value: value);
                        break;
                    case ParameterNames.CapsLockOffColor:
                        SetKeyColor(key_vk: VK.CapsLock, on: false, value: value);
                        break;
                    case ParameterNames.StandardKeyColor:
                        if (ParseColor(value) is RGBTuple color)
                            config.StandardKeyColor = color;
                        break;
                }
            } // End foreach loop

            // Update the global brightness
            if (setGlobalBrightness >= 0)
            {
                config.UpdateBrightnessForAllKeys(setGlobalBrightness);
            }

            // Apply
            _mainWindow.ApplyAndSaveSettings(saveFile: false, newConfig: config);
        }

        private static int? ParseBrightness(string brightness)
        {
            if (int.TryParse(brightness, out int result))
            {
                if (result < 0)
                    return 0;
                if (result > 100)
                    return 100;
            }

            // If can't be parsed or is out of range, return null
            return null;
        }

        private static RGBTuple? ParseColor(string color)
        {
            // Require a string in the format "R,G,B" (3 values)
            string[] rgb = color.Split(',');
            if (rgb.Length != 3)
                return null;

            if (int.TryParse(rgb[0], out int r) && int.TryParse(rgb[1], out int g) && int.TryParse(rgb[2], out int b))
            {
                return (r, g, b);
            }

            // If fail to parse, return null
            return (0, 0, 0);
        }

        private static void Cleanup()
        {
            // Perform any necessary cleanup
        }
    }
}