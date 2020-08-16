// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tests
{
    [TestClass]
    public class KeyboardInputTest
    {
        const int INPUT_IDLE_CHECK_INTERVAL = 100;
        const string TEXT_TO_FEED = "This is an example\nof multiline\ntext";

        static KeyboardInputTest ()
        {
            WinApi.SetProcessDpiAwareness(WinApi.PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
        }

        private enum ForegroundEvents
        {
            Ready,
            TextReceived,
            Finish
        }

        private enum BackgroundEvents
        {
            Ready,
            TextSent,
            Finish
        }

        /// <summary>
        /// A foreground test workflow that creates a UI form
        /// </summary>
        private static async IAsyncEnumerable<(ForegroundEvents, object)> ForegroundCoroutine(
            ICoroutineProxy<(BackgroundEvents, object)> backgroundCoroutineProxy,
            [EnumeratorCancellation] CancellationToken token)
        {
            Assert.IsInstanceOfType(SynchronizationContext.Current, typeof(WindowsFormsSynchronizationContext));

            // create a test form with TextBox inside
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            using var form = new Form
            {
                Text = nameof(KeyboardInputTest),
                Left = 10,
                Top = 10,
                Width = 640,
                Height = 480,
                ShowInTaskbar = false
            };

            using var formClosedHandlerScope = EventHandlerScope<FormClosedEventHandler>.Create(
                (s, e) => cts.Cancel(),
                handler => form.FormClosed += handler,
                handler => form.FormClosed -= handler);

            // add a textbox 
            var textBox = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            form.Controls.Add(textBox);
            form.Show();

            // show
            form.Activate();
            textBox.Focus();

            // coordinate further execution steps with the background coroutine
            await using var backgroundCoroutine =
                await backgroundCoroutineProxy.AsAsyncEnumerator(cts.Token);

            // notify the background couroutine that we're ready
            yield return (ForegroundEvents.Ready, DBNull.Value);

            // await for the background couroutine to also be ready
            var (foregroundEvent, _) = await backgroundCoroutine.GetNextAsync(cts.Token);
            Assert.IsTrue(foregroundEvent == BackgroundEvents.Ready);

            // await for the background couroutine to have fed some keystrokes
            (foregroundEvent, _) = await backgroundCoroutine.GetNextAsync(cts.Token);
            Assert.IsTrue(foregroundEvent == BackgroundEvents.TextSent);

            // await for idle input
            await InputHelpers.InputYield(delay: INPUT_IDLE_CHECK_INTERVAL, token: cts.Token);

            // notify the background couroutine about the text we've actually received
            var text = textBox.Text.Replace(Environment.NewLine, "\n");
            yield return (ForegroundEvents.TextReceived, text);
        }

        /// <summary>
        /// A background test workflow that sends keystrokes
        /// </summary>
        private static async IAsyncEnumerable<(BackgroundEvents, object)> BackgroundCoroutine(
            ICoroutineProxy<(ForegroundEvents, object)> foregroundCoroutineProxy,
            [EnumeratorCancellation] CancellationToken token)
        {
            Assert.IsTrue(SynchronizationContext.Current is WindowsFormsSynchronizationContext);

            await using var foregroundCoroutine = await foregroundCoroutineProxy.AsAsyncEnumerator(token);

            // notify the foreground couroutine that we're ready
            yield return (BackgroundEvents.Ready, DBNull.Value);

            // await for the foreground couroutine to also be ready
            var (foregroundEvent, _) = await foregroundCoroutine.GetNextAsync(token);
            Assert.IsTrue(foregroundEvent == ForegroundEvents.Ready);

            // feed some text to the foregroudn window
            using var threadInputScope = AttachedThreadInputScope.Create();
            Assert.IsTrue(threadInputScope.IsAttached);

            using (WaitCursorScope.Create())
            {
                await KeyboardInput.WaitForAllKeysReleasedAsync(token);
                await KeyboardInput.FeedTextAsync(TEXT_TO_FEED, token);
            }

            // notify the foreground couroutine that we've been fed some text
            yield return (BackgroundEvents.TextSent, DBNull.Value);

            // await for the foreground couroutine to reply with the text
            object text;
            (foregroundEvent, text) = await foregroundCoroutine.GetNextAsync(token);
            Assert.IsTrue(foregroundEvent == ForegroundEvents.TextReceived);
            Assert.AreEqual(text, TEXT_TO_FEED);
        }

        [TestMethod]
        public async Task Feed_text_to_TextBox_and_verify_it_was_consumed()
        {
            using var cts = new CancellationTokenSource(); // TODO: test cancellation

            var foregroundCoroutineProxy = new CoroutineProxy<(ForegroundEvents, object)>();
            var backgroundCoroutineProxy = new CoroutineProxy<(BackgroundEvents, object)>();

            await using var foregroundApartment = new WinFormsApartment();
            await using var backroundApartment = new WinFormsApartment();

            // start both coroutines, each in on its own WinForms thread

            var foregroundTask = foregroundCoroutineProxy.Run(
                foregroundApartment, 
                token => ForegroundCoroutine(backgroundCoroutineProxy, token),
                cts.Token);

            var backgroundTask = backgroundCoroutineProxy.Run(
                backroundApartment,
                token => BackgroundCoroutine(foregroundCoroutineProxy, token),
                cts.Token);

            await Task.WhenAll(foregroundTask, backgroundTask).WithAggregatedExceptions();
        }
    }
}
