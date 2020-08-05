// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Threading.Tasks;

#nullable enable

namespace AppLogic.Helpers
{
    /// <summary>
    /// Disposable
    /// </summary>
    internal struct Disposable: IDisposable
    {
        private struct EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public static IDisposable Empty = new EmptyDisposable();

        private readonly Action _finally;

        private Disposable(Action @finally)
        {
            _finally = @finally;
        }

        void IDisposable.Dispose()
        {
            _finally();
        }

        public static async ValueTask<IDisposable> CreateAsync(Func<Task> func, Action @finally)
        {
            await func();
            return new Disposable(@finally);
        }

        public static IDisposable Create(Action action, Action @finally)
        {
            action();
            return new Disposable(@finally);
        }
    }
}
