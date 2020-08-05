// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Tests
{
    public interface ICoroutineProxy<T>
    {
        public Task<IAsyncEnumerable<T>> AsAsyncEnumerable(CancellationToken token = default);
    }

    public static class CoroutineProxyExt
    {
        public async static Task<IAsyncEnumerator<T>> AsAsyncEnumerator<T>(
            this ICoroutineProxy<T> @this,
            CancellationToken token = default)
        {
            return (await @this.AsAsyncEnumerable(token)).GetAsyncEnumerator(token);
        }

        public async static ValueTask<T> GetNextAsync<T>(this IAsyncEnumerator<T> @this)
        {
            if (!await @this.MoveNextAsync())
            {
                throw new IndexOutOfRangeException(nameof(GetNextAsync));
            }
            return @this.Current;
        }

        public static Task<T> GetNextAsync<T>(this IAsyncEnumerator<T> @this, CancellationToken token)
        {
            return @this.GetNextAsync().AsTask().ContinueWith<Task<T>>(
                continuationFunction: ante => ante,
                cancellationToken: token,
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
        }

        public static Task Run<T>(
            this CoroutineProxy<T> @this,
            IAsyncApartment apartment,
            Func<CancellationToken, IAsyncEnumerable<T>> routine, 
            CancellationToken token)
        {
            return apartment.Run(
                () => @this.Run(routine, token), 
                token);
        }
    }

    public class CoroutineProxy<T> : ICoroutineProxy<T>
    {
        readonly TaskCompletionSource<IAsyncEnumerable<T>> _proxyTcs =
            new TaskCompletionSource<IAsyncEnumerable<T>>(TaskCreationOptions.RunContinuationsAsynchronously);

        public CoroutineProxy()
        {
        }

        private async IAsyncEnumerable<T> CreateProxyAsyncEnumerable(
            ISourceBlock<T> bufferBlock,
            [EnumeratorCancellation] CancellationToken token)
        {
            var completionTask = bufferBlock.Completion;
            while (true)
            {
                var itemTask = bufferBlock.ReceiveAsync(token);
                var any = await Task.WhenAny(itemTask, completionTask);
                if (any == completionTask)
                {
                    // observe completion exceptions if any
                    await completionTask; 
                    yield break;
                }
                yield return await itemTask;
            }
        }

        async Task<IAsyncEnumerable<T>> ICoroutineProxy<T>.AsAsyncEnumerable(CancellationToken token)
        {
            using var _ = token.Register(() => _proxyTcs.TrySetCanceled(), useSynchronizationContext: false);
            return await _proxyTcs.Task;
        }

        public async Task Run(Func<CancellationToken, IAsyncEnumerable<T>> routine, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var bufferBlock = new BufferBlock<T>();
            var proxy = CreateProxyAsyncEnumerable(bufferBlock, token);
            _proxyTcs.SetResult(proxy); // throw if already set

            try
            {
                //TODO: do we need to use routine(token).WithCancellation(token) ?
                await foreach (var item in routine(token))
                {
                    await bufferBlock.SendAsync(item, token);
                }
                bufferBlock.Complete();
            }
            catch (Exception ex)
            {
                ((IDataflowBlock)bufferBlock).Fault(ex);
                throw;
            }
        }
    }
}
