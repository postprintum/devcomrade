// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Config;
using AppLogic.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLogic.Events;

namespace AppLogic.Presenter
{
    [System.ComponentModel.DesignerCategory("")]
    internal partial class HotkeyHandlerHost : Form,
        IMessageFilter,
        INotifyPropertyChanged,
        IHotkeyHandlerHost,
        IContainer,
        IEventTargetProp<ClipboardUpdateEventArgs>,
        IEventTargetProp<ControlClipboardMonitoringEventArgs>,
        IEventTargetHub
    {
        const int ASYNC_LOCK_TIMEOUT = 250;
        const int CLIPBOARD_MONITORING_DELAY = 100;

        // classes which provide hotkey handlers
        private int _hotkeyId = 0; // IDs for Win32 RegisterHotKey

        private int _menuActive = 0;

        private bool _updatingClipboard = false;

        // SemaphoreSlim as an async lock for hotkey handlers to avoid re-rentrancy
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1);

        // when this is signalled, the container's RunAsync exits
        private readonly CancellationTokenSource _cts;

        private readonly Container _componentContainer = new();

        // cancellation for RunAsync
        private CancellationToken Token => _cts.Token;

        // NB: named MainMenu rather than Menu, so it doesn't hide Form.Menu
        private ContextMenuStrip MainMenu => _menu.Value;

        private Notepad Notepad => _notepad.Value;

        // the task of RunAsync
        private Task Completion { get; }

        // for playing sound notifictions
        private readonly Lazy<SoundPlayer?> _soundPlayer;

        // all configured handlers, in config order. A name can appear more than
        // once here, when an action has more than one key combination
        private readonly List<HotkeyHandler> _handlers = new List<HotkeyHandler>();

        // map hotkey ID to handler
        private readonly Dictionary<int, HotkeyHandler> _handlersByHotkeyIdMap =
            new Dictionary<int, HotkeyHandler>();

        private readonly Lazy<ContextMenuStrip> _menu;

        private readonly Lazy<Notepad> _notepad;

        private readonly Lazy<ClipboardFormatMonitor> _clipboardFormatMonitor;

        private void Quit() => _cts.Cancel();

        private void OnEnterMenu() => _menuActive++;

        private void OnExitMenu() => _menuActive--;

        private bool IsMenuActive => _menuActive > 0;

        #region Events
        EventTarget<ClipboardUpdateEventArgs> IEventTargetProp<ClipboardUpdateEventArgs>.Value { get; init; } = new();

        EventTarget<ControlClipboardMonitoringEventArgs> IEventTargetProp<ControlClipboardMonitoringEventArgs>.Value { get; init; } = new();
        #endregion

        private ValueTask<IDisposable> WithLockAsync() =>
            Disposable.CreateAsync(
                () => _asyncLock.WaitAsync(this.Token),
                () => _asyncLock.Release());

        public event PropertyChangedEventHandler? PropertyChanged;

        public HotkeyHandlerHost(CancellationToken token): base()
        {
            this.ShowInTaskbar = false;
            CreateHandle();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _soundPlayer = new Lazy<SoundPlayer?>(CreateSoundPlayer, isThreadSafe: false);
            _menu = new Lazy<ContextMenuStrip>(CreateContextMenu, isThreadSafe: false);
            _notepad = new Lazy<Notepad>(CreateNotepad, isThreadSafe: false);
            _clipboardFormatMonitor = new Lazy<ClipboardFormatMonitor>(
                () => new ClipboardFormatMonitor(this), isThreadSafe: false);

            this.Completion = RunAsync();
        }

        // standard hotkey handler providers
        private static IEnumerable<Type> GetHotkeyHandlerProviders()
        {
            yield return typeof(PredefinedHotkeyHandlers);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Parent = WinApi.GetDesktopWindow();
                cp.Style = unchecked((int)WinApi.WS_POPUP);
                cp.ExStyle = unchecked((int)(WinApi.WS_EX_NOACTIVATE | WinApi.WS_EX_TOOLWINDOW));
                return cp;
            }
        }

        public void RaisePropertyChange([CallerMemberName] string propertyname = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        public Task AsTask()
        {
            return this.Completion;
        }

        private void RegisterWindowsHotkey(HotkeyHandler hotkeyHandler)
        {
            var hotkey = hotkeyHandler.Hotkey;
            if (!hotkey.HasHotkey)
            {
                throw new InvalidOperationException(nameof(RegisterWindowsHotkey));
            }

            if (WinApi.RegisterHotKey(IntPtr.Zero, ++_hotkeyId,
                hotkey.Mods!.Value | WinApi.MOD_NOREPEAT,
                hotkey.Vkey!.Value))
            {
                _handlersByHotkeyIdMap.Add(_hotkeyId, hotkeyHandler);
            }
            else
            {
                var error = WinUtils.CreateExceptionFromLastWin32Error();
                throw new WarningException($"{hotkeyHandler.Hotkey.Name}: {error.Message}", error);
            }
        }

        private void SetCurrentFolder()
        {
            if (Configuration.TryGetOption("currentFolder", out var folder))
            {
                folder = Environment.ExpandEnvironmentVariables(folder);
            }
            if (folder == null || !Directory.Exists(folder))
            {
                folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            Directory.SetCurrentDirectory(folder);
        }

        private void StartClipboardFormatMonitoring()
        {
            _clipboardFormatMonitor.Value.StartAsync().IgnoreCancellations();

            // process whatever is already in the clipboard
            this.Dispatch(this, new ClipboardUpdateEventArgs());
        }

        private void StopClipboardFormatMonitoring()
        {
            if (_clipboardFormatMonitor.IsValueCreated)
            {
                _clipboardFormatMonitor.Value.Stop();
            }
        }

        private void InitializeClipboardFormatMonitoring()
        {
            // the listener stays subscribed for the lifetime of the app;
            // it's the monitor itself which gets hooked and unhooked on demand
            this.AddListener<ClipboardUpdateEventArgs>((s, e) => HandleOnClipboardTextChangedAsync());

            if (this.IsFormattingRemovalEnabled)
            {
                StartClipboardFormatMonitoring();
            }

            async void HandleOnClipboardTextChangedAsync()
            {
                try
                {
                    await OnClipboardTextChangedAsync();
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException || ClipboardAccess.IsClipboardError(ex))
                    {
                        // absorb cancellations and clipboard errors
                        Trace.WriteLine(ex.Message);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            async Task OnClipboardTextChangedAsync()
            {
                // the enabled check also covers the race when the toggle is
                // switched off while ClipboardFormatMonitor.StartAsync is still
                // retrying and attaches the listener afterwards
                if (_updatingClipboard || !this.IsFormattingRemovalEnabled)
                {
                    return;
                }

                _updatingClipboard = true;
                try
                {
                    await ClipboardAccess.EnsureAsync(
                        IntPtr.Zero,
                        CLIPBOARD_MONITORING_DELAY,
                        this.Token);

                    if (!Clipboard.ContainsText())
                    {
                        return;
                    }

                    if (Clipboard.ContainsText(TextDataFormat.Html) ||
                        Clipboard.ContainsText(TextDataFormat.Rtf))
                    {
                        var text = String.Empty;
                        if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                        {
                            text = Clipboard.GetText(TextDataFormat.UnicodeText);
                        }
                        if (text.IsNullOrEmpty())
                        {
                            text = Clipboard.GetText(TextDataFormat.Text);
                        }
                        if (!text.IsNullOrEmpty())
                        {
                            Clipboard.SetText(text, TextDataFormat.UnicodeText);
                            await InputUtils.InputYield(delay: CLIPBOARD_MONITORING_DELAY, token: this.Token);
                        }
                    }
                }
                finally
                {
                    _updatingClipboard = false;
                }
            }
        }

        private void InitializeHotkeys()
        {
            // Roaming config gets precedence over Local
            var hotkeys = Configuration.GetHotkeys();

            // instantiate the known hotkey handler providers
            var providers = GetHotkeyHandlerProviders()
                .Select(type => Activator.CreateInstance(type, this))
                .OfType<IHotkeyHandlerProvider>().ToArray();

            foreach (var hotkey in hotkeys)
            {
                foreach (var provider in providers)
                {
                    if (provider.CanHandle(hotkey, out var callback))
                    {
                        var handler = new HotkeyHandler(hotkey, callback);
                        _handlers.Add(handler);
                        if (hotkey.HasHotkey)
                        {
                            RegisterWindowsHotkey(handler);
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            _cts.Dispose();

            foreach (var hotkeyId in _handlersByHotkeyIdMap.Keys)
            {
                WinApi.UnregisterHotKey(IntPtr.Zero, hotkeyId);
            }

            _handlers.Clear();
            _handlersByHotkeyIdMap.Clear();

            if (_notepad.IsValueCreated)
            {
                _notepad.Value.Dispose();
            }

            _componentContainer.Dispose();

            base.Dispose(disposing);
        }

        private SoundPlayer? CreateSoundPlayer()
        {
            if (!Configuration.GetOption("playNotificationSound", defaultValue: true))
            {
                return null;
            }

            if (!Configuration.TryGetOption("notifySound", out var soundPath))
            {
                return null;
            }

            SoundPlayer? soundPlayer = null;
            var filePath = Environment.ExpandEnvironmentVariables(soundPath);
            if (File.Exists(filePath))
            {
                soundPlayer = new SoundPlayer();
                try
                {
                    soundPlayer.SoundLocation = filePath;
                    this.Add(soundPlayer);
                }
                catch
                {
                    soundPlayer.Dispose();
                    throw;
                }
            }

            return soundPlayer;
        }

        /// <summary>
        /// Actions run in response to a hotkey or a menu click; a failing action
        /// must not bring the whole app down, so we report and carry on rather than
        /// letting the exception reach Program.ThreadExceptionHandler, which calls Stop()
        /// </summary>
        private static void ReportActionError(string actionName, Exception ex)
        {
            Trace.TraceError(ex.ToString());
            MessageBox.Show(
                $"\"{actionName}\" failed:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                Application.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private async Task HandleHotkeyAsync(HotkeyHandler hotkeyHandler)
        {
            while (!await _asyncLock.WaitAsync(ASYNC_LOCK_TIMEOUT, this.Token))
            {
                {
                    if (this.IsMenuActive)
                    {
                        this.MainMenu.Close(ToolStripDropDownCloseReason.Keyboard);
                    }
                    else
                    {
                        // discard this hotkey event, as we only allow
                        // one handler at a time to prevent re-entrancy
                        return;
                    }
                }
            }
            // try to get an instant async lock
            try
            {
                await InputUtils.TimerYield(token: this.Token);
                await hotkeyHandler.Callback(hotkeyHandler.Hotkey, this.Token);
            }
            catch (Exception ex) when (!ex.IsOperationCanceled())
            {
                // e.g. RunWindowsTerminal when wt.exe is not installed
                ReportActionError(hotkeyHandler.Hotkey.Name, ex);
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WinApi.WM_HOTKEY:
                    if (_handlersByHotkeyIdMap.TryGetValue((int)m.WParam, out var handler))
                    {
                        HandleHotkeyAsync(handler).IgnoreCancellations();
                        return true;
                    }
                    break;

                case WinApi.WM_QUIT:
                    Quit();
                    return true;

                default:
                    break;
            }
            return false;
        }

        #region Menu Handlers
        const string FEEDBACK_URL = "https://github.com/postprintum/devcomrade/issues";
        const string ABOUT_URL = "https://github.com/postprintum/devcomrade";

        private delegate void MenuItemEventHandler(object s, EventArgs e);

        private delegate void MenuItemSetUpdaterCallback(Action<bool> updater);

        private static void About(object? s, EventArgs e) => Diagnostics.ShellExecute(ABOUT_URL);

        private static void Feedback(object? s, EventArgs e) => Diagnostics.ShellExecute(FEEDBACK_URL);

        private static void EditLocalConfig(object? s, EventArgs e) =>
            Diagnostics.ShellExecute(Configuration.LocalConfigPath);

        private static void EditRoamingConfig(object? s, EventArgs e)
        {
            var path = Configuration.RoamingConfigPath;
            if (!File.Exists(path) || File.ReadAllText(path).IsNullOrWhiteSpace())
            {
                // copy local config to roaming config
                var folder = Path.GetDirectoryName(path);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder!);
                }
                File.WriteAllText(path, Configuration.GetDefaultRoamingConfig(), Encoding.UTF8);
            }
            Diagnostics.ShellExecute(path);
        }

        private void Restart(object? s, EventArgs e)
        {
            Diagnostics.StartProcess(Diagnostics.GetExecutablePath());
            Quit();
        }

        private void RestartAsAdmin(object? s, EventArgs e)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = Diagnostics.GetExecutablePath(),
                Verb = "runas"
            };
            try
            {
                using var process = Process.Start(startInfo);
                Quit();
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != WinApi.ERROR_CANCELLED)
                {
                    throw;
                }
            }
        }

        private void AutoStart(object? s, EventArgs e)
        {
            // s is ToolStripMenuItem menuItem
            this.IsAutorun = !this.IsAutorun;
        }

        private void ToggleFormattingRemoval(object? s, EventArgs e)
        {
            this.IsFormattingRemovalEnabled = !this.IsFormattingRemovalEnabled;
        }

        private void Exit(object? s, EventArgs e)
        {
            Quit();
        }

        private static (
            string,
            MenuItemEventHandler?,
            MenuItemSetUpdaterCallback?) GetSeparatorMenuItem()
        {
            return ("-", null, null);
        }

        #endregion

        /// <summary>
        /// Provide tray menu items
        /// </summary>
        private IEnumerable<(
            string,
            MenuItemEventHandler?,
            MenuItemSetUpdaterCallback?)> GetMenuItems()
        {
            // first add hotkey handlers which also have menuItem in the config file
            var handlers = _handlers
                .Where(handler => handler.Hotkey.MenuItem.IsNotNullNorWhiteSpace())
                .ToArray();

            if (handlers.Length > 0)
            {
                foreach (var handler in handlers)
                {
                    var hotkey = handler.Hotkey;
                    string menuItemText;
                    if (hotkey.HasHotkey)
                    {
                        var hotkeyTitle = WinUtils.GetHotkeyTitle(hotkey.Mods!.Value, hotkey.Vkey!.Value);
                        menuItemText = $"{hotkey.MenuItem}|{hotkeyTitle}";
                    }
                    else
                    {
                        menuItemText = hotkey.MenuItem!;
                    }

                    yield return (
                        menuItemText,
                        (s, e) => HandleHotkeyAsync(handler).IgnoreCancellations(),
                        null);

                    if (hotkey.AddSeparator)
                    {
                        yield return GetSeparatorMenuItem();
                    }
                }
                yield return GetSeparatorMenuItem();
            }

            yield return ("Auto Start", AutoStart, update => 
            {
                update(this.IsAutorun);
                this.PropertyChanged += (s, e) =>
                {
                    if (String.CompareOrdinal(e.PropertyName, nameof(IsAutorun)) == 0)
                    {
                        update(this.IsAutorun);
                    }
                };
            });

            yield return ("Remove Clipboard &Formatting", ToggleFormattingRemoval, update =>
            {
                update(this.IsFormattingRemovalEnabled);
                this.PropertyChanged += (s, e) =>
                {
                    if (String.CompareOrdinal(e.PropertyName, nameof(IsFormattingRemovalEnabled)) == 0)
                    {
                        update(this.IsFormattingRemovalEnabled);
                    }
                };
            });

            yield return ("Edit Local Config", EditLocalConfig, null);
            yield return ("Edit Roaming Config", EditRoamingConfig, null);
            yield return ("Restart", Restart, null);
            if (!Diagnostics.IsAdmin())
            {
                yield return ("Restart as Admin", RestartAsAdmin, null);
            }
            yield return GetSeparatorMenuItem();
            yield return ($"About {Application.ProductName}", About, null);
            yield return ("E&xit", Exit, null);
        }

        private EventHandler AsAsync(string itemText, MenuItemEventHandler? handler)
        {
            // we make all click handlers async because
            // we want the menu to be dismissed first
            void handle(object s, EventArgs e)
            {
                async Task handleAsync()
                {
                    await InputUtils.InputYield(token: this.Token);
                    try
                    {
                        handler?.Invoke(s, e);
                    }
                    catch (Exception ex) when (!ex.IsOperationCanceled())
                    {
                        // a failing menu command must not bring the app down
                        ReportActionError(itemText.Replace("&", String.Empty), ex);
                    }
                }
                handleAsync().IgnoreCancellations();
            }
            return handle!;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();
            contextMenu.Opened += (s, e) => OnEnterMenu();
            contextMenu.Closed += (s, e) => OnExitMenu();

            foreach (var (text, handler, setUpdater) in GetMenuItems())
            {
                if (text == "-")
                {
                    contextMenu.Items.Add(new ToolStripSeparator());
                }
                else
                {
                    var left = text;
                    var right = String.Empty;
                    var separator = text.LastIndexOf('|');
                    if (separator >= 0)
                    {
                        left = text.Substring(0, separator);
                        right = text.Substring(separator + 1);
                    }
                    var menuItem = new ToolStripMenuItem(left, image: null, AsAsync(left, handler));
                    menuItem.ShortcutKeyDisplayString = right;
                    setUpdater?.Invoke(value => menuItem.Checked = value);
                    contextMenu.Items.Add(menuItem);
                }
            }

            return contextMenu;
        }

        private Notepad CreateNotepad()
        {
            var notepad = new Notepad(this.Token);

            notepad.ControlEnterPressed += (s, e) =>
                SaveNotepadToClipboard().IgnoreCancellations();

            return notepad;

            async Task SaveNotepadToClipboard()
            {
                using var _lock = await WithLockAsync();
                var text = notepad!.EditorText;
                notepad!.Hide();
                if (text.IsNotNullNorEmpty())
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                }
            }
        }

        private NotifyIcon CreateTrayIconMenu()
        {
            var notifyIcon = new NotifyIcon(this)
            {
                Text = Application.ProductName,
                ContextMenuStrip = this.MainMenu,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Diagnostics.GetExecutablePath()),
            };

            void clicked(object? s, EventArgs e)
            {
                (this as IHotkeyHandlerHost).ShowMenu();
            }

            notifyIcon.Click += clicked;
            notifyIcon.DoubleClick += clicked;

            return notifyIcon;
        }

        // async entry point
        private async Task RunAsync()
        {
            SetCurrentFolder();
            InitializeHotkeys();
            InitializeClipboardFormatMonitoring();

            Application.AddMessageFilter(this);
            try
            {
                var trayIconMenu = CreateTrayIconMenu();
                this.Add(trayIconMenu);
                trayIconMenu.Visible = true;
                try
                {
                    // this infinte delay defines the async scope 
                    // for AddMessageFilter/RemoveMessageFilter
                    // the token is cancelled when the app exits
                    await Task.Delay(Timeout.Infinite, this.Token);
                }
                finally
                {
                    trayIconMenu.Visible = false;
                }
            }
            finally
            {
                Application.RemoveMessageFilter(this);
            }
        }

        bool IHotkeyHandlerHost.ClipboardContainsText()
        {
            return Clipboard.ContainsText();
        }

        string IHotkeyHandlerHost.GetClipboardText()
        {
            return Clipboard.GetText(TextDataFormat.UnicodeText);
        }

        private void UpdateClipboard(Action updateAction)
        {
            // suppress our own WM_CLIPBOARDUPDATE notification; it arrives
            // asynchronously via the message loop, so the guard must stay set
            // until the queue has been pumped, same as in OnClipboardTextChangedAsync
            _updatingClipboard = true;
            try
            {
                updateAction();
            }
            finally
            {
                ResetAsync().IgnoreCancellations();
            }

            async Task ResetAsync()
            {
                try
                {
                    await InputUtils.InputYield(
                        delay: CLIPBOARD_MONITORING_DELAY, token: this.Token);
                }
                finally
                {
                    _updatingClipboard = false;
                }
            }
        }

        void IHotkeyHandlerHost.ClearClipboard()
        {
            UpdateClipboard(() => Clipboard.Clear());
        }

        void IHotkeyHandlerHost.SetClipboardText(string text)
        {
            UpdateClipboard(() => Clipboard.SetText(text, TextDataFormat.UnicodeText));
        }

        void IHotkeyHandlerHost.SetClipboardDataObject(object data)
        {
            UpdateClipboard(() => Clipboard.SetDataObject(data));
        }

        async Task IHotkeyHandlerHost.FeedTextAsync(string text, CancellationToken token)
        {
            using var threadInputScope = AttachedThreadInputScope.Create();
            if (threadInputScope.IsAttached)
            {
                using (WaitCursorScope.Create())
                {
                    await KeyboardInput.WaitForAllKeysReleasedAsync(token);
                }
                await KeyboardInput.FeedTextAsync(text, token);
            }
        }

        void IHotkeyHandlerHost.PlayNotificationSound()
        {
            if (_soundPlayer.Value is SoundPlayer soundPlayer)
            {
                soundPlayer.Stop();
                soundPlayer.Play();
            }
        }

        void IHotkeyHandlerHost.ShowMenu()
        {
            async Task showMenuAsync()
            {
                // show the menu and await its dismissal
                if (!await _asyncLock.WaitAsync(ASYNC_LOCK_TIMEOUT, this.Token))
                {
                    return;
                }
                OnEnterMenu();
                try
                {
                    var menuClosedTcs = new TaskCompletionSource<DBNull>(TaskCreationOptions.RunContinuationsAsynchronously);

                    using var rego = this.Token.Register(() => menuClosedTcs.TrySetCanceled());

                    using var menuCloseScope = SubscriptionScope<ToolStripDropDownClosedEventHandler>.Create(
                        (s, e) => menuClosedTcs.TrySetResult(DBNull.Value),
                        handler => this.MainMenu.Closed += handler,
                        handler => this.MainMenu.Closed -= handler);

                    if (!WinUtils.TryGetThirdPartyForgroundWindow(out var targetWindow))
                    {
                        // special treatment for our Internal Notepad
                        if (_notepad.IsValueCreated && this._notepad.Value.ContainsFocus)
                        {
                            targetWindow = WinApi.GetForegroundWindow();
                        }
                        else
                        {
                            targetWindow = WinUtils.GetPrevActiveWindow();
                        }
                    }

                    using (var attachedInput = AttachedThreadInputScope.Create())
                    {
                        // steal the focus
                        WinApi.SetForegroundWindow(this.Handle);
                        await InputUtils.TimerYield(token: this.Token);
                    }

                    try
                    {
                        this.MainMenu.Show(this, Cursor.Position);
                        await menuClosedTcs.Task;
                    }
                    finally
                    {
                        // restore the focus
                        Cursor.Hide();
                        Cursor.Show();
                        WinApi.SetForegroundWindow(targetWindow);
                    }
                }
                finally
                {
                    OnExitMenu();
                    _asyncLock.Release();
                }
            }

            showMenuAsync().IgnoreCancellations();
        }

        async Task IHotkeyHandlerHost.ShowNotepad(string? text)
        {
            using var threadInputScope = AttachedThreadInputScope.Create();

            if (!this.Notepad.Visible)
            {
                this.Notepad.Show();
            }
            WinApi.SetForegroundWindow(Notepad.Handle);

            await this.Notepad.WaitForReadyAsync(this.Token);

            this.Notepad.FocusEditor();

            if (text != null)
            {
                this.Notepad.Paste(text);
            }
        }

        #region IContainer
        public ComponentCollection Components => _componentContainer.Components;

        public void Add(IComponent? component)
        {
            _componentContainer.Add(component);
        }

        public void Add(IComponent? component, string? name)
        {
            _componentContainer.Add(component, name);
        }

        public void Remove(IComponent? component)
        {
            _componentContainer.Remove(component);
        }
        #endregion

        int IHotkeyHandlerHost.TabSize =>
            Configuration.GetOption("tabSize", 2);

        #region IsFormattingRemovalEnabled

        private const string REMOVE_CLIPBOARD_FORMATTING = "removeClipboardFormatting";

        /// <summary>
        /// Toggled from the tray menu and persisted to the Roaming config,
        /// so the choice survives restarts and upgrades
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool IsFormattingRemovalEnabled
        {
            get => Configuration.GetOption(REMOVE_CLIPBOARD_FORMATTING, defaultValue: true);
            set
            {
                if (value == this.IsFormattingRemovalEnabled)
                {
                    return;
                }

                Configuration.SetRoamingOption(
                    REMOVE_CLIPBOARD_FORMATTING, value ? "true" : "false");

                if (value)
                {
                    StartClipboardFormatMonitoring();
                }
                else
                {
                    StopClipboardFormatMonitoring();
                }

                RaisePropertyChange();
            }
        }
        #endregion

        #region IsAutorun
        private const string AUTORUN_REGKEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        // backed by the registry, never persisted by the designer
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool IsAutorun
        {
            get
            {
                // ProductName falls back to the assembly name and is never actually null
                var valueName = Application.ProductName!;
                using var regKey = Registry.CurrentUser.OpenSubKey(AUTORUN_REGKEY, writable: false);
                var value = regKey?.GetValue(valueName, String.Empty)?.ToString();
                return value.IsNotNullNorEmpty() &&
                    File.Exists(value) &&
                    String.Compare(
                        Path.GetFullPath(value.Trim()),
                        Path.GetFullPath(Diagnostics.GetExecutablePath()),
                        StringComparison.OrdinalIgnoreCase) == 0;
            }
            set
            {
                var valueName = Application.ProductName!;
                using var regKey = Registry.CurrentUser.OpenSubKey(AUTORUN_REGKEY, writable: true);
                if (regKey == null)
                {
                    throw WinUtils.CreateExceptionFromLastWin32Error();
                }
                if (value)
                {
                    var valueData = Diagnostics.GetExecutablePath();
                    regKey.SetValue(valueName, valueData, RegistryValueKind.String);
                }
                else
                {
                    // the value may have been removed externally in the meantime
                    regKey.DeleteValue(valueName, throwOnMissingValue: false);
                }
                RaisePropertyChange();
            }

        }

        #endregion
    }
}
