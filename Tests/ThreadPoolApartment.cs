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
    /// <summary>
    /// Queue callback items posted to SynchronizationContext.Post for execution 
    /// on ThreadPool and collect any thrown exceptions.
    /// Used for testing async/await behaviors, including async void methods.
    /// It also imposes asynchronous continuations for all await within its scope,
    /// the reasons behind this is essentially the same as with RunContinuationsAsynchronously:
    /// https://tinyurl.com/RunContinuationsAsynchronously
    /// </summary>
    public class ThreadPoolApartment : AsyncApartmentBase
    {
        #region Helpers

        internal class ThreadPoolSyncContext : SynchronizationContext
        {
            private readonly ThreadPoolApartment _apartment;

            public TaskScheduler TaskScheduler { get; }

            public ThreadPoolSyncContext(ThreadPoolApartment apartment)
            {
                _apartment = apartment;
                this.TaskScheduler = WithContext(() => TaskScheduler.FromCurrentSynchronizationContext());
            }

            protected void WithContext(Action action)
            {
                var savedContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(this);
                try
                {
                    action();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(savedContext);
                }
            }

            protected T WithContext<T>(Func<T> func)
            {
                var savedContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(this);
                try
                {
                    return func();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(savedContext);
                }
            }

            public override void Send(SendOrPostCallback d, object? state)
            {
                throw new NotImplementedException(nameof(Send));
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }

            public override void OperationStarted()
            {
                _apartment.OperationStarted();
            }

            public override void OperationCompleted()
            {
                _apartment.OperationCompleted();
            }

            public override void Post(SendOrPostCallback d, object? state)
            {
                void callback(object? s)
                {
                    try
                    {
                        WithContext(() => d(s));
                    }
                    catch (Exception ex)
                    {
                        _apartment.AddException(ex);
                    }
                    finally
                    {
                        OperationCompleted();
                    }
                }

                OperationStarted();
                ThreadPool.UnsafeQueueUserWorkItem(callback, state, preferLocal: false);
            }
        }

        #endregion

        #region State

        private readonly ThreadPoolSyncContext _syncContext;

        protected override TaskScheduler TaskScheduler { get; }

        #endregion

        public ThreadPoolApartment()
        {
            _syncContext = new ThreadPoolSyncContext(this);
            this.TaskScheduler = _syncContext.TaskScheduler;
        }

        protected override void ConcludeCompletion()
        {
            TrySetCompletion();
        }
    }
}
