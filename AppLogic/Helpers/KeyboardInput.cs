// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Helpers
{
    internal static class KeyboardInput
    {
        const int KEYBOARD_POLL_DELAY = WinApi.USER_TIMER_MINIMUM;
        const uint QS_KEYBOARD = WinApi.QS_KEY | WinApi.QS_HOTKEY;

        static readonly SemaphoreSlim s_asyncLock = new SemaphoreSlim(1);

        private static void SimulateKeyDown(uint vKey, bool extended = false)
        {
            var scancode = (byte)WinApi.MapVirtualKey(vKey, WinApi.MAPVK_VK_TO_VSC);
            WinApi.keybd_event((byte)vKey, (byte)scancode,
                extended ? WinApi.KEYEVENTF_EXTENDEDKEY : 0, 0);
        }

        private static void SimulateKeyUp(uint vKey, bool extended = false)
        {
            var scancode = (byte)WinApi.MapVirtualKey(vKey, WinApi.MAPVK_VK_TO_VSC);
            WinApi.keybd_event((byte)vKey, (byte)scancode,
                (extended ? WinApi.KEYEVENTF_EXTENDEDKEY : 0) | WinApi.KEYEVENTF_KEYUP, 0);
        }

        private static int[] AllKeys => s_allKeys.Value;

        private static Lazy<int[]> s_allKeys = new Lazy<int[]>(() =>
        {
            // ignore some toogle keys (CapsLock etc) when we check if all keys are de-pressed
            var toogleKeys = new[]
            {
                // https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
                WinApi.VK_NUMLOCK, WinApi.VK_SCROLL, WinApi.VK_CAPITAL, WinApi.VK_ESCAPE,
                0xE7, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1F, 0xFF
            };
            return Enumerable.Range(1, 256).Where(key => !toogleKeys.Contains(key)).ToArray();
        }, LazyThreadSafetyMode.None);

        public static bool IsKeyPressed(int key)
        {
            return (WinApi.GetAsyncKeyState(key) & 0x8000) != 0; 
        }

        public static bool IsAnyKeyPressed()
        {
            return AllKeys.Any(key => IsKeyPressed(key));
        }

        private static void ClearKeyboardState()
        {
            foreach (var key in AllKeys)
            {
                if (IsKeyPressed(key))
                {
                    SimulateKeyUp((uint)key);
                    SimulateKeyUp((uint)key, extended: true);
                }
            }
        }

        public static async Task WaitForAllKeysReleasedAsync(CancellationToken token)
        {
            await s_asyncLock.WaitAsync(token);
            try
            {
                ClearKeyboardState();

                while (IsAnyKeyPressed())
                {
                    if (IsKeyPressed(WinApi.VK_ESCAPE))
                    {
                        throw new TaskCanceledException();
                    }
                    await InputHelpers.InputYield(QS_KEYBOARD, KEYBOARD_POLL_DELAY, token);
                }
            }
            finally
            {
                s_asyncLock.Release();
            }
        }

        private static void CharToKeyboardInput(char c, ref WinApi.INPUT input)
        {
            input.type = WinApi.INPUT_KEYBOARD;
            input.union.keyboard.wVk = 0;
            input.union.keyboard.wScan = c;
            input.union.keyboard.dwFlags = WinApi.KEYEVENTF_UNICODE;
            input.union.keyboard.time = 0;
            input.union.keyboard.dwExtraInfo = UIntPtr.Zero;
        }

        public static async Task<bool> FeedTextAsync(string text, CancellationToken token)
        {
            await s_asyncLock.WaitAsync(token);
            try
            {
                var foregroundWindow = WinApi.GetForegroundWindow();
                WinApi.BlockInput(true);
                try
                {
                    var size = Marshal.SizeOf<WinApi.INPUT>();
                    var input = new WinApi.INPUT[1];

                    // feed each character individually and asynchronously
                    foreach (var c in text)
                    {
                        token.ThrowIfCancellationRequested();

                        if (WinApi.GetForegroundWindow() != foregroundWindow || 
                            IsAnyKeyPressed())
                        {
                            break;
                        }

                        if (c == '\n' || c == '\r')
                        {
                            // we need this for correctly handling line breaks
                            // when pasting into Chromium's <textarea> 
                            SimulateKeyDown(WinApi.VK_RETURN);
                            SimulateKeyUp(WinApi.VK_RETURN);
                        }
                        else
                        {
                            CharToKeyboardInput(c, ref input[0]);
                            if (WinApi.SendInput((uint)input.Length, input, size) == 0)
                            {
                                break;
                            }
                        }

                        if (InputHelpers.AnyInputMessage(WinApi.QS_ALLINPUT))
                        {
                            await InputHelpers.TimerYield(token: token);
                        }
                    }
                    return true;
                }
                finally
                {
                    WinApi.BlockInput(false);
                }
            }
            finally
            {
                s_asyncLock.Release();
            }
        }
    }
}
