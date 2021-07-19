// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Helpers
{
    internal static class InputUtils
    {
        public const int MIN_DELAY = WinApi.USER_TIMER_MINIMUM;

        public static bool AnyInputMessage(uint qsFlags)
        {
            // the high-order word of the return value indicates the types of messages currently in the queue,
            // including already observed but not retrieved messages;
            // beware of the undoc QS_EVENT, it may always be returning non-zero
            uint status = WinApi.GetQueueStatus(qsFlags);
            return (status >> 16) != 0;
        }

        /// <summary>
        /// Yield to the message loop via a low-priority WM_TIMER message
        /// https://web.archive.org/web/20130627005845/http://support.microsoft.com/kb/96006 
        /// All input messages are processed before WM_TIMER and WM_PAINT messages.
        /// Unlike with System.Windows.Forms.Timer, we don't need a hidden window for this.
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCode]
        public static async ValueTask TimerYield(
            int delay = MIN_DELAY, 
            CancellationToken token = default)
        {
            // It is possible to reduce the ammount of allocations by 
            // using a custom awaiter or a pooled/cached implementation of IValueTaskSource 
            // instead of TaskCompletionSource, as well as TimerProc, 
            // but that'd be an overkill for our use case

            token.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<DBNull>();

            WinApi.TimerProc timerProc = delegate
            {
                if (token.IsCancellationRequested)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(DBNull.Value);
            };

            var gch = System.Runtime.InteropServices.GCHandle.Alloc(timerProc);
            try
            {
                var timerId = WinApi.SetTimer(IntPtr.Zero, IntPtr.Zero, (uint)delay, timerProc);
                if (timerId == IntPtr.Zero)
                    throw new InvalidOperationException();
                try
                {
                    using var rego = token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
                    await tcs.Task;
                }
                finally
                {
                    WinApi.KillTimer(IntPtr.Zero, timerId);
                }
            }
            finally
            {
                gch.Free();
            }
        }

        /// <summary>
        /// timer-based yielding to keep the UI responsive
        /// </summary>
        public static async ValueTask InputYield(
            uint qsFlags = WinApi.QS_INPUT | WinApi.QS_POSTMESSAGE, 
            int delay = MIN_DELAY, 
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            while (true)
            {
                // yield via a low-priority WM_TIMER message, 
                // all keyboard and mouse messages should have been pumped
                // before WM_TIMER is posted
                await TimerYield(delay, token);

                // exit if there is no pending input in the Windows message queue to process
                if (!AnyInputMessage(qsFlags))
                    break;
            }
        }
    }
}
