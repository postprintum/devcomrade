// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using AppLogic.Models;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
            var text = GetClipboardText()
                .NormalizeLineEndings()
                .TrimTrailingEmptyLines()
                .TrimEnd();

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
                .NormalizeLineEndings()
                .TrimTrailingEmptyLines()
                .ConvertToSingleLine();

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
                .NormalizeLineEndings()
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
                .NormalizeLineEndings()
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
        public Task PasteToNotepad(Hotkey _, CancellationToken token)
        {
            Host.ShowNotepad(GetClipboardText());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Opens a URL from clipboard, merging lines and removing trailing blank spaces
        /// </summary>
        [HotkeyHandler]
        public Task OpenUrl(Hotkey _, CancellationToken token)
        {
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
            return Task.CompletedTask;
        }

        /// <summary>
        /// Run Windows Terminal at the current folder
        /// </summary>
        [HotkeyHandler]
        public Task RunWindowsTerminal(Hotkey _, CancellationToken token)
        {
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
            return Task.CompletedTask;
        }

        /// <summary>
        /// Run VS Code at the current folder
        /// </summary>
        [HotkeyHandler]
        public Task RunVSCode(Hotkey _, CancellationToken token)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                Arguments = "/c code .",
                FileName = Environment.ExpandEnvironmentVariables("%ComSpec%"),
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            using var process = Process.Start(startInfo);
            Host.PlayNotificationSound();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Show the main menu
        /// </summary>
        [HotkeyHandler]
        public Task ShowMenu(Hotkey _, CancellationToken token)
        {
            Host.ShowMenu();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoke Windows' PresentationSettings.exe
        /// </summary>
        [HotkeyHandler]
        public Task PresentationSettings(Hotkey _, CancellationToken token)
        {
            Diagnostics.StartProcess("PresentationSettings.exe");

            Host.PlayNotificationSound();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Show the internal Notepad
        /// </summary>
        [HotkeyHandler]
        public Task OpenNotepad(Hotkey _, CancellationToken token)
        {
            Host.ShowNotepad(null);
            return Task.CompletedTask;
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
