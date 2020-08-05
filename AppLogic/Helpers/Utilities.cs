// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Text;
using System.Windows.Forms;

namespace AppLogic.Helpers
{
    internal static partial class Utilities
    {
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

        public static void EnableMenuShortcutsUnderlining()
        {
            int pv = 1;
            WinApi.SystemParametersInfo(WinApi.SPI_SETKEYBOARDCUES, 0, ref pv, 0);
        }

        public static bool IsDescendant(IntPtr hwndParent, IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero &&
                (hwnd == hwndParent || WinApi.IsChild(hwndParent, hwnd));
        }
    }
}
