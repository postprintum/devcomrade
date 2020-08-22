// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace AppLogic.Helpers
{
    internal class WaitCursorScope : IDisposable
    {
        private static readonly ThreadLocal<IDisposable?> s_current = 
            new ThreadLocal<IDisposable?>();

        private const int DELAY = 200;

        private bool IsCurrent => s_current.Value == this;

        private readonly Func<bool>? _showWhen;
        private readonly Cursor? _oldCursor;
        private readonly System.Windows.Forms.Timer? _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private void OnIdle(object? s, EventArgs e)
        {
            if (this.IsCurrent && 
                _showWhen?.Invoke() != false && // i.e., if null or true
                _stopwatch.ElapsedMilliseconds >= DELAY)
            {
                Cursor.Current = Cursors.AppStarting;
            }
        }

        private WaitCursorScope(Func<bool>? showWhen = null)
        {
            s_current.Value?.Dispose();
            s_current.Value = this;

            _stopwatch.Start();
            _oldCursor = Cursor.Current;
            _showWhen = showWhen;
            _timer = new System.Windows.Forms.Timer { Interval = 250 };
            _timer.Tick += OnIdle;
            _timer.Start();
            Application.Idle += OnIdle;
        }

        public void Stop()
        {
            if (s_current.Value == this)
            {
                s_current.Value = null;
                _timer?.Dispose();
                Application.Idle -= OnIdle;
                Cursor.Hide();
                Cursor.Current = _oldCursor ?? Cursors.Default;
                Cursor.Show();
            }
        }

        void IDisposable.Dispose()
        {
            Stop();
        }

        public static IDisposable Create(Func<bool>? showWhen = null)
        {
            return new WaitCursorScope(showWhen);
        }
    }
}
