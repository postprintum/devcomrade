// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppLogic.Presenter
{
    internal class ClipboardFormatMonitor : NativeWindow
    {
        public event EventHandler? ClipboardUpdate;

        public ClipboardFormatMonitor()
        {
            var cp = new CreateParams()
            {
                Caption = String.Empty,
                Style = unchecked((int)WinApi.WS_POPUP),
                Parent = WinApi.HWND_MESSAGE,
            };

			base.CreateHandle(cp);
            if (!WinApi.AddClipboardFormatListener(this.Handle))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WinApi.WM_CLIPBOARDUPDATE)
            {
                this.ClipboardUpdate?.Invoke(this, new EventArgs());
            }
            base.WndProc(ref m);
        }
    }
}
