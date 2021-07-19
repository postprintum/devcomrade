// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppLogic.Helpers
{
    internal static partial class WinUtils
    {
        /// <summary>
        /// Get top-level window by process name
        /// </summary>
        public static IntPtr FindProcessWindow(string processName)
        {
            var hwndFound = IntPtr.Zero;

            bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam)
            {
                if (!WinApi.IsWindowVisible(hwnd))
                {     
                    return true;
                }

                WinApi.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == 0)
                {
                    return true;
                }

                var hProcess = WinApi.OpenProcess(WinApi.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProcess == IntPtr.Zero)
                {
                    return true;
                }
                try
                {
                    var buffer = new StringBuilder(1024);
                    int size = buffer.Capacity;
                    if (WinApi.QueryFullProcessImageName(hProcess, 0, buffer, out size))
                    {
                        var path = buffer.ToString();
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                        if (String.Compare(fileName, processName, ignoreCase: true) == 0)
                        {
                            hwndFound = hwnd;
                            return false;
                        }
                    }
                }
                finally
                {
                    WinApi.CloseHandle(hProcess);
                }
                return true;
            }

            WinApi.EnumDesktopWindows(IntPtr.Zero, EnumWindowsProc, IntPtr.Zero);
            return hwndFound;
        }

        /// <summary>
        /// Activate the process's top window
        /// </summary>
        public static async Task<bool> ActivateProcess(string processName, CancellationToken token)
        {
            var hwnd = WinUtils.FindProcessWindow(processName);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            var placement = new WinApi.WINDOWPLACEMENT();
            placement.length = System.Runtime.InteropServices.Marshal.SizeOf(placement);
            WinApi.GetWindowPlacement(hwnd, ref placement);
            var showCmd = placement.showCmd switch
            {
                WinApi.SW_SHOWMAXIMIZED => WinApi.SW_SHOWMAXIMIZED,
                WinApi.SW_SHOWMINIMIZED => WinApi.SW_RESTORE,
                _ => WinApi.SW_SHOWNORMAL
            };

            using (AttachedThreadInputScope.Create())
            {
                await InputUtils.TimerYield(token: token);
                WinApi.SetForegroundWindow(hwnd);
                WinApi.ShowWindow(hwnd, showCmd);
                await InputUtils.TimerYield(token: token);
            }
            return true;
        }

        /// <summary>
        /// GetHotkeyTitle
        /// </summary>
        public static string GetHotkeyTitle(uint mods, uint vk)
        {
            var title = new StringBuilder();
            if ((mods & WinApi.MOD_SHIFT) != 0)
            {
                title.Append("Shift+");
            }
            if ((mods & WinApi.MOD_CONTROL) != 0)
            {
                title.Append("Ctrl+");
            }
            if ((mods & WinApi.MOD_WIN) != 0)
            {
                title.Append("Win+");
            }
            if ((mods & WinApi.MOD_ALT) != 0)
            {
                title.Append("Alt+");
            }
            title.Append(Enum.GetName(typeof(Keys), vk));
            return title.ToString();
        }

        /// <summary>
        /// Enable menu shortcuts underlining
        /// </summary>
        public static void EnableMenuShortcutsUnderlining()
        {
            int pv = 1;
            WinApi.SystemParametersInfo(WinApi.SPI_SETKEYBOARDCUES, 0, ref pv, 0);
        }

        /// <summary>
        /// Check if the window is the same as or a child of another window
        /// </summary>
        public static bool IsDescendant(IntPtr hwndParent, IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero &&
                (hwnd == hwndParent || WinApi.IsChild(hwndParent, hwnd));
        }

        /// <summary>
        /// Returns a foreground window which is not of our app and not the taskbar
        /// </summary>
        public static bool TryGetThirdPartyForgroundWindow(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            var foregroundWindow = WinApi.GetForegroundWindow();
            var currentThreadId = WinApi.GetCurrentThreadId();
            var foregroundThread = WinApi.GetWindowThreadProcessId(foregroundWindow, out var _);
            if (currentThreadId == foregroundThread)
            {
                return false;
            }
            else
            {
                var hwndRoot = WinApi.GetAncestor(foregroundWindow, WinApi.GA_ROOT);
                if (hwndRoot != IntPtr.Zero)
                {
                    foregroundWindow = hwndRoot;
                }

                var className = new StringBuilder(capacity: 256);
                WinApi.GetClassName(foregroundWindow, className, className.Capacity - 1);
                if (className.ToString().CompareTo("Shell_TrayWnd") == 0)
                {
                    return false;
                }

                hwnd = foregroundWindow;
                return true;
            }
        }

        /// <summary>
        /// Get top level window for the current desktop
        /// </summary>
        public static IntPtr GetPrevActiveWindow()
        {
            var foregroundWindow = WinApi.GetAncestor(WinApi.GetForegroundWindow(), WinApi.GA_ROOT);

            var hwndPrev = IntPtr.Zero;
            var hwndFound = IntPtr.Zero;

            bool enumWindowsProc(IntPtr hwnd, IntPtr lparam)
            {
                if (WinApi.IsWindowVisible(hwnd))
                {
                    if (hwndPrev == foregroundWindow)
                    {
                        hwndFound = hwnd;
                        return false;
                    }
                    hwndPrev = hwnd;
                }
                return true;
            }

            WinApi.EnumDesktopWindows(IntPtr.Zero, enumWindowsProc, IntPtr.Zero);
            return hwndFound;
        }

        /// <summary>
        /// Create an Exception object for the last failed Win32 API
        /// </summary>
        public static Exception CreateExceptionFromLastWin32Error(int hresult = WinApi.E_FAIL)
        {
            var lastError = Marshal.GetLastWin32Error();
            if (lastError != 0)
            {
                return new Win32Exception(lastError);
            }
            else
            {
                return new COMException(null, hresult);
            }
        }
    }
}
