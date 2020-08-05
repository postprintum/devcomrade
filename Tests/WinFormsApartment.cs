// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tests
{
    /// <summary>
    /// Test WinFroms stuff on a dedicated thread with proper WindowsFormsSynchronizationContext
    /// </summary>
    public class WinFormsApartment : AsyncApartmentBase
    {
        private readonly Thread _thread; // an STA thread for WinForms

        public override Task Completion { get; }

        protected override TaskScheduler TaskScheduler { get; }

        public override bool? AnyBackgroundOperation => null;

        /// <summary>MessageLoopApartment constructor</summary>
        public WinFormsApartment()
        {
            var startTcs = new TaskCompletionSource<TaskScheduler>();

            // start an STA thread with WindowsFormsSynchronizationContext 
            // and gets a task scheduler for it
            void threadStart()
            {
                try
                {
                    WindowsFormsSynchronizationContext.AutoInstall = false;
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    using (var context = new WindowsFormsSynchronizationContext())
                    {
                        SynchronizationContext.SetSynchronizationContext(context);
                        Application.ThreadException += ThreadExceptionHandler;
                        startTcs.TrySetResult(TaskScheduler.FromCurrentSynchronizationContext());
                        try
                        {
                            using (CreateAsyncScope())
                            {
                                Application.Run();
                            }
                        }
                        catch (Exception ex)
                        {
                            AddException(ex);
                        }
                        finally
                        {
                            Application.ThreadException -= ThreadExceptionHandler;
                            SynchronizationContext.SetSynchronizationContext(null);
                            TrySetCompletion();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!startTcs.TrySetException(ex))
                        throw;
                }
            }

            Task waitForCompletionAsync() => 
                GetCompletionTask().ContinueWith(
                    anteTask => { _thread.Join(); return anteTask; },
                    cancellationToken: CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    scheduler: TaskScheduler.Default).Unwrap();

            _thread = new Thread(threadStart);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();

            this.TaskScheduler = startTcs.Task.Result;
            this.Completion = waitForCompletionAsync();
        }

        protected override void ConcludeCompletion()
        {
            if (_thread.IsAlive)
            {
                Task.Factory.StartNew(
                    () => Application.ExitThread(),
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    this.TaskScheduler).Observe();
            }
        }

        private void ThreadExceptionHandler(object s, System.Threading.ThreadExceptionEventArgs e)
        {
            AddException(e.Exception);
        }
    }
}
