// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class WinFormsApartmentTest
    {
        [TestMethod]
        public async Task Test_async_void_exceptions_are_captured()
        {
            await using var apartment = new WinFormsApartment();
            await apartment.Run(async () =>
            {
                await Task.Yield();

                Assert.IsTrue(SynchronizationContext.Current is 
                    System.Windows.Forms.WindowsFormsSynchronizationContext);

                async void AsyncVoidMethod1()
                {
                    await Task.Delay(200);
                    throw new InvalidOperationException();
                }

                async void AsyncVoidMethod2()
                {
                    await Task.Delay(400);
                    throw new NotSupportedException();
                }

                AsyncVoidMethod1();
                AsyncVoidMethod2();
            });

            // we can't track background operations with WindowsFormsSynchronizationContext
            Assert.IsTrue(apartment.AnyBackgroundOperation == null);

            await Task.Delay(600); // race with both async void methods
            apartment.Complete();

            try
            {
                await apartment.Completion.WithAggregatedExceptions();
                Assert.Fail("Must not reach here, expecting exceptions.");
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                apartment.ClearExceptions();

                // we expect to see 2 exceptions wrapped as AggregateException

                var aggregate = ex as AggregateException;
                Assert.IsNotNull(aggregate);
                Assert.IsTrue(aggregate!.InnerExceptions.Count == 2);
                Assert.IsTrue(aggregate.InnerExceptions[0] is InvalidOperationException);
                Assert.IsTrue(aggregate.InnerExceptions[1] is NotSupportedException);
            }
        }
    }
}
