// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Threading.Tasks;

namespace AppLogic.Helpers
{
    /// <summary>
    /// Disposable
    /// </summary>
    internal struct Disposable : IDisposable
    {
        private readonly Action _dispose;

        private Disposable(Action dispose)
        {
            _dispose = dispose;
        }

        void IDisposable.Dispose()
        {
            _dispose();
        }

        public static async ValueTask<IDisposable> CreateAsync(Func<Task> func, Action dispose)
        {
            await func();
            return new Disposable(dispose);
        }

        public static IDisposable Create(Action action, Action dispose)
        {
            action();
            return new Disposable(dispose);
        }
    }
}