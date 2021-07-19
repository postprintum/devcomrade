// Copyright (C) 2020+ by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLogic.Events;

namespace AppLogic.Presenter
{
    internal class ClipboardFormatMonitor : NativeWindow
    {
        private IEventTargetHub EventTargetHub { get; init; }

        public ClipboardFormatMonitor(IEventTargetHub hub)
        {
            this.EventTargetHub = hub;

            var cp = new CreateParams()
            {
                Caption = String.Empty,
                Style = unchecked((int)WinApi.WS_POPUP),
                Parent = WinApi.HWND_MESSAGE,
            };

            base.CreateHandle(cp);
        }

        public async Task StartAsync()
        {
            // AddClipboardFormatListener may fail when 
            // another app clipboard operation is in progress

            const int retryDelay = 500;
            int retryAttempts = 10;

            while (true)
            {
                if (WinApi.AddClipboardFormatListener(this.Handle))
                {
                    return;
                }
                if (--retryAttempts <= 0)
                {
                    break;
                } 
                await Task.Delay(retryDelay);
            }

            throw WinUtils.CreateExceptionFromLastWin32Error();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WinApi.WM_CLIPBOARDUPDATE)
            {
                this.EventTargetHub.Dispatch(this, new ClipboardUpdateEventArgs());
            }
            base.WndProc(ref m);
        }
    }
}
