// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Text;
using System.Threading;

namespace AppLogic.Helpers
{
    /// <summary>
    /// Attach Win32 input queue to another thread 
    /// </summary>
    internal class AttachedThreadInputScope: IDisposable
    {
        private static readonly ThreadLocal<IDisposable?> s_current = 
            new ThreadLocal<IDisposable?>();

        public bool IsCurrent => s_current.Value == this;  
        public bool IsAttached => _attached;
        public IntPtr ForegroundWindow => _foregroundWindow;

        private bool _attached = false;
        private readonly IntPtr _foregroundWindow = IntPtr.Zero;
        private readonly uint _foregroundThreadId = 0;
        private readonly uint _currentThreadId = 0;

        private AttachedThreadInputScope()
        {
            s_current.Value?.Dispose();
            s_current.Value = this;

            _foregroundWindow = WinApi.GetForegroundWindow();
            _currentThreadId = WinApi.GetCurrentThreadId();
            _foregroundThreadId = WinApi.GetWindowThreadProcessId(_foregroundWindow, out var _);

            if (_currentThreadId != _foregroundThreadId)
            {
                // attach to the foreground thread
                if (!WinApi.AttachThreadInput(_currentThreadId, _foregroundThreadId, true))
                {
                    // unable to attach, see if the window is a Win10 Console Window
                    var className = new StringBuilder(capacity: 256);
                    WinApi.GetClassName(_foregroundWindow, className, className.Capacity - 1);
                    if (String.CompareOrdinal("ConsoleWindowClass", className.ToString()) != 0)
                    {
                        return;
                    }
                    // consider attached to a console window
                }
            }

            _attached = true;
        }

        void IDisposable.Dispose()
        {
            if (s_current.Value == this)
            {
                s_current.Value = null;
                if (_attached)
                {
                    _attached = false;
                    if (_currentThreadId != _foregroundThreadId)
                    {
                        WinApi.AttachThreadInput(_currentThreadId, _foregroundThreadId, false);
                    }
                }
            }
        }

        public static AttachedThreadInputScope Create()
        {
            return new AttachedThreadInputScope();
        }
    }
}
