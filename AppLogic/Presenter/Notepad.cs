// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppLogic.Presenter
{
    /// <summary>
    /// A plain text editor with multi-level Undo/Redo
    /// For now, we the legacy WebBrowser for that and will replace it 
    /// with WebView2 when it is available
    /// </summary>
    [System.ComponentModel.DesignerCategory("")]
    internal class Notepad : Form
    {
        private WebBrowser Browser { get; }

        private readonly Task _initTask;

        public event EventHandler? ControlEnterPressed;

        internal void OnControlEnterPressed()
        {
            this.ControlEnterPressed?.Invoke(this, new EventArgs());
        }

        private Lazy<IBrowser> BrowserInstance => new Lazy<IBrowser>(() =>
            this.Browser.ActiveXInstance as IBrowser ??
                throw new InvalidComObjectException(nameof(BrowserInstance)),
            isThreadSafe: false);

        private Lazy<IDocument> Document => new Lazy<IDocument>(() =>
            this.BrowserInstance.Value.Document ??
               throw new InvalidComObjectException(nameof(Document)),
            isThreadSafe: false);

        private Lazy<ITextArea> EditorElement => new Lazy<ITextArea>(() =>
            (this.Document.Value.getElementById("editor") as ITextArea) ??
                throw new InvalidComObjectException(nameof(EditorElement)),
            isThreadSafe: false);

        public bool IsReady =>
            _initTask.IsCompleted &&
            this.Browser.ReadyState == WebBrowserReadyState.Complete;

        static Notepad()
        {
            // enable HTML5, more info: https://stackoverflow.com/a/18333982/1768303
            if (LicenseManager.UsageMode != LicenseUsageMode.Runtime)
            {
                return;
            }
            SetWebBrowserFeature("FEATURE_BROWSER_EMULATION", 11000);
            SetWebBrowserFeature("FEATURE_SPELLCHECKING", 1);
            SetWebBrowserFeature("FEATURE_DOMSTORAGE", 1);
            SetWebBrowserFeature("FEATURE_GPU_RENDERING", 1);
            SetWebBrowserFeature("FEATURE_ENABLE_CLIPCHILDREN_OPTIMIZATION", 1);
            SetWebBrowserFeature("FEATURE_DISABLE_NAVIGATION_SOUNDS", 1);
            SetWebBrowserFeature("FEATURE_WEBOC_DOCUMENT_ZOOM", 1);
            SetWebBrowserFeature("FEATURE_WEBOC_MOVESIZECHILD", 1);
            SetWebBrowserFeature("FEATURE_96DPI_PIXEL", 1);
            SetWebBrowserFeature("FEATURE_LOCALMACHINE_LOCKDOWN", 1);
        }

        private static void SetWebBrowserFeature(string feature, uint value)
        {
            using var process = Process.GetCurrentProcess();
            using var key = Registry.CurrentUser.CreateSubKey(
                String.Concat(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\", feature),
                RegistryKeyPermissionCheck.ReadWriteSubTree);
            var appName = Path.GetFileName(process.MainModule.FileName);
            key.SetValue(appName, (UInt32)value, RegistryValueKind.DWord);
        }

        public Notepad(CancellationToken token)
        {
            this.Browser = new CustomWebBrowser(this)
            {
                Dock = DockStyle.Fill,
                AllowWebBrowserDrop = false,
                ScriptErrorsSuppressed = true,
                ScrollBarsEnabled = false,
                AllowNavigation = false,
                IsWebBrowserContextMenuEnabled = true,
                WebBrowserShortcutsEnabled = true
            };

            this.Text = $"{Application.ProductName} Notepad";
            this.ShowInTaskbar = false;
            this.Padding = new Padding(3);
            this.Icon = Icon.ExtractAssociatedIcon(Diagnostics.GetExecutablePath());

            var workingArea = Screen.PrimaryScreen.WorkingArea;
            this.StartPosition = FormStartPosition.Manual;
            this.Width = workingArea.Width / 2;
            this.Height = workingArea.Height / 2;
            this.Left = workingArea.Left + (workingArea.Width - this.Width) / 2;
            this.Top = workingArea.Top + (workingArea.Height - this.Height) / 2;

            this.KeyPreview = true;

            this.Controls.Add(this.Browser);

            async void handleFocus(object? s, EventArgs e)
            {
                await Task.Yield();
                if (!this.IsDisposed && this.Handle == WinApi.GetActiveWindow())
                {
                    this.FocusEditor();
                }
            }

            this.Activated += handleFocus;
            this.GotFocus += handleFocus;

            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };

            _initTask = LoadAsync(token);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Hide();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async Task LoadAsync(CancellationToken token)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var handlerScope = EventHandlerScope<WebBrowserDocumentCompletedEventHandler>.Create(
                (s, e) => tcs.TrySetResult(DBNull.Value),
                handler => this.Browser.DocumentCompleted += handler,
                handler => this.Browser.DocumentCompleted -= handler);

            using var rego = token.Register(() => tcs.TrySetCanceled());
            this.Browser.DocumentText = HTML_SOURCE;

            await tcs.Task;
        }

        public void FocusEditor()
        {
            if (this.IsReady)
            {
                this.Browser.Focus();
                this.EditorElement.Value.focus();
            }
        }

        public Task WaitForReadyAsync(CancellationToken token) =>
            _initTask.WithCancellation(token);

        private bool ExecCommand(string command, bool showUI, object value) =>
            this.IsReady ? this.Document.Value.execCommand(command, showUI, value) : false;

        public bool SelectAll()
        {
            if (!this.IsReady)
            {
                return false;
            }
            this.EditorElement.Value.createTextRange().select();
            return true;
        }

        public string? EditorText =>
            this.IsReady ? this.EditorElement.Value.value : null;

        public bool Paste(string? text)
        {
            if (!this.IsReady)
            {
                return false;
            }

            var range = this.EditorElement.Value.createTextRange();
            if (range.text != text)
            {
                range.text = text ?? String.Empty;

                range = this.EditorElement.Value.createTextRange();
                range.select();

                return true;
            }

            return false;
        }

        #region WebBrowser stuff

        private const string HTML_SOURCE = @"
        <!doctype html>
        <head>
            <style>
                html, body { width: 100%; height: 100% }
                body { border: 0; margin: 0; padding: 0; }
                textarea { 
                    width: 100%; height: 100%; overflow: auto; 
                    font-family: Consolas; font-size: 14px;
                    border: 0; margin: 0; padding: 0
                }
            </style>
        </head>
        <body>
            <textarea id='editor' spellcheck='false' wrap='off' tabIndex='1'></textarea>
        </body>
        ";

        private class CustomWebBrowser : WebBrowser
        {
            private readonly Notepad _parent;

            public CustomWebBrowser(Notepad parent)
            {
                _parent = parent;
            }

            protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
            {
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Enter)
                {
                    e.IsInputKey = true;
                    this._parent.OnControlEnterPressed();
                    return;
                }

                // ignore these keys
                if (e.Control &&
                    (e.KeyCode == Keys.N || e.KeyCode == Keys.L ||
                    e.KeyCode == Keys.O || e.KeyCode == Keys.P)
                    || e.KeyCode == Keys.F5)
                {
                    e.IsInputKey = true;
                    return;
                }

                base.OnPreviewKeyDown(e);
            }
        }

#pragma warning disable CS0618 // InterfaceIsIDispatch is obsolete
        private const string IID_IDispatch = "00020400-0000-0000-C000-000000000046";

        [ComVisible(true), Guid(IID_IDispatch), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IDocument
        {
            IElement getElementById(string id);
            bool execCommand(string command, bool showUI, object value);
            ISelection selection { get; }
        }

        [ComVisible(true), Guid(IID_IDispatch), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IElement
        {
            void focus();
        }

        [ComVisible(true), Guid(IID_IDispatch), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface ITextArea
        {
            void focus();
            string value { get; set; }
            IRange createTextRange();
        }

        [ComVisible(true), Guid(IID_IDispatch), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IBrowser
        {
            IDocument Document { get; }
            int ReadyState { get; }
        }

        [ComVisible(true), Guid(IID_IDispatch), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface ISelection
        {
            IRange createRange();
        }

        [ComVisible(true), Guid(IID_IDispatch), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IRange
        {
            string text { get; set; }
            void collapse(bool start);
            void select();
        }

#pragma warning restore CS0618 // Type or member is obsolete
        #endregion
    }
}
