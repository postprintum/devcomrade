// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Helpers
{
    internal static partial class Diagnostics
    {
        /// <summary>
        /// Double-click the output in Visual Studio Output Window to go to that source file and line
        /// </summary>
        [Conditional("DEBUG")]
        public static void Log(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "")
        {
            // trim trailing new line characters
            var trimmedMessage = message.TrimEnd(Environment.NewLine.ToCharArray());
            Debug.WriteLine($"{filePath}({lineNumber}): {trimmedMessage}");
        }

        [Conditional("DEBUG")]
        public static void LogMethodName(
            [CallerMemberName] string callerName = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerFilePath] string filePath = "")
        {
            // trim trailing new line characters
            Debug.WriteLine($"{filePath}({lineNumber}): {callerName}");
        }

        public static bool IsAdmin()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static string GetExecutablePath()
        {
            using var currentProcess = Process.GetCurrentProcess();
            return currentProcess!.MainModule!.FileName!;
        }

        public static void ShellExecute(string path)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = path
            };
            using var process = Process.Start(startInfo);
        }

        public static void StartProcess(string fileName)
        {
            using var process = Process.Start(fileName);
        }

#if DEBUG
        /// <summary>
        /// Pause execution until both left and right control keys are pressed and depressed
        /// </summary>
        public static async ValueTask PauseUntilLeftRightCtrlClickedAsync(CancellationToken token = default)
        {
            // wait for de-pressed state
            await WaitForLeftRightControlStateAsync(pressed: false, token);
            // wait for pressed state
            await WaitForLeftRightControlStateAsync(pressed: true, token);
            // wait for de-pressed state again
            await WaitForLeftRightControlStateAsync(pressed: false, token);
        }

        private async static ValueTask<(bool left, bool right)> GetLeftRightCtrlStateAsync(uint delay, CancellationToken token)
        {
            // this is used purely for debugging and so we utilize 
            // native CreateTimerQueueTimer with WT_EXECUTEINPERSISTENTTHREAD 
            // for keyboard polling here, because we don't want 
            // to pollute ThreadPool threads with AttachThreadInput other user32 calls
            var tcs = new TaskCompletionSource<(bool, bool)>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var rego = token.Register(() => tcs.SetCanceled(), useSynchronizationContext: false);
            WinApi.WaitOrTimerCallbackProc timerCallback = delegate
            {
                try
                {
                    // attach the to the foreground thread to read the keyboard status
                    var currentThread = WinApi.GetCurrentThreadId();
                    var foregroundWindow = WinApi.GetForegroundWindow();
                    var foregroundThread = WinApi.GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcess);

                    var attached = WinApi.AttachThreadInput(foregroundThread, currentThread, true);
                    try
                    {
                        var leftCtrl = (WinApi.GetAsyncKeyState(WinApi.VK_LCONTROL) & 0x8000) != 0;
                        var rightCtrl = (WinApi.GetAsyncKeyState(WinApi.VK_RCONTROL) & 0x8000) != 0;
                        tcs.TrySetResult((leftCtrl, rightCtrl));
                    }
                    finally
                    {
                        if (attached)
                        {
                            WinApi.AttachThreadInput(foregroundThread, currentThread, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            var gcHandle = GCHandle.Alloc(timerCallback);
            IntPtr timerHandle = IntPtr.Zero;
            try
            {
                if (!WinApi.CreateTimerQueueTimer(
                        out timerHandle,
                        IntPtr.Zero,
                        timerCallback,
                        IntPtr.Zero, delay, 0,
                        (UIntPtr)(WinApi.WT_EXECUTEINPERSISTENTTHREAD | WinApi.WT_EXECUTEONLYONCE)))
                {
                    throw WinUtils.CreateExceptionFromLastWin32Error();
                }

                return await tcs.Task;
            }
            finally
            {
                if (timerHandle != IntPtr.Zero)
                {
                    WinApi.DeleteTimerQueueTimer(IntPtr.Zero, timerHandle, IntPtr.Zero);
                }
                gcHandle.Free();
            }
        }

        private static async ValueTask WaitForLeftRightControlStateAsync(bool pressed, CancellationToken token)
        {
            const int interval = 100;
            while (true)
            {
                (bool leftCtrl, bool rightCtrl) = await GetLeftRightCtrlStateAsync(interval, token);
                if (leftCtrl == pressed && rightCtrl == pressed)
                {
                    break;
                }
            }
        }
#endif
    }
}
