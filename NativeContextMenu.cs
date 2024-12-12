using Dynamic_Lighting_Key_Indicator;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

#pragma warning disable IDE0305 // Collection expression more confusing than a simple ToArray initializer

namespace Dynamic_Lighting_Key_Indicator
{
    public class NativeContextMenu
    {
        // Win32 API constants
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_LEFTBUTTON = 0x0000;

        // Win32 API structures
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // Win32 API functions
        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hwnd, IntPtr lprc);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public static uint ShowContextMenu(IntPtr hwnd, MenuItem[] menuItems)
        {
            // Create the popup menu
            IntPtr hMenu = CreatePopupMenu();

            // Add menu items
            uint itemId = 1;
            foreach (var item in menuItems)
            {
                if (item.IsSeparator)
                {
                    InsertMenu(hMenu, itemId, MF_SEPARATOR, itemId, string.Empty);
                }
                else
                {
                    InsertMenu(hMenu, itemId, MF_STRING, itemId, item.Text);
                }
                itemId++;
            }

            // Get the current cursor position to display the menu at that location, result comes out as pt parameter
            GetCursorPos(out POINT pt);

            // This is necessary to ensure the menu will close when the user clicks elsewhere
            SetForegroundWindow(hwnd);

            // Tells the OS to show the context menu and wait for a selection. But if the user clicks elsewhere, it will return 0.
            uint flags = TPM_RIGHTBUTTON | TPM_LEFTBUTTON | TPM_RETURNCMD;
            uint clickedItem = TrackPopupMenu(hMenu, flags, pt.X, pt.Y, 0, hwnd, IntPtr.Zero);

            // Clean up
            DestroyMenu(hMenu);

            return clickedItem;
        }

        public class MenuItem
        {
            public string Text { get; set; }
            public bool IsSeparator { get; set; }
            public int Index { get; set; } // The index should be 1-based

            public MenuItem(string text, int index)
            {
                Text = text;
                IsSeparator = false;
                Index = index;
            }

            public static MenuItem Separator(int index)
            {
                return new MenuItem(string.Empty, index) { IsSeparator = true };
            }
        }

        public class MenuItemSet
        {
            private readonly List<MenuItem> _menuItems = [];

            public void AddMenuItem(string text)
            {
                _menuItems.Add(new MenuItem(text, _menuItems.Count + 1)); // 1-based index because 0 is reserved for no selection
            }

            public void AddSeparator()
            {
                _menuItems.Add(MenuItem.Separator(_menuItems.Count + 1)); // 1-based index because 0 is reserved for no selection
            }

            public MenuItem[] GetMenuItems()
            {
                return _menuItems.ToArray();
            }

            public int GetMenuItemIndex_ByText(string text)
            {
                return _menuItems.FindIndex(x => x.Text == text);
            }

            public string? GetMenuItemText_ByIndex(int index)
            {
                return _menuItems.Find(x => x.Index == index)?.Text;
            }
        }
    } // End of NativeContextMenu class

    public class CustomContextMenu
    {
        private static class MenuItemNames
        {
            public const string Restore = "Restore";
            public const string Restart = "Restart Process";
            public const string Exit = "Exit";
            
        }

        internal static void CreateAndShowMenu(IntPtr hwnd, SystemTray systemTray)
        {
            var menuItemSet = new NativeContextMenu.MenuItemSet();

            //menuItemSet.AddMenuItem(MenuItemNames.Restore);
            //menuItemSet.AddSeparator();
            menuItemSet.AddMenuItem(MenuItemNames.Restart);
            menuItemSet.AddMenuItem(MenuItemNames.Exit);

            // Show menu and get selection
            uint selected = NativeContextMenu.ShowContextMenu(hwnd, menuItemSet.GetMenuItems());

            // Handle the selected item
            if (selected > 0)
            {
                string? selectedText = menuItemSet.GetMenuItemText_ByIndex((int)selected);

                //Call the appropriate function based on the selected menu item
                switch (selectedText)
                {
                    case MenuItemNames.Restore:
                        systemTray.RestoreFromTray();
                        break;
                    case MenuItemNames.Restart:
                        RestartApplication();
                        break;
                    case MenuItemNames.Exit:
                        systemTray.ExitApplication();
                        break;

                    case null:
                        Trace.WriteLine("Error: Selected item not found.");
                        break;
                    default:
                        Trace.WriteLine("Error: Selected item not handled.");
                        break;
                }
            }
        }


        // General universal functions
        private static void RestartApplication()
        {
            // Restart the application
            string? executablePath = Environment.ProcessPath;
            if (executablePath == null)
            {
                Trace.WriteLine("Error: Executable path not found.");
                return;
            }
            Process.Start(executablePath);
            Environment.Exit(0);
        }

        //private static void ExitApplication()
        //{
        //    // Logic to exit the application
        //    Trace.WriteLine("Exiting application...");
        //    Environment.Exit(0);
        //}

    } //  ---------------- End of CustomContextMenu class ----------------

    public class NativeMessageBox
    {
        // Import the MessageBox function from user32.dll
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        // MB_OK constant from WinUser.h
        private const uint MB_OK = 0x00000000;

        public static void ShowInfoMessage(string message, string title)
        {
            // Show message box with MB_OK style (just OK button)
            // First parameter is IntPtr.Zero for no parent window
            _ = MessageBox(IntPtr.Zero, message, title, MB_OK);
        }

        public static void ShowErrorMessage(string message, string title)
        {
            // Show message box with MB_ICONERROR style (error icon)
            // First parameter is IntPtr.Zero for no parent window
            const uint MB_ICONERROR = 0x00000010;
            _ = MessageBox(IntPtr.Zero, message, title, MB_OK | MB_ICONERROR);
        }
    } // --------------- End of NativeMessageBox class ---------------


} // --------------- End of Namespace ---------------
