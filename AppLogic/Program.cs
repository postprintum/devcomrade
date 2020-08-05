// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using AppLogic.Presenter;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: InternalsVisibleTo("Tests")]
[assembly: InternalsVisibleTo("DevComrade")]
[assembly: AssemblyCopyright("Copyright (c) 2020 Postprintum Pty Ltd")]

namespace AppLogic
{
    internal static partial class Program
    {
        // cancellation of the RunAsync event loop
        private static CancellationTokenSource RuntimeCts { get; } = new CancellationTokenSource();

        private static void Stop() => RuntimeCts.Cancel();

        private static async void RunAsync()
        {
            // we're just a little sys tray app without a main window
            try
            {
                using var container = new HotkeyHandlerHost(RuntimeCts.Token);
                await container.AsTask();
            }
            catch (Exception ex)
            {
                if (ex.IsOperationCanceled())
                {
                    // absorb cancellations
                    Trace.WriteLine(ex.Message);
                }
                else
                {
                    // handle here or re-throw and threadExceptionHandler will handle it
                    ObserveError(ex);
                }
            }
            finally
            {
                Application.Exit();
            }
        }

        private static void ObserveError(Exception ex)
        {
            // can use DbgView to view the trace: https://live.sysinternals.com/Dbgview.exe
            Trace.TraceError(ex.ToString());
            MessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }

        private static void ThreadExceptionHandler(object s, System.Threading.ThreadExceptionEventArgs e)
        {
            ObserveError(e.Exception);
            Stop();
        }

        private static readonly Guid mutexGuid = new Guid(
            0xe7a2ee5a, 0xc826, 0x4152, 0x9d, 0x0, 0x20, 0xfb, 0x19, 0x99, 0xdd, 0x9a);

        private static Mutex CreateAppMutex()
        {
            var mutex = new Mutex(false, mutexGuid.ToString());
            var mutexSecurity = new MutexSecurity();
            var allowEveryoneRule = new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl, AccessControlType.Allow);
            mutexSecurity.AddAccessRule(allowEveryoneRule);
            mutex.SetAccessControl(mutexSecurity);
            return mutex;
        }

        private static void Run()
        {
            WindowsFormsSynchronizationContext.AutoInstall = false;
            Utilities.EnableMenuShortcutsUnderlining();

            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            using (var context = new WindowsFormsSynchronizationContext())
            {
                SynchronizationContext.SetSynchronizationContext(context);
                Application.ThreadException += ThreadExceptionHandler;
                try
                {
                    Environment.ExitCode = 0;
                    context.Post(_ => RunAsync(), null);
                    Application.Run();
                }
                catch (Exception ex)
                {
                    ObserveError(ex);
                }
                finally
                {
                    Application.ThreadException -= ThreadExceptionHandler;
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // make sure we don't run multiple instances
            using var mutex = CreateAppMutex();
            if (!mutex.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Trace.WriteLine("Another instance is already running.");
                Environment.ExitCode = 1;
            }
            else
            {
                // acquired the mutex
                try
                {
                    Run();
                    Environment.ExitCode = 0;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}
