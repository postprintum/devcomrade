// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using AppLogic.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Presenter
{
    internal class PredefinedHotkeyHandlers: IHotkeyHandlerProvider
    {
        /// <summary>
        /// Predefined handlers are decorated with [HotkeyHandler]
        /// </summary>
        public class HotkeyHandlerAttribute : Attribute
        {
        }

        public IHotkeyHandlerHost Host { get; }

        public PredefinedHotkeyHandlers(IHotkeyHandlerHost host)
        {
            Host = host;
        }

        private string GetClipboardText()
        {
            if (!Host.ClipboardContainsText())
            {
                return String.Empty;
            }
            return Host.GetClipboardText();
        }

        /// <summary>
        /// Remove formatting, trailing CR/LFs and paste by simulating typing
        /// </summary>
        [HotkeyHandler]
        public async Task PasteUnformatted(Hotkey _, CancellationToken token)
        {
            var text = GetClipboardText().UnixifyLineEndings();

            await Host.FeedTextAsync(text, token);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Remove formatting and paste as single line
        /// </summary>
        [HotkeyHandler]
        public async Task PasteAsSingleLine(Hotkey _, CancellationToken token)
        {
            var text = GetClipboardText()
                .UnixifyLineEndings()
                .TrimTrailingEmptyLines()
                .ConvertToSingleLine();

            await Host.FeedTextAsync(text, token);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Remove formatting, spaces and paste as single line
        /// </summary>
        [HotkeyHandler]
        public async Task PasteAsNumber(Hotkey _, CancellationToken token)
        {
            var text = GetClipboardText().Where(c => Char.IsDigit(c) || c == '.').AsString();

            await Host.FeedTextAsync(text, token);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Remove formatting and paste unindented
        /// </summary>
        [HotkeyHandler]
        public async Task PasteUnindented(Hotkey _, CancellationToken token)
        {
            var text = GetClipboardText()
                .UnixifyLineEndings()
                .TrimTrailingEmptyLines()
                .Unindent();

            await Host.FeedTextAsync(text, token);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Remove formatting, CR/LFs, tabs and paste by simulating typing
        /// </summary>
        [HotkeyHandler]
        public async Task PasteUnindentedUntabified(Hotkey _, CancellationToken token)
        {
            var tabSize = Host.TabSize;

            var text = GetClipboardText()
                .UnixifyLineEndings()
                .TrimTrailingEmptyLines()
                .TabifyStart(tabSize)
                .UntabifyStart(tabSize)
                .Unindent();

            await Host.FeedTextAsync(text, token);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Paste to the internal Notepad
        /// </summary>
        [HotkeyHandler]
        public async Task PasteToNotepad(Hotkey _, CancellationToken token)
        {
            await Host.ShowNotepad(GetClipboardText());
        }

        /// <summary>
        /// Opens a URL from clipboard, merging lines and removing trailing blank spaces
        /// </summary>
        [HotkeyHandler]
        public async Task OpenUrl(Hotkey _, CancellationToken token)
        {
            await Task.CompletedTask;

            var text = Regex.Replace(
                Host.GetClipboardText(), @"\r\n", String.Empty,
                RegexOptions.Singleline);

            // match the url
            var urlRegex = new Regex(
                @"\bhttp(s)?://([\w\?%&=/\.\-])+", RegexOptions.Singleline);

            var match = urlRegex.Match(text);
            if (match.Success)
            {
                Diagnostics.ShellExecute(match.Value);
            }

            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Run Windows Terminal at the current folder
        /// </summary>
        [HotkeyHandler]
        public async Task RunWindowsTerminal(Hotkey _, CancellationToken token)
        {
            if (await WinUtils.ActivateProcess("WINDOWSTERMINAL", token))
            {
                return;
            }
                
            var currentFolder = Directory.GetCurrentDirectory();
            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Maximized,
                Arguments = $"-d \"{currentFolder}\"",
                FileName = "wt.exe",
                WorkingDirectory = currentFolder
               
            };
            using var process = Process.Start(startInfo);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Run Windows Terminal as Admin
        /// </summary>
        [HotkeyHandler]
        public async Task RunWindowsTerminalAsAdmin(Hotkey _, CancellationToken token)
        {
            if (Diagnostics.IsAdmin())
            {
                await RunWindowsTerminal(_, token);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                Arguments = $"-d \"{Directory.GetCurrentDirectory()}\"",
                FileName = "wt.exe",
                Verb = "runas"
            };
            try
            {
                using var process = Process.Start(startInfo);
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != WinApi.ERROR_CANCELLED)
                {
                    throw;
                }
            }
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Run VS Code at the current folder
        /// </summary>
        [HotkeyHandler]
        public async Task RunVSCode(Hotkey _, CancellationToken token)
        {
            if (await WinUtils.ActivateProcess("CODE", token))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                Arguments = "/c code .",
                FileName = Environment.ExpandEnvironmentVariables("%ComSpec%"),
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            using var process = Process.Start(startInfo);
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Show the main menu
        /// </summary>
        [HotkeyHandler]
        public async Task ShowMenu(Hotkey _, CancellationToken token)
        {
            await Task.Yield();
            Host.ShowMenu();
        }

        /// <summary>
        /// Invoke Windows' PresentationSettings.exe
        /// </summary>
        [HotkeyHandler]
        public async Task PresentationSettings(Hotkey _, CancellationToken token)
        {
            await Task.CompletedTask;
            Diagnostics.StartProcess("PresentationSettings.exe");
            Host.PlayNotificationSound();
        }

        /// <summary>
        /// Show the internal Notepad
        /// </summary>
        [HotkeyHandler]
        public async Task OpenNotepad(Hotkey _, CancellationToken token)
        {
            await Host.ShowNotepad(null);
        }


        /// <summary>
        /// Match a HotkeyHandlerCallback to hotkey.Name 
        /// </summary>
        bool IHotkeyHandlerProvider.CanHandle(Hotkey hotkey, [NotNullWhen(true)] out HotkeyHandlerCallback? callback)
        {
            // try to match hotkey.Name to a method with [HotkeyHandler] attribute
            var methodInfo = this.GetType().GetMethod(hotkey.Name, 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (methodInfo?.GetCustomAttribute(typeof(HotkeyHandlerAttribute), true) != null)
            {
                callback = methodInfo.CreateDelegate<HotkeyHandlerCallback>(this);
                return true;
            }

            callback = default;
            return false;
        }
    }
}
