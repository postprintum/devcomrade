#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Helpers
{
    internal class ClipboardAccess
    {
        public static bool IsClipboardError(Exception ex)
        {
            return ex is ExternalException && ex.HResult == WinApi.CLIPBRD_E_CANT_OPEN;
        }

        /// <summary>
        /// Querying or setting clipboard data can fail, as other applications can 
        /// be locking the clipboard.
        /// Polling with OpenClipboard/CloseClipboard helps solving this. 
        /// It's OK to pass IntPtr.Zero for hwnd
        /// </summary>
        public static async Task EnsureAsync(
            IntPtr hwnd, 
            int interval,
            CancellationToken token)
        {
            while (!WinApi.OpenClipboard(hwnd))
            {
                await Task.Delay(interval, token);
            }
            WinApi.CloseClipboard();
            token.ThrowIfCancellationRequested();
        }
    }
}
