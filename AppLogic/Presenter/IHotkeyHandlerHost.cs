// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Presenter
{
    /// <summary>
    /// Host interface for hotkey handlers
    /// </summary>
    /// 
    public interface IHotkeyHandlerHost
    {
        int TabSize { get; }
        bool ClipboardContainsText();
        string GetClipboardText();
        void ClearClipboard();
        void SetClipboardText(string text);
        Task FeedTextAsync(string text, CancellationToken token);
        void PlayNotificationSound();
        void ShowMenu();
        Task ShowNotepad(string? text);
    }
}
