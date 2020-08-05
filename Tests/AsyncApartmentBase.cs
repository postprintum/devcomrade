// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    /// <summary>
    /// A base class for an async apartment, which is a higher level wrapper over SynchronizationContext,
    /// which we use for testing async/await behaviors, including async void methods
    /// </summary>
    public abstract class AsyncApartmentBase: IAsyncApartment
    {
        #region State

        private readonly object _lock = new Object();

        private readonly List<Exception> _exceptions = new List<Exception>();

        private Int64 _backgroundOperationCount = 0;

        private bool _completionCommenced = false;

        private readonly TaskCompletionSource<bool> _completionTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        protected abstract TaskScheduler TaskScheduler { get; }

        #endregion

        #region Helpers

        /// <summary>
        /// Scop async operations with OperationStarted/OperationCompleted calls
        /// </summary>
        protected struct AsyncScope : IDisposable
        {
            private AsyncApartmentBase _apartment;

            public AsyncScope(AsyncApartmentBase apartment)
            {
                _apartment = apartment;
                _apartment.OperationStarted();
            }

            void IDisposable.Dispose()
            {
                _apartment.OperationCompleted();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T WithLock<T>(Func<T> func)
        {
            lock (_lock)
            {
                return func();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WithLock(Action action)
        {
            lock (_lock)
            {
                action();
            }
        }

        protected virtual void AddException(Exception ex)
        {
            WithLock(() => _exceptions.Add(ex));
            ThreadException?.Invoke(this, new ThreadExceptionEventArgs(ex));
        }

        protected bool TrySetCompletion()
        {
            return WithLock(() =>
            {
                if (_completionTcs.Task.IsCompleted)
                {
                    return false;
                }

                if (_exceptions.Any())
                {
                    return _completionTcs.TrySetException(_exceptions.ToArray());
                }
                else
                {
                    return _completionTcs.TrySetResult(true);
                }
            });
        }

        protected void CommenceCompletion(Action action)
        {
            WithLock(() => 
            {
                if (_completionCommenced)
                {
                    return;
                }
                _completionCommenced = true;
                action();
            });
        }

        /// <summary>
        /// This actually concludes the completion, e.g., 
        /// by posting a quite message to the message loop or resolving a promise task
        /// </summary>
        protected abstract void ConcludeCompletion();

        protected Task GetCompletionTask() => _completionTcs.Task;

        #endregion

        #region Public API

        public virtual Task Completion => GetCompletionTask();

        public bool IsCompletionCommenced => 
            WithLock(() => _completionCommenced);

        /// <summary>
        /// Can be null if there is no realiable way of tracking "async void" operations,
        /// e.g., as with WindowsFormsSynchronizationContext
        /// </summary>
        public virtual bool? AnyBackgroundOperation => 
            WithLock(() => _backgroundOperationCount > 0);

        public event ThreadExceptionEventHandler? ThreadException;

        public event EventHandler? BackgroundOperationsCompleted;

        /// <summary>
        /// All encountered and saved exceptions
        /// </summary>
        public Exception[] GetExceptions()
        {
            return WithLock(() => _exceptions.ToArray());
        }
    
        /// <summary>
        /// Clear the list of exceptions, other wise Dispose will rethrow them
        /// </summary>
        public void ClearExceptions()
        {
            WithLock(() => _exceptions.Clear());
        }

        /// <summary>
        /// Wrap something with a OperationStarted/OperationCompleted pair as IDisposable
        /// </summary>
        public IDisposable CreateAsyncScope()
        {
            return new AsyncScope(this);
        }

        /// <summary>
        /// Commence the completion, which may be further deffered until 
        /// the last background operation (i.e. "async void" method) has completed
        /// </summary>
        public virtual void Complete()
        {
            CommenceCompletion(() =>
            {
                // AnyBackgroundOperation can be null when 
                // it is impossible to track background operations
                if (this.AnyBackgroundOperation != true) 
                {
                    ConcludeCompletion();
                }
            });
        }

        /// <summary>
        /// Awaits completion without observing any exceptions.
        /// The consuming code should use the Completion task to observe the exceptions.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (this.Completion.IsCompleted)
            {
                return;
            }
            // any race conditons must be handled by this.Complete()
            this.Complete();
            try
            {
                await this.Completion;
            }
            catch (Exception ex)
            {
                // it is the client code responsibility to 
                // observe the Completion task and call ClearExceptions if desired;
                // we can stop here for debugging, otherwise
                // we rethrow accumulated exceptions in the finally block below
                ex.Unused();
            }
            finally
            {
                var exceptions = GetExceptions();
                if (exceptions.Any())
                {
                    throw new AggregateException(
                        $"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod()?.Name}",
                        exceptions);
                }
            }
        }

        public virtual void OperationStarted()
        {
            WithLock(() => _backgroundOperationCount++);
        }

        public virtual void OperationCompleted()
        {
            bool backgroundOperationsCompleted = false;

            WithLock(() =>
            {
                _backgroundOperationCount--;
                Debug.Assert(_backgroundOperationCount >= 0);

                // AnyBackgroundOperation can be null, in which case don't conclude here
                backgroundOperationsCompleted = this.AnyBackgroundOperation == false;

                if (_completionCommenced && backgroundOperationsCompleted)
                {
                    ConcludeCompletion();
                }
            });

            if (backgroundOperationsCompleted)
            {
                BackgroundOperationsCompleted?.Invoke(this, new EventArgs());
            }
        }

        /// <summary>
        /// Wraps an asyc operation with OperationStarted/OperationCompleted
        /// </summary>
        private Task WithScopeAsync(Func<Task> func)
        {
            // An alternative version could be like below,
            // but we don't want any side effects like unwrapping exceptions 
            // or different continuation context:
            //
            // using (CreateAsyncScope())
            // {
            //     await Task.Factory.StartNew(
            //         action, token, TaskCreationOptions.None, this.TaskScheduler).ConfigureAwait(false);
            // }

            this.OperationStarted();

            return func().ContinueWith(
                continuationFunction: anteTask =>
                {
                    this.OperationCompleted();
                    return anteTask;
                },
                cancellationToken: CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default).Unwrap();
        }

        private Task<T> WithScopeAsync<T>(Func<Task<T>> func)
        {
            this.OperationStarted();

            return func().ContinueWith(
                continuationFunction: anteTask =>
                {
                    this.OperationCompleted();
                    return anteTask;
                },
                cancellationToken: CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default).Unwrap();
        }

        /// <summary>A wrapper around Task.Factory.StartNew to run an action</summary>
        public Task Run(Action action, CancellationToken token = default)
        {
            return WithScopeAsync(() => Task.Factory.StartNew(
                    action, token, TaskCreationOptions.None, this.TaskScheduler));
        }

        /// <summary>A wrapper around Task.Factory.StartNew to run a func</summary>
        public Task<TResult> Run<TResult>(Func<TResult> func, CancellationToken token = default)
        {
            return WithScopeAsync(() => Task<TResult>.Factory.StartNew(
                func, token, TaskCreationOptions.None, this.TaskScheduler));
        }

        /// <summary>A wrapper around Task.Factory.StartNew to run an async func</summary>
        public Task Run(Func<Task> func, CancellationToken token = default)
        {
            return WithScopeAsync(() => Task.Factory.StartNew(
                func, token, TaskCreationOptions.None, this.TaskScheduler).Unwrap());
        }

        /// <summary>A wrapper around Task.Factory.StartNew to run async lambdas with a result</summary>
        public Task<TResult> Run<TResult>(Func<Task<TResult>> func, CancellationToken token = default)
        {
            return WithScopeAsync(() => Task.Factory.StartNew(
                func, token, TaskCreationOptions.None, this.TaskScheduler).Unwrap());
        }

        #endregion
    }
}
