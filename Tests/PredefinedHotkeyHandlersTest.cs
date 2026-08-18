// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information.
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Models;
using AppLogic.Presenter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    /// <summary>
    /// Hotkeys in the .config file are matched to handler methods by name, using
    /// reflection. A name which matches nothing is skipped without any error, so
    /// these tests guard the names the shipped App.config depends on.
    /// </summary>
    [TestClass]
    public class PredefinedHotkeyHandlersTest
    {
        /// <summary>
        /// Records what a handler feeds to the active window
        /// </summary>
        private class StubHost : IHotkeyHandlerHost
        {
            public List<string> FedText { get; } = new List<string>();
            public int NotificationsPlayed { get; private set; }

            public string ClipboardText { get; set; } = string.Empty;

            public int TabSize => 2;
            public bool ClipboardContainsText() => ClipboardText.Length > 0;
            public string GetClipboardText() => ClipboardText;
            public void ClearClipboard() { }
            public void SetClipboardText(string text) { }
            public void SetClipboardDataObject(object data) { }
            public void PlayNotificationSound() => NotificationsPlayed++;
            public void ShowMenu() { }
            public Task ShowNotepad(string? text) => Task.CompletedTask;

            public Task FeedTextAsync(string text, CancellationToken token)
            {
                FedText.Add(text);
                return Task.CompletedTask;
            }
        }

        private static bool TryGetHandler(
            string hotkeyName, StubHost host, out HotkeyHandlerCallback? callback)
        {
            IHotkeyHandlerProvider provider = new PredefinedHotkeyHandlers(host);
            return provider.CanHandle(new Hotkey { Name = hotkeyName }, out callback);
        }

        /// <summary>
        /// Every hotkey name used by the shipped DevComrade/App.config
        /// </summary>
        private static IEnumerable<string> GetShippedHotkeyNames()
        {
            yield return "PasteAsSingleLine";
            yield return "PasteShellCommandAsSingleLine";
            yield return "PasteUnformatted";
            yield return "PasteAsNumber";
            yield return "PasteToNotepad";
            yield return "PasteUnindented";
            yield return "PasteUnindentedUntabified";
            yield return "ConvertToPreformattedHtml";
            yield return "OpenNotepad";
            yield return "OpenUrl";
            yield return "RunVSCode";
            yield return "RunWindowsTerminal";
            yield return "RunWindowsTerminalAsAdmin";
            yield return "PresentationSettings";
            yield return "ShowMenu";
            yield return "InsertGuid";
        }

        [TestMethod]
        public void Test_every_hotkey_name_in_the_shipped_config_has_a_handler()
        {
            var host = new StubHost();
            foreach (var name in GetShippedHotkeyNames())
            {
                Assert.IsTrue(
                    TryGetHandler(name, host, out _),
                    $"No handler found for the \"{name}\" hotkey, it would be silently ignored");
            }
        }

        [TestMethod]
        public void Test_an_unknown_hotkey_name_is_not_handled()
        {
            // e.g. a leftover scriptlet from an older roaming config
            Assert.IsFalse(TryGetHandler("SomeRemovedScriptlet", new StubHost(), out _));
        }

        [TestMethod]
        public async Task Test_PasteShellCommandAsSingleLine_merges_line_continuations()
        {
            var host = new StubHost
            {
                // \ and ` continuations, including stray whitespace after the \
                ClipboardText = "curl -X POST \\ \r\n  -H \"a: b\" `\r\n  https://example.com\r\n"
            };
            Assert.IsTrue(TryGetHandler("PasteShellCommandAsSingleLine", host, out var callback));

            await callback!(
                new Hotkey { Name = "PasteShellCommandAsSingleLine" }, CancellationToken.None);

            Assert.AreEqual(1, host.FedText.Count);
            Assert.AreEqual("curl -X POST -H \"a: b\" https://example.com", host.FedText[0]);
            Assert.AreEqual(1, host.NotificationsPlayed);
        }

        [TestMethod]
        public async Task Test_InsertGuid_feeds_a_new_guid_in_the_expected_format()
        {
            var host = new StubHost();
            Assert.IsTrue(TryGetHandler("InsertGuid", host, out var callback));

            await callback!(new Hotkey { Name = "InsertGuid" }, CancellationToken.None);

            Assert.AreEqual(1, host.FedText.Count);
            var text = host.FedText[0];

            // the same "B" format, upper case, as the scriptlet it replaced
            Assert.IsTrue(Guid.TryParse(text, out var guid), $"not a guid: {text}");
            Assert.AreEqual(guid.ToString("B").ToUpper(), text);
            Assert.IsTrue(text.StartsWith("{") && text.EndsWith("}"), text);
            Assert.AreEqual(text.ToUpper(), text, "expected upper case");

            Assert.AreEqual(1, host.NotificationsPlayed);
        }

        [TestMethod]
        public async Task Test_InsertGuid_feeds_a_different_guid_every_time()
        {
            var host = new StubHost();
            Assert.IsTrue(TryGetHandler("InsertGuid", host, out var callback));

            var hotkey = new Hotkey { Name = "InsertGuid" };
            await callback!(hotkey, CancellationToken.None);
            await callback!(hotkey, CancellationToken.None);

            Assert.AreEqual(2, host.FedText.Count);
            Assert.AreNotEqual(host.FedText[0], host.FedText[1]);
        }
    }
}
