// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AppLogic.Helpers
{
    /// <summary>
    /// Task Extensions
    /// </summary>
    internal static partial class TaskExtensions
    {
        /// <summary>
        /// Get all of AggregateException.InnerExceptions with try/await task/catch.
        /// See https://stackoverflow.com/a/62607500/1768303
        /// </summary>
        public static Task WithAggregatedExceptions(this Task @this)
        {
            return @this.ContinueWith(
                continuationFunction: anteTask =>
                    anteTask.IsFaulted &&
                    anteTask.Exception is AggregateException ex &&
                    (ex.InnerExceptions.Count > 1 || ex.InnerException is AggregateException) ?
                    Task.FromException(ex.Flatten()) : anteTask,
                cancellationToken: CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// A workaround for getting all of AggregateException.InnerExceptions with try/await/catch
        /// </summary>
        public static Task<T> WithAggregatedExceptions<T>(this Task<T> @this)
        {
            return @this.ContinueWith(
                continuationFunction: anteTask =>
                    anteTask.IsFaulted &&
                    anteTask.Exception is AggregateException ex &&
                    (ex.InnerExceptions.Count > 1 || ex.InnerException is AggregateException) ?
                    Task.FromException<T>(ex.Flatten()) : anteTask,
                cancellationToken: CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// Add surrogate cancellation to any task
        /// </summary>
        public static Task WithCancellation(this Task @this, CancellationToken token)
        {
            return @this.ContinueWith(
                continuationFunction: anteTask => anteTask,
                cancellationToken: token,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// Add surrogate cancellation to any task
        /// </summary>
        public static Task<T> WithCancellation<T>(this Task<T> @this, CancellationToken token)
        {
            return @this.ContinueWith(
                continuationFunction: anteTask => anteTask,
                cancellationToken: token,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// Never overlook exceptions! Observe exceptions for a fire-and-forget task 
        /// by posting any exception to the current synchronization context.
        /// </summary>
        public static async void Observe(this Task @this, Action<Exception>? logger = null)
        {
            try
            {
                await @this;
            }
            catch (Exception ex)
            {
                logger?.Invoke(@this.PickException(ex));
                throw;
            }
        }

        /// <summary>
        /// Observe exceptions for a fire-and-forget task ignoring OperationCanceledException
        /// </summary>
        public static async void IgnoreCancellations(this Task @this, Action<Exception>? logger = null)
        {
            try
            {
                await @this;
            }
            catch (Exception ex)
            {
                logger?.Invoke(@this.PickException(ex));
                if (@this.IsCanceled ||
                    ex is OperationCanceledException ||
                    @this.Exception?.IsOperationCanceled() == true)
                {
                    return;
                }
                throw;
            }
        }

        /// <summary>
        /// Handle exception with an Action, with recursive flattening of any nested AggregateException
        /// </summary>
        public static void Handle(this Exception @this, Action<Exception> handler)
        {
            foreach (var exception in @this.AsEnumerable())
            {
                handler(exception);
            }
        }

        /// <summary>
        /// Observe and handle exceptions for a fire-and-forget task 
        /// Unhandled exceptions are posted to the current synchronization context
        /// </summary>
        public static async void Handle(this Task @this, Func<Exception, bool> handler)
        {
            try
            {
                await @this;
            }
            catch (Exception ex)
            {
                if (handler(@this.PickException(ex)) == false)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Choose between Task.Exception or caught exception, like this:
        /// </summary>
        /// <example>
        /// <code>
        /// var task = DoAsync(); 
        /// try { await task; } 
        /// catch (Exception @caught) { Log(task.PickException(@caught); }
        /// </code>
        /// </example>		
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception PickException(this Task @this, Exception @caught)
        {
            // pick many exceptions over one
            return (@this.Exception?.InnerExceptions.Count > 1) ?
                @this.Exception :
                @caught.Unwrap();
        }
    }
}
