// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Global suppressions
[assembly: SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "Allow use of .ToList(), etc.")]
[assembly: SuppressMessage("Style", "IDE0090:Use 'new(...)'", Justification = "<Pending>")]

// Specific suppressions
[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "<Pending>", Scope = "namespace", Target = "~N:Dynamic_Lighting_Key_Indicator")]
[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "<Pending>", Scope = "namespace", Target = "~N:Dynamic_Lighting_Key_Indicator.Utils")]
[assembly: SuppressMessage("Style", "IDE0028:Simplify collection initialization", Justification = "<Pending>", Scope = "member", Target = "~M:Dynamic_Lighting_Key_Indicator.ColorSetter.BuildMonitoredKeyIndicesDict(Windows.Devices.Lights.LampArray)")]
[assembly: SuppressMessage("Style", "IDE0090:Use 'new(...)'", Justification = "<Pending>", Scope = "member", Target = "~M:Dynamic_Lighting_Key_Indicator.MainWindow.AttachToDevice_Async(Windows.Devices.Enumeration.DeviceInformation)~System.Threading.Tasks.Task{Dynamic_Lighting_Key_Indicator.MainWindow.LampArrayInfo}")]
[assembly: SuppressMessage("Style", "IDE0017:Simplify object initialization", Justification = "It's just one line", Scope = "member", Target = "~M:Dynamic_Lighting_Key_Indicator.Logging.GetOrCreateFileStream(System.String)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "It's clearer this way", Scope = "member", Target = "~M:Dynamic_Lighting_Key_Indicator.MainViewModel.StaticUpdateLastKnownKeyState(Dynamic_Lighting_Key_Indicator.MonitoredKey)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Needs to be not static to use in Xaml binding", Scope = "member", Target = "~P:Dynamic_Lighting_Key_Indicator.MainWindow.DefaultFontColor")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Needs to be not static to use in Xaml binding", Scope = "member", Target = "~P:Dynamic_Lighting_Key_Indicator.MainViewModel.DebugMode_VisibilityBool")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "It's a test function that changes so remove static warning", Scope = "member", Target = "~M:Dynamic_Lighting_Key_Indicator.MainWindow.TestButton_Click(System.Object,System.Object)")]
