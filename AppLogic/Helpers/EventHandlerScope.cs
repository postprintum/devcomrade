// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace AppLogic.Helpers
{
    /// <summary>
    /// Handle an event with a scope, e.g.:
    /// </summary>
    /// <example>
    /// <code>
    /// using (new EventHandlerScope<FormClosedEventHandler>(
    ///     (s, e) => cts.Cancel(),
    ///     handler => form.FormClosed += handler,
    ///     handler => form.FormClosed -= handler))
    /// {
    ///		...	
    /// }
    /// </code>
    /// </example>
    internal struct EventHandlerScope<TEventHandler>: IDisposable
        where TEventHandler : Delegate
    {
        readonly TEventHandler _handler;
        readonly Action<TEventHandler> _unsubscribe;

        private EventHandlerScope(
            TEventHandler handler,
            Action<TEventHandler> subscribe,
            Action<TEventHandler> unsubscribe)
        {
            _handler = handler;
            _unsubscribe = unsubscribe;
            subscribe(handler);
        }

        void IDisposable.Dispose()
        {
            _unsubscribe(_handler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDisposable Create(
                TEventHandler handler,
                Action<TEventHandler> subscribe,
                Action<TEventHandler> unsubscribe)
        {
            return new EventHandlerScope<TEventHandler>(handler, subscribe, unsubscribe);
        }
    }
}
