// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    /// <summary>
    /// Test for AppLogic.Extensions
    /// </summary>
    [TestClass]
    public class ExtensionsTest
    {
        public ExtensionsTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext? testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext? TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void Test_Exception_AsEnumerable_returns_an_enumerable_of_all_aggregated_exceptions()
        {
            static async Task trowAggregateExceptionAsync()
            {
                await Task.Yield();
                var ex1 = new AggregateException(
                    new AggregateException("Empty aggregate"),
                    new TaskCanceledException("Canceled 1"));
                var ex2 = new AggregateException(ex1, new TaskCanceledException("Canceled 2"));
                var ex3 = new AggregateException(ex2, new InvalidOperationException("Invalid"));
                var ex4 = new AggregateException(ex3, new NotImplementedException("Not Implemented"));
                throw ex4;
            }

            try
            {
                // Task.Wait() wraps an exception with yet another AggregateException 
                // and we want that
                trowAggregateExceptionAsync().Wait();
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(AggregateException));

                var all = ex.AsEnumerable().ToArray();
                Assert.IsTrue(all.Length == 5);
                Assert.IsInstanceOfType(all[0], typeof(AggregateException));
                Assert.IsInstanceOfType(all[1], typeof(TaskCanceledException));
                Assert.IsInstanceOfType(all[2], typeof(TaskCanceledException));
                Assert.IsInstanceOfType(all[3], typeof(InvalidOperationException));
                Assert.IsInstanceOfType(all[4], typeof(NotImplementedException));
            }
        }

        [TestMethod]
        public void Test_Exception_IsOperationCanceledException_is_true_for_cancellation_exceptions_only()
        {
            // first, test a mix of exceptions

            static async Task throwAggregateExceptionAsync()
            {
                await Task.Yield();
                var ex1 = new AggregateException(
                    new AggregateException("Empty aggregate"),
                    new TaskCanceledException("Canceled 1!"));
                var ex2 = new AggregateException(ex1, new TaskCanceledException("Canceled 2!"));
                var ex3 = new AggregateException(ex2, new InvalidOperationException("Invalid!"));
                var ex4 = new AggregateException(ex3, new NotImplementedException("Not Implemented!"));
                throw ex4;
            }

            try
            {
                // Task.Wait() wraps an exception with yet another AggregateException
                // and we want that
                throwAggregateExceptionAsync().Wait();
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(AggregateException));
                // it does contain an OperationCanceledException but also instances of some other types
                Assert.IsTrue(!ex.IsOperationCanceled());
            }

            // Then, test a set of TaskCanceledException

            static async Task throwAggregateOfTaskCanceledExceptionsAsync()
            {
                await Task.Yield();
                var ex1 = new AggregateException(new TaskCanceledException("Canceled 1!"));
                var ex2 = new AggregateException(ex1, 
                    new TaskCanceledException("Canceled 2!"));
                var ex3 = new AggregateException(ex1, 
                    new TaskCanceledException("Canceled 3!"),
                    new TaskCanceledException("Canceled 4!"));
                throw ex3;
            }

            try
            {
                // Task.Wait() wraps an exception with yet another AggregateException
                // and we want that
                throwAggregateOfTaskCanceledExceptionsAsync().Wait();
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(AggregateException));
                Assert.IsTrue(ex.IsOperationCanceled());
            }

            // now, test a sigle TaskCanceledException
            try
            {
                throw new TaskCanceledException("Canceled!");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(TaskCanceledException));
                Assert.IsTrue(ex.IsOperationCanceled());
            }

            // now, test a sigle non-TaskCanceledException
            try
            {
                throw new InvalidOperationException("Invalid!");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(!ex.IsOperationCanceled());
            }
        }

        [TestMethod]
        public async Task Test_Task_Observe_does_observes_and_logs_exceptions()
        {
            await using var apartment = new ThreadPoolApartment();
            int logged = 0;

            await apartment.Run(async () =>
            {
                await Task.Yield();

                Action<Exception> logger = _ =>  logged++;

                var token = new CancellationToken(canceled: true);

                async Task work0()
                {
                    await Task.Delay(200);
                    await Task.FromCanceled(token);
                }

                async Task work1()
                {
                    await Task.Delay(400);
                    await Task.FromCanceled(token);
                }

                static async Task work2()
                {
                    await Task.Delay(600);
                    await Task.FromException(new InvalidOperationException());
                }

                static async Task work3()
                {
                    await Task.Delay(800);
                    await Task.FromException(new NotImplementedException());
                }

                work0().Observe();
                work1().Observe(logger);
                work2().Observe(logger);
                work3().Observe(_ => logged++);
            });

            await Task.Delay(100);
            Assert.IsTrue(apartment.AnyBackgroundOperation == true);

            apartment.Complete();
            try
            {
                await apartment.Completion;
                Assert.Fail("Expecting exceptions.");
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var exceptions = apartment.GetExceptions();
                apartment.ClearExceptions();

                Assert.AreSame(ex, apartment.Completion.Exception?.InnerException);

                Assert.IsTrue(logged == 3);
                Assert.IsTrue(exceptions.Length == 4);

                Assert.IsInstanceOfType(exceptions[0], typeof(TaskCanceledException));
                Assert.IsInstanceOfType(exceptions[1], typeof(TaskCanceledException));
                Assert.IsInstanceOfType(exceptions[2], typeof(InvalidOperationException));
                Assert.IsInstanceOfType(exceptions[3], typeof(NotImplementedException));
            }
        }

        [TestMethod]
        public async Task Test_Task_Handle_does_handle_all_exceptions()
        {
            var exceptionList = new List<Exception>();
            var @lock = new object();
            bool handler(Exception ex) 
            { 
                lock (@lock!) 
                    exceptionList!.Add(ex);
                return true;
            }

            await using var apartment = new ThreadPoolApartment();
            await apartment.Run(async () =>
            {
                await Task.Yield();

                var token = new CancellationToken(canceled: true);

                async Task work0()
                {
                    await Task.Delay(200);
                    await Task.FromCanceled(token);
                }

                async Task work1()
                {
                    await Task.Delay(400);
                    await Task.FromCanceled(token);
                }

                static async Task work2()
                {
                    await Task.Delay(600);
                    await Task.FromException(new InvalidOperationException());
                }

                static async Task work3()
                {
                    await Task.Delay(800);
                    await Task.FromException(new NotImplementedException());
                }

                work0().Handle(handler);
                work1().Handle(handler);
                work2().Handle(handler);
                work3().Handle(handler);
            });

            await Task.Delay(100);
            Assert.IsTrue(apartment.AnyBackgroundOperation == true);

            apartment.Complete();
            await apartment.Completion;

            Assert.IsTrue(apartment.AnyBackgroundOperation == false);
            Assert.IsTrue(!apartment.GetExceptions().Any());

            Assert.IsInstanceOfType(exceptionList[0], typeof(TaskCanceledException));
            Assert.IsInstanceOfType(exceptionList[1], typeof(TaskCanceledException));
            Assert.IsInstanceOfType(exceptionList[2], typeof(InvalidOperationException));
            Assert.IsInstanceOfType(exceptionList[3], typeof(NotImplementedException));
        }

        [TestMethod]
        public async Task Test_Task_IgnoreCancellations_ignores_only_cancellation_exceptions()
        {
            static async Task throwAggregateOfTaskCanceledExceptionsAsync()
            {
                await Task.Yield();
                var ex1 = new AggregateException(new TaskCanceledException("Canceled 1!"));
                var ex2 = new AggregateException(ex1,
                    new TaskCanceledException("Canceled 2!"));
                var ex3 = new AggregateException(ex1,
                    new TaskCanceledException("Canceled 3!"),
                    new TaskCanceledException("Canceled 4!"));
                throw ex3;
            }

            await using var apartment = new ThreadPoolApartment();
            var logged = false;
            await apartment.Run(async () =>
            {
                await Task.Yield();
                Task.FromCanceled(new CancellationToken(canceled: true)).IgnoreCancellations();
                throwAggregateOfTaskCanceledExceptionsAsync().IgnoreCancellations();
                throwAggregateOfTaskCanceledExceptionsAsync().IgnoreCancellations(ex => logged = true);
            });

            apartment.Complete();
            await apartment.Completion;
            Assert.IsTrue(apartment.AnyBackgroundOperation == false);
            Assert.IsTrue(logged);
        }
    }
}
