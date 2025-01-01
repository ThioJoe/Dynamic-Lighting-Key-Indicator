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
[assembly: SuppressMessage("Style", "IDE0028:Simplify collection initialization", Justification = "<Pending>", Scope = "member", Target = "~M:Dynamic_Lighting_Key_Indicator.MainWindow.FindKeyboardLampArrayDevices~System.Threading.Tasks.Task{System.ComponentModel.BindingList{Windows.Devices.Enumeration.DeviceInformation}}")]


