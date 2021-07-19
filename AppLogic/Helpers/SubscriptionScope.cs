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
    /// using (new SubscriptionScope<FormClosedEventHandler>(
    ///     (s, e) => cts.Cancel(),
    ///     listener => form.FormClosed += listener,
    ///     listener => form.FormClosed -= listener))
    /// {
    ///		...	
    /// }
    /// </code>
    /// </example>
    internal struct SubscriptionScope<TListener>: IDisposable
        where TListener : Delegate
    {
        readonly TListener _listener;
        readonly Action<TListener> _unsubscribe;

        private SubscriptionScope(
            TListener listener,
            Action<TListener> subscribe,
            Action<TListener> unsubscribe)
        {
            _listener = listener;
            _unsubscribe = unsubscribe;
            subscribe(listener);
        }

        void IDisposable.Dispose()
        {
            _unsubscribe(_listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDisposable Create(
                TListener listener,
                Action<TListener> subscribe,
                Action<TListener> unsubscribe)
        {
            return new SubscriptionScope<TListener>(listener, subscribe, unsubscribe);
        }
    }
}
