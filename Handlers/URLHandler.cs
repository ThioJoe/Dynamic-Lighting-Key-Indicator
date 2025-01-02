using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Activation;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class URLHandler
    {
        private const string PROTOCOL_NAME = "key-lighting-indicator";  // Your protocol name (e.g., dlki://)
        private static MainWindow? _mainWindow;
        private static UserConfig? _currentConfig;

        public static void Initialize()
        {
            // Register for protocol activation (existing code)
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup();
            RegisterForProtocolActivation();
        }

        private static void RegisterForProtocolActivation()
        {
            try
            {
                Logging.WriteDebug("Registering protocol activation.");

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
                Logging.WriteDebug($"Error in protocol registration: {ex.Message}");
            }
        }

        public static void HandleActivation(AppActivationArguments args)
        {
            Logging.WriteDebug("App activation event triggered.");
            if (args.Kind == ExtendedActivationKind.Protocol)
            {
                if (args.Data is IProtocolActivatedEventArgs protocolArgs)
                {
                    Logging.WriteDebug("URI protocol activation event triggered by url: " + protocolArgs.Uri.ToString());
                    // Process the URI
                    ProcessUri(protocolArgs.Uri);
                }
            }
            else
            {
                Logging.WriteDebug("Unknown activation event. It was not URI protocol activation.");
            }
        }

        //  -----------------------------------------------------------------

        public static void ProvideWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public static readonly List<string> Commands =
        [
            "set",
        ];

        public static class ParameterNames
        {
            // Integer values from 0 to 100
            public const string GlobalBrightness = "global_brightness";
            // Tuples of RGB values comma separated with no spaces
            public const string ScrollLockOnColor = "scrolllockcolor_on";
            public const string ScrollLockOffColor = "scrolllockcolor_off";
            public const string NumLockOnColor = "numlockcolor_on";
            public const string NumLockOffColor = "numlockcolor_off";
            public const string CapsLockOnColor = "capslockcolor_on";
            public const string CapsLockOffColor = "capslockcolor_off";
            public const string StandardKeyColor = "standardkeycolor";
        }

        public static void ProcessUri(Uri uri)
        {
            try
            {
                string currentTime = DateTime.Now.ToString("HH:mm:ss");
                System.Diagnostics.Debug.WriteLine($"Inside ProcessUri at" + currentTime);

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
            _currentConfig = MainWindow.CurrentConfig;
            // Get the various keys from the config
            if (_currentConfig != null && _currentConfig.MonitoredKeysAndColors != null)
            {
                config = _currentConfig;
            }
            else
            {
                config = new UserConfig();
            }

            RGBTuple standardColorFromConfig = config.StandardKeyColor;

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

                RGBTuple? color = null;
                if (ParseColor(value, standardColorFromConfig) is RGBTuple outColor)
                {
                    color = outColor;
                }

                // Update the default linking status if the value is 'default', and unlink if it was previously linked
                if (value.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    if (on)
                        key.onColorTiedToStandard = true;
                    else
                        key.offColorTiedToStandard = true;
                }
                else
                {
                    if (on)
                        key.onColorTiedToStandard = false;
                    else
                        key.offColorTiedToStandard = false;
                }

                // Set the actual color
                if (color != null)
                {
                    if (on)
                        key.onColor = (RGBTuple)color;
                    else
                        key.offColor = (RGBTuple)color;

                    if (addKeyToConfig)
                    {
                        config.MonitoredKeysAndColors.Add(key);
                    }
                }


            }
            // --------------------------------------

            // First check for global brightness and process that first
            int setGlobalBrightness = -1;
            if (queryParams[ParameterNames.GlobalBrightness] is string globalBrightness)
            {
                int? brightness = ParseBrightness(globalBrightness);
                if (brightness.HasValue)
                {
                    setGlobalBrightness = brightness.Value;
                }
                // Remove the parameter from the collection
                queryParams.Remove(ParameterNames.GlobalBrightness);
            }

            // Then check for standard / default key color
            if (queryParams[ParameterNames.StandardKeyColor] is string standardColor)
            {
                if (ParseColor(standardColor, standardColorFromConfig) is RGBTuple color)
                    config.StandardKeyColor = color;
                // Remove the parameter from the collection
                queryParams.Remove(ParameterNames.StandardKeyColor);
            }

            foreach (string? paramKey in queryParams.AllKeys)
            {
                if (paramKey == null || queryParams[paramKey] is not string value)
                    continue;

                // Don't skip if value is empty, might add parameter-less commands in the future

                switch (paramKey.ToLower())
                {
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
                }

            } // End foreach loop

            // Update the global brightness
            if (setGlobalBrightness >= 0)
            {
                config.UpdateGlobalBrightness(setGlobalBrightness);
            }

            // Apply
            _mainWindow.ApplyAndSaveColorSettings(saveFile: false, newConfig: config);
        }

        private static int? ParseBrightness(string brightness)
        {
            if (int.TryParse(brightness, out int result))
            {
                if (result < 0)
                    return 0;
                else if (result > 100)
                    return 100;
                else
                    return result;
            }

            // If can't be parsed or is out of range, return null
            return null;
        }

        private static RGBTuple? ParseColor(string color, RGBTuple standardColor)
        {
            // If the string says 'default'
            if (color.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                return standardColor;
            }

            // Require a string in the format "R,G,B" (3 values)
            string[] rgb = color.Split(',');
            if (rgb.Length != 3)
                return null;

            if (int.TryParse(rgb[0], out int r) && int.TryParse(rgb[1], out int g) && int.TryParse(rgb[2], out int b))
            {
                return (r, g, b);
            }

            // If fail to parse, return null
            return null;
        }

        private static void Cleanup()
        {
            // Perform any necessary cleanup
        }

    }


}