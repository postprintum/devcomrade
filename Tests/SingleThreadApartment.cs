// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    /// <summary>
    /// Queue posted callback items for execution on ThreadPool while collecting exceptions,
    /// Used for testing async/await behaviors, including async void methods.
    /// This is a pumping STA thread, Windows messages do get pumped and legacy COM can be used
    /// <seealso href="https://stackoverflow.com/a/21371891/1768303"/>
    /// </summary>
    public class SingleThreadApartment : AsyncApartmentBase
    {
        #region Helpers

        internal class SingleThreadSyncContext : SynchronizationContext
        {
            private readonly SingleThreadApartment _apartment;

            private readonly Thread _thread;

            private readonly bool _pumpMessages;

            private readonly BlockingCollection<(SendOrPostCallback, object?)> _items =
                new BlockingCollection<(SendOrPostCallback, object?)>();

            public TaskScheduler TaskScheduler { get; }

            public SingleThreadSyncContext(SingleThreadApartment apartment, bool pumpMessages = true)
            {
                _apartment = apartment;
                _pumpMessages = pumpMessages;

                if (_pumpMessages)
                {
                    base.SetWaitNotificationRequired();
                }

                var startTcs = new TaskCompletionSource<TaskScheduler>();

                void threadStart()
                {
                    try
                    {
                        SynchronizationContext.SetSynchronizationContext(this);
                        startTcs.TrySetResult(TaskScheduler.FromCurrentSynchronizationContext());
                        try
                        {
                            foreach ((SendOrPostCallback d, object? state) in _items.GetConsumingEnumerable())
                            {
                                try
                                {
                                    d(state);
                                }
                                catch (Exception ex)
                                {
                                    _apartment.AddException(ex);
                                }
                                finally
                                {
                                    OperationCompleted();
                                }
                            }
                        }
                        finally
                        {
                            SynchronizationContext.SetSynchronizationContext(null);
                            _apartment.TrySetCompletion();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!startTcs.TrySetException(ex))
                            throw;
                    }
                }

                _thread = new Thread(threadStart);
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.IsBackground = true;
                _thread.Start();

                this.TaskScheduler = startTcs.Task.Result;
            }

            public void Conclude()
            {
                _items.CompleteAdding();
            }

            public void JoinThread()
            {
                if (_thread.IsAlive)
                {
                    _thread.Join();
                }
            }

            public override void Send(SendOrPostCallback d, object? state)
            {
                throw new NotImplementedException(nameof(Send));
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }

            public override void OperationStarted()
            {
                _apartment.OperationStarted();
            }

            public override void OperationCompleted()
            {
                _apartment.OperationCompleted();
            }

            public override void Post(SendOrPostCallback d, object? state)
            {
                OperationStarted();
                _items.Add((d, state));
            }

            /// <summary>
            /// Blocking wait for Win32 kernel objects with Win32 message pumping
            /// </summary>
            public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
            {
                if (!_pumpMessages)
                {
                    return WaitHelper(waitHandles, waitAll, millisecondsTimeout);
                }

                if (waitHandles == null || (uint)waitHandles.Length is var count && count == 0)
                    throw new ArgumentNullException();

                var timeout = millisecondsTimeout;

                // wait for all?
                if (waitAll && count > 1)
                {
                    // more: https://tinyurl.com/Apartments-and-Pumping, search for "mutex"
                    throw new NotSupportedException("WaitAll for multiple handles on a STA thread is not supported.");
                }
                else
                {
                    // optimization: a quick check with a zero timeout
                    var nativeResult = WinApi.WaitForMultipleObjects(
                        count, waitHandles, 
                        bWaitAll: false, dwMilliseconds: 0);
                    if (IsNativeWaitSuccessful(count, nativeResult, out var managedResult))
                    {
                        return managedResult;
                    }

                    // proceed to pumping

                    // track timeout if not infinite
                    var startTickCount = Environment.TickCount;
                    var remainingTimeout = (int)timeout;

                    // the core loop
                    var msg = new WinApi.MSG();
                    while (true)
                    {
                        // MsgWaitForMultipleObjectsEx with MWMO_INPUTAVAILABLE returns,
                        // even if there's a message already seen but not removed in the message queue
                        nativeResult = WinApi.MsgWaitForMultipleObjectsEx(
                            count, waitHandles,
                            (uint)remainingTimeout,
                            WinApi.QS_ALLINPUT,
                            WinApi.MWMO_INPUTAVAILABLE);

                        if (IsNativeWaitSuccessful(count, nativeResult, out managedResult) ||
                            managedResult == WaitHandle.WaitTimeout)
                        {
                            return managedResult;
                        }

                        // there is a message, pump and dispatch it
                        if (WinApi.PeekMessage(out msg, IntPtr.Zero, 0, 0, WinApi.PM_REMOVE))
                        {
                            WinApi.TranslateMessage(ref msg);
                            WinApi.DispatchMessage(ref msg);
                        }

                        // check the timeout
                        if (remainingTimeout != Timeout.Infinite)
                        {
                            // Environment.TickCount is expected to wrap correctly even when runs continuously 
                            var lapse = unchecked(Environment.TickCount - startTickCount);
                            remainingTimeout = Math.Max(timeout - lapse, 0);
                            if (remainingTimeout <= 0)
                            {
                                return WaitHandle.WaitTimeout;
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Analyze the result of the native wait API
            /// </summary>
            private static bool IsNativeWaitSuccessful(uint count, uint nativeResult, out int managedResult)
            {
                if (nativeResult == WinApi.WAIT_TIMEOUT)
                {
                    // timed out
                    managedResult = WaitHandle.WaitTimeout;
                    return false;
                }

                managedResult = unchecked((int)(nativeResult - WinApi.WAIT_OBJECT_0));

                if (nativeResult == (WinApi.WAIT_OBJECT_0 + count))
                {
                    // a Windows message is pending, only valid for MsgWaitForMultipleObjectsEx
                    return false;
                }

                if (nativeResult >= WinApi.WAIT_OBJECT_0 && nativeResult < (WinApi.WAIT_OBJECT_0 + count))
                {
                    // one of the native kernel objects has signalled
                    return true;
                }

                if (nativeResult >= WinApi.WAIT_ABANDONED_0 && nativeResult < (WinApi.WAIT_ABANDONED_0 + count))
                {
                    // an abandoned native mutex
                    throw new AbandonedMutexException();
                }

                if (nativeResult == WinApi.WAIT_IO_COMPLETION)
                {
                    // io completion
                    return false;
                }

                if (nativeResult == WinApi.WAIT_FAILED)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                throw new InvalidOperationException();
            }
        }

        #endregion

        #region State

        private readonly SingleThreadSyncContext _syncContext;

        #endregion

        protected override TaskScheduler TaskScheduler { get; }

        public override Task Completion { get; }

        public SingleThreadApartment(bool pumpMessages = true)
        {
            _syncContext = new SingleThreadSyncContext(this, pumpMessages);

            Task waitForCompletionAsync() =>
                GetCompletionTask().ContinueWith(
                    anteTask => { _syncContext.JoinThread(); return anteTask; },
                    cancellationToken: CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    scheduler: TaskScheduler.Default).Unwrap();

            this.TaskScheduler = _syncContext.TaskScheduler;
            this.Completion = waitForCompletionAsync();
        }

        protected override void ConcludeCompletion()
        {
            _syncContext.Conclude();
        }
    }
}
