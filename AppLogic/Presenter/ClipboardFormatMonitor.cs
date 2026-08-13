// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
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

        private bool _listening = false;

        public bool IsListening => _listening;

        public async Task StartAsync()
        {
            if (_listening)
            {
                return;
            }

            // AddClipboardFormatListener may fail when
            // another app clipboard operation is in progress

            const int retryDelay = 500;
            int retryAttempts = 10;

            while (true)
            {
                if (WinApi.AddClipboardFormatListener(this.Handle))
                {
                    _listening = true;
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

        /// <summary>
        /// Unhook from the clipboard chain, so that no WM_CLIPBOARDUPDATE
        /// is delivered at all while the feature is switched off
        /// </summary>
        public void Stop()
        {
            if (!_listening)
            {
                return;
            }
            _listening = false;
            WinApi.RemoveClipboardFormatListener(this.Handle);
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
