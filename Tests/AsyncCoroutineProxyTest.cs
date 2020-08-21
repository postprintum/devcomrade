// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class AsyncCoroutineProxyTest
    {
        const string TRACE_CATEGORY = "coroutines";

        /// <summary>
        /// CoroutineA yields to CoroutineB
        /// </summary>
        private async IAsyncEnumerable<string> CoroutineA(
            ICoroutineProxy<string> coroutineProxy,
            [EnumeratorCancellation] CancellationToken token)
        {
            await using var coroutine = await coroutineProxy.AsAsyncEnumerator(token);

            const string name = "A";
            var i = 0;

            // yielding 1
            Trace.WriteLine($"{name} about to yeild: {++i}", TRACE_CATEGORY);
            yield return $"{i} from {name}";

            // receiving
            if (!await coroutine.MoveNextAsync())
            {
                yield break;
            }
            Trace.WriteLine($"{name} received: {coroutine.Current}", TRACE_CATEGORY);

            // yielding 2
            Trace.WriteLine($"{name} about to yeild: {++i}", TRACE_CATEGORY);
            yield return $"{i} from {name}";

            // receiving
            if (!await coroutine.MoveNextAsync())
            {
                yield break;
            }
            Trace.WriteLine($"{name} received: {coroutine.Current}", TRACE_CATEGORY);

            // yielding 3
            Trace.WriteLine($"{name} about to yeild: {++i}", TRACE_CATEGORY);
            yield return $"{i} from {name}";
        }

        /// <summary>
        /// CoroutineB yields to CoroutineA
        /// </summary>
        private async IAsyncEnumerable<string> CoroutineB(
            ICoroutineProxy<string> coroutineProxy,
            [EnumeratorCancellation] CancellationToken token)
        {
            await using var coroutine = await coroutineProxy.AsAsyncEnumerator(token);

            const string name = "B";
            var i = 0;

            // receiving
            if (!await coroutine.MoveNextAsync())
            {
                yield break;
            }
            Trace.WriteLine($"{name} received: {coroutine.Current}", TRACE_CATEGORY);

            // yielding 1
            Trace.WriteLine($"{name} about to yeild: {++i}", TRACE_CATEGORY);
            yield return $"{i} from {name}";

            // receiving
            if (!await coroutine.MoveNextAsync())
            {
                yield break;
            }
            Trace.WriteLine($"{name} received: {coroutine.Current}", TRACE_CATEGORY);

            // yielding 2
            Trace.WriteLine($"{name} about to yeild: {++i}", TRACE_CATEGORY);
            yield return $"{i} from {name}";

            // receiving
            if (!await coroutine.MoveNextAsync())
            {
                yield break;
            }
            Trace.WriteLine($"{name} received: {coroutine.Current}", TRACE_CATEGORY);
        }

        /// <summary>
        /// Testing CoroutineA and CoroutineB cooperative execution
        /// </summary>
        [TestMethod] 
        public async Task test_two_coroutines_execution_flow()
        {
            // Here we execute two cotoutines, CoroutineA and CoroutineB,
            // which asynchronously yield to each other

            //TODO: test cancellation scenarios
            var token = CancellationToken.None;

            // use ThreadPoolApartment to impose asynchronous continuations for all awaits,
            // regardless if the task has completed synchronously
            // the reasoning behind this is essentially the same as for
            // the TaskContinuationOptions.RunContinuationsAsynchronously option:
            // https://tinyurl.com/RunContinuationsAsynchronously

            await using var apartment = new Tests.ThreadPoolApartment();
            await apartment.Run(async () =>
            {
                var proxyA = new AsyncCoroutineProxy<string>();
                var proxyB = new AsyncCoroutineProxy<string>();

                var listener = new Tests.CategoryTraceListener(TRACE_CATEGORY);
                Trace.Listeners.Add(listener);
                try
                {
                    // start both coroutines
                    await Task.WhenAll(
                        proxyA.Run(token => CoroutineA(proxyB, token), token),
                        proxyB.Run(token => CoroutineB(proxyA, token), token))
                        .WithAggregatedExceptions();
                }
                finally
                {
                    Trace.Listeners.Remove(listener);
                }

                var traces = listener.ToArray();
                Assert.AreEqual(traces[0], "A about to yeild: 1");
                Assert.AreEqual(traces[1], "B received: 1 from A");
                Assert.AreEqual(traces[2], "B about to yeild: 1");
                Assert.AreEqual(traces[3], "A received: 1 from B");
                Assert.AreEqual(traces[4], "A about to yeild: 2");
                Assert.AreEqual(traces[5], "B received: 2 from A");
                Assert.AreEqual(traces[6], "B about to yeild: 2");
                Assert.AreEqual(traces[7], "A received: 2 from B");
                Assert.AreEqual(traces[8], "A about to yeild: 3");
                Assert.AreEqual(traces[9], "B received: 3 from A");
            });
        }
    }
}
