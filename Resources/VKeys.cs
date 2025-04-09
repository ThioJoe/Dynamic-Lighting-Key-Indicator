using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using WinRT;

namespace Dynamic_Lighting_Key_Indicator;
//
// Summary: Extended version of the VirtualKey enumeration that includes additional keys and buttons.
//     
[WindowsRuntimeType("Windows.Foundation.UniversalApiContract")]
[SupportedOSPlatform("Windows10.0.10240.0")]
[ContractVersion(typeof(UniversalApiContract), 65536u)]
public enum VKey: Int32
{
    //
    // Summary:
    //     No virtual key value.
    None = 0,
    //
    // Summary:
    //     The left mouse button.
    LeftButton = 1,
    //
    // Summary:
    //     The right mouse button.
    RightButton = 2,
    //
    // Summary:
    //     The cancel key or button
    Cancel = 3,
    //
    // Summary:
    //     The middle mouse button.
    MiddleButton = 4,
    //
    // Summary:
    //     An additional "extended" device key or button (for example, an additional mouse
    //     button).
    XButton1 = 5,
    //
    // Summary:
    //     An additional "extended" device key or button (for example, an additional mouse
    //     button).
    XButton2 = 6,
    //
    // Summary:
    //     The virtual back key or button.
    Back = 8,
    //
    // Summary:
    //     The Tab key.
    Tab = 9,
    //
    // Summary:
    //     The Clear key or button.
    Clear = 12,
    //
    // Summary:
    //     The Enter key.
    Enter = 13,
    //
    // Summary:
    //     The Shift key. This is the general Shift case, applicable to key layouts with
    //     only one Shift key or that do not need to differentiate between left Shift and
    //     right Shift keystrokes.
    Shift = 16,
    //
    // Summary:
    //     The Ctrl key. This is the general Ctrl case, applicable to key layouts with only
    //     one Ctrl key or that do not need to differentiate between left Ctrl and right
    //     Ctrl keystrokes.
    Control = 17,
    //
    // Summary:
    //     The menu key or button.
    Menu = 18,
    //
    // Summary:
    //     The Pause key or button.
    Pause = 19,
    //
    // Summary:
    //     The Caps Lock key or button.
    CapitalLock = 20,
    //
    // Summary:
    //     The Kana symbol key-shift button
    Kana = 21,
    //
    // Summary:
    //     The Hangul symbol key-shift button.
    Hangul = 21,
    ImeOn = 22,
    //
    // Summary:
    //     The Junja symbol key-shift button.
    Junja = 23,
    //
    // Summary:
    //     The Final symbol key-shift button.
    Final = 24,
    //
    // Summary:
    //     The Hanja symbol key shift button.
    Hanja = 25,
    //
    // Summary:
    //     The Kanji symbol key-shift button.
    Kanji = 25,
    ImeOff = 26,
    //
    // Summary:
    //     The Esc key.
    Escape = 27,
    //
    // Summary:
    //     The convert button or key.
    Convert = 28,
    //
    // Summary:
    //     The nonconvert button or key.
    NonConvert = 29,
    //
    // Summary:
    //     The accept button or key.
    Accept = 30,
    //
    // Summary:
    //     The mode change key.
    ModeChange = 31,
    //
    // Summary:
    //     The Spacebar key or button.
    Space = 32,
    //
    // Summary:
    //     The Page Up key.
    PageUp = 33,
    //
    // Summary:
    //     The Page Down key.
    PageDown = 34,
    //
    // Summary:
    //     The End key.
    End = 35,
    //
    // Summary:
    //     The Home key.
    Home = 36,
    //
    // Summary:
    //     The Left Arrow key.
    Left = 37,
    //
    // Summary:
    //     The Up Arrow key.
    Up = 38,
    //
    // Summary:
    //     The Right Arrow key.
    Right = 39,
    //
    // Summary:
    //     The Down Arrow key.
    Down = 40,
    //
    // Summary:
    //     The Select key or button.
    Select = 41,
    //
    // Summary:
    //     The Print key or button.
    Print = 42,
    //
    // Summary:
    //     The execute key or button.
    Execute = 43,
    //
    // Summary:
    //     The snapshot key or button.
    Snapshot = 44,
    //
    // Summary:
    //     The Insert key.
    Insert = 45,
    //
    // Summary:
    //     The Delete key.
    Delete = 46,
    //
    // Summary:
    //     The Help key or button.
    Help = 47,
    //
    // Summary:
    //     The number "0" key.
    Number0 = 48,
    //
    // Summary:
    //     The number "1" key.
    Number1 = 49,
    //
    // Summary:
    //     The number "2" key.
    Number2 = 50,
    //
    // Summary:
    //     The number "3" key.
    Number3 = 51,
    //
    // Summary:
    //     The number "4" key.
    Number4 = 52,
    //
    // Summary:
    //     The number "5" key.
    Number5 = 53,
    //
    // Summary:
    //     The number "6" key.
    Number6 = 54,
    //
    // Summary:
    //     The number "7" key.
    Number7 = 55,
    //
    // Summary:
    //     The number "8" key.
    Number8 = 56,
    //
    // Summary:
    //     The number "9" key.
    Number9 = 57,
    //
    // Summary:
    //     The letter "A" key.
    A = 65,
    //
    // Summary:
    //     The letter "B" key.
    B = 66,
    //
    // Summary:
    //     The letter "C" key.
    C = 67,
    //
    // Summary:
    //     The letter "D" key.
    D = 68,
    //
    // Summary:
    //     The letter "E" key.
    E = 69,
    //
    // Summary:
    //     The letter "F" key.
    F = 70,
    //
    // Summary:
    //     The letter "G" key.
    G = 71,
    //
    // Summary:
    //     The letter "H" key.
    H = 72,
    //
    // Summary:
    //     The letter "I" key.
    I = 73,
    //
    // Summary:
    //     The letter "J" key.
    J = 74,
    //
    // Summary:
    //     The letter "K" key.
    K = 75,
    //
    // Summary:
    //     The letter "L" key.
    L = 76,
    //
    // Summary:
    //     The letter "M" key.
    M = 77,
    //
    // Summary:
    //     The letter "N" key.
    N = 78,
    //
    // Summary:
    //     The letter "O" key.
    O = 79,
    //
    // Summary:
    //     The letter "P" key.
    P = 80,
    //
    // Summary:
    //     The letter "Q" key.
    Q = 81,
    //
    // Summary:
    //     The letter "R" key.
    R = 82,
    //
    // Summary:
    //     The letter "S" key.
    S = 83,
    //
    // Summary:
    //     The letter "T" key.
    T = 84,
    //
    // Summary:
    //     The letter "U" key.
    U = 85,
    //
    // Summary:
    //     The letter "V" key.
    V = 86,
    //
    // Summary:
    //     The letter "W" key.
    W = 87,
    //
    // Summary:
    //     The letter "X" key.
    X = 88,
    //
    // Summary:
    //     The letter "Y" key.
    Y = 89,
    //
    // Summary:
    //     The letter "Z" key.
    Z = 90,
    //
    // Summary:
    //     The left Windows key.
    LeftWindows = 91,
    //
    // Summary:
    //     The right Windows key.
    RightWindows = 92,
    //
    // Summary:
    //     The application key or button.
    Application = 93,
    //
    // Summary:
    //     The sleep key or button.
    Sleep = 95,
    //
    // Summary:
    //     The number "0" key as located on a numeric pad.
    NumberPad0 = 96,
    //
    // Summary:
    //     The number "1" key as located on a numeric pad.
    NumberPad1 = 97,
    //
    // Summary:
    //     The number "2" key as located on a numeric pad.
    NumberPad2 = 98,
    //
    // Summary:
    //     The number "3" key as located on a numeric pad.
    NumberPad3 = 99,
    //
    // Summary:
    //     The number "4" key as located on a numeric pad.
    NumberPad4 = 100,
    //
    // Summary:
    //     The number "5" key as located on a numeric pad.
    NumberPad5 = 101,
    //
    // Summary:
    //     The number "6" key as located on a numeric pad.
    NumberPad6 = 102,
    //
    // Summary:
    //     The number "7" key as located on a numeric pad.
    NumberPad7 = 103,
    //
    // Summary:
    //     The number "8" key as located on a numeric pad.
    NumberPad8 = 104,
    //
    // Summary:
    //     The number "9" key as located on a numeric pad.
    NumberPad9 = 105,
    //
    // Summary:
    //     The multiply (*) operation key as located on a numeric pad.
    Multiply = 106,
    //
    // Summary:
    //     The add (+) operation key as located on a numeric pad.
    Add = 107,
    //
    // Summary:
    //     The separator key as located on a numeric pad.
    Separator = 108,
    //
    // Summary:
    //     The subtract (-) operation key as located on a numeric pad.
    Subtract = 109,
    //
    // Summary:
    //     The decimal (.) key as located on a numeric pad.
    Decimal = 110,
    //
    // Summary:
    //     The divide (/) operation key as located on a numeric pad.
    Divide = 111,
    //
    // Summary:
    //     The F1 function key.
    F1 = 112,
    //
    // Summary:
    //     The F2 function key.
    F2 = 113,
    //
    // Summary:
    //     The F3 function key.
    F3 = 114,
    //
    // Summary:
    //     The F4 function key.
    F4 = 115,
    //
    // Summary:
    //     The F5 function key.
    F5 = 116,
    //
    // Summary:
    //     The F6 function key.
    F6 = 117,
    //
    // Summary:
    //     The F7 function key.
    F7 = 118,
    //
    // Summary:
    //     The F8 function key.
    F8 = 119,
    //
    // Summary:
    //     The F9 function key.
    F9 = 120,
    //
    // Summary:
    //     The F10 function key.
    F10 = 121,
    //
    // Summary:
    //     The F11 function key.
    F11 = 122,
    //
    // Summary:
    //     The F12 function key.
    F12 = 123,
    //
    // Summary:
    //     The F13 function key.
    F13 = 124,
    //
    // Summary:
    //     The F14 function key.
    F14 = 125,
    //
    // Summary:
    //     The F15 function key.
    F15 = 126,
    //
    // Summary:
    //     The F16 function key.
    F16 = 127,
    //
    // Summary:
    //     The F17 function key.
    F17 = 128,
    //
    // Summary:
    //     The F18 function key.
    F18 = 129,
    //
    // Summary:
    //     The F19 function key.
    F19 = 130,
    //
    // Summary:
    //     The F20 function key.
    F20 = 131,
    //
    // Summary:
    //     The F21 function key.
    F21 = 132,
    //
    // Summary:
    //     The F22 function key.
    F22 = 133,
    //
    // Summary:
    //     The F23 function key.
    F23 = 134,
    //
    // Summary:
    //     The F24 function key.
    F24 = 135,
    //
    // Summary:
    //     The navigation up button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationView = 136,
    //
    // Summary:
    //     The navigation menu button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationMenu = 137,
    //
    // Summary:
    //     The navigation up button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationUp = 138,
    //
    // Summary:
    //     The navigation down button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationDown = 139,
    //
    // Summary:
    //     The navigation left button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationLeft = 140,
    //
    // Summary:
    //     The navigation right button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationRight = 141,
    //
    // Summary:
    //     The navigation accept button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationAccept = 142,
    //
    // Summary:
    //     The navigation cancel button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    NavigationCancel = 143,
    //
    // Summary:
    //     The Num Lock key.
    NumberKeyLock = 144,
    //
    // Summary:
    //     The Scroll Lock (ScrLk) key.
    Scroll = 145,
    //
    // Summary:
    //     The left Shift key.
    LeftShift = 160,
    //
    // Summary:
    //     The right Shift key.
    RightShift = 161,
    //
    // Summary:
    //     The left Ctrl key.
    LeftControl = 162,
    //
    // Summary:
    //     The right Ctrl key.
    RightControl = 163,
    //
    // Summary:
    //     The left menu key.
    LeftMenu = 164,
    //
    // Summary:
    //     The right menu key.
    RightMenu = 165,
    //
    // Summary:
    //     The go back key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GoBack = 166,
    //
    // Summary:
    //     The go forward key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GoForward = 167,
    //
    // Summary:
    //     The refresh key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    Refresh = 168,
    //
    // Summary:
    //     The stop key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    Stop = 169,
    //
    // Summary:
    //     The search key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    Search = 170,
    //
    // Summary:
    //     The favorites key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    Favorites = 171,
    //
    // Summary:
    //     The go home key.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GoHome = 172,
    //
    // Summary:
    //     The gamepad A button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadA = 195,
    //
    // Summary:
    //     The gamepad B button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadB = 196,
    //
    // Summary:
    //     The gamepad X button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadX = 197,
    //
    // Summary:
    //     The gamepad Y button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadY = 198,
    //
    // Summary:
    //     The gamepad right shoulder.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightShoulder = 199,
    //
    // Summary:
    //     The gamepad left shoulder.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftShoulder = 200,
    //
    // Summary:
    //     The gamepad left trigger.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftTrigger = 201,
    //
    // Summary:
    //     The gamepad right trigger.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightTrigger = 202,
    //
    // Summary:
    //     The gamepad d-pad up.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadDPadUp = 203,
    //
    // Summary:
    //     The gamepad d-pad down.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadDPadDown = 204,
    //
    // Summary:
    //     The gamepad d-pad left.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadDPadLeft = 205,
    //
    // Summary:
    //     The gamepad d-pad right.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadDPadRight = 206,
    //
    // Summary:
    //     The gamepad menu button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadMenu = 207,
    //
    // Summary:
    //     The gamepad view button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadView = 208,
    //
    // Summary:
    //     The gamepad left thumbstick button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftThumbstickButton = 209,
    //
    // Summary:
    //     The gamepad right thumbstick button.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightThumbstickButton = 210,
    //
    // Summary:
    //     The gamepad left thumbstick up.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftThumbstickUp = 211,
    //
    // Summary:
    //     The gamepad left thumbstick down.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftThumbstickDown = 212,
    //
    // Summary:
    //     The gamepad left thumbstick right.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftThumbstickRight = 213,
    //
    // Summary:
    //     The gamepad left thumbstick left.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadLeftThumbstickLeft = 214,
    //
    // Summary:
    //     The gamepad right thumbstick up.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightThumbstickUp = 215,
    //
    // Summary:
    //     The gamepad right thumbstick down.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightThumbstickDown = 216,
    //
    // Summary:
    //     The gamepad right thumbstick right.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightThumbstickRight = 217,
    //
    // Summary:
    //     The gamepad right thumbstick left.
    [SupportedOSPlatform("Windows10.0.10240.0")]
    GamepadRightThumbstickLeft = 218,


    // ---------------- Additional --------------------
    LeftBracket = 0xDB,
    RightBracket = 0xDD,
    Backslash = 0xDC,
    ForwardSlash = 0xBF,
    Quote = 0xDE,
    Oem102 = 0xE2, // Sometimes <> keys, sometimes \|
    Period = 0xBE, // On US keyboards, this is the .> key
    Semicolon = 0xBA, // Semicolon/Colon key
    Minus = 0xBD,
    Plus = 0xBB,
    Tilde = 0xC0, // Tilde/Grave/Backtick key
    Comma = 0xBC,

    // ---------------- Duplicate Definitions ----------------
    NumLock = NumberKeyLock,
    CapsLock = CapitalLock,
    ScrollLock = Scroll,

    // ----------------- Other -----------------------
    Null = -1, // No key. Could also use None but this might have specific meaning in my app

}