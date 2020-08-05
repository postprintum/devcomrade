// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public interface IAsyncApartment: IAsyncDisposable
    {
        /// <summary>A wrapper around Task.Factory.StartNew to run an action</summary>
        public Task Run(Action action, CancellationToken token = default);

        /// <summary>A wrapper around Task.Factory.StartNew to run a func</summary>
        public Task<TResult> Run<TResult>(Func<TResult> func, CancellationToken token = default);

        /// <summary>A wrapper around Task.Factory.StartNew to run an async func</summary>
        public Task Run(Func<Task> func, CancellationToken token = default);

        /// <summary>A wrapper around Task.Factory.StartNew to run an async func</summary>
        public Task<TResult> Run<TResult>(Func<Task<TResult>> func, CancellationToken token = default);
    }
}
