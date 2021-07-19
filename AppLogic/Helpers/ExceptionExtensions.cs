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
    /// Exception extensions
    /// </summary>
    internal static partial class ExceptionExtensions
    {
        /// <summary>
        /// Unwrap a parent-child chain of single instance AggregateException exceptions
        /// </summary>
        public static Exception Unwrap(this Exception @this)
        {
            var inner = @this;
            while (true)
            {
                if (!(inner is AggregateException aggregate) ||
                    aggregate.InnerExceptions.Count != 1)
                {
                    return inner;
                }
                inner = aggregate.InnerExceptions[0];
            }
        }

        /// <summary>
        /// Exception as enumerable, with recursive flattening of any nested AggregateException
        /// </summary>
        public static IEnumerable<Exception> AsEnumerable(this Exception? @this)
        {
            if (@this == null)
            {
                yield break;
            }

            if (@this is AggregateException aggregate)
            {
                if (aggregate.InnerExceptions.Count == 0)
                {
                    // uncommon but possible: AggregateException without inner exceptions
                    yield return aggregate;
                }
                else if (aggregate.InnerExceptions.Count == 1 && !(aggregate.InnerException is AggregateException))
                {
                    // the most common case: one wrapped exception which is not AggregateException 
                    yield return aggregate.InnerExceptions[0];
                }
                else
                {
                    // yield all of inner exceptions recursively
                    foreach (var child in aggregate.InnerExceptions.SelectMany(inner => inner.AsEnumerable()))
                    {
                        yield return child;
                    }
                }
            }
            else
            {
                yield return @this;
            }
        }

        /// <summary>
        /// Suppress "is never used" warning, use it only if you really mean it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unused(this Exception _)
        {
        }

        /// <summary>
        /// True if the exception is an instance of OperationCanceledException (or a derived class)
        /// </summary>
        public static bool IsOperationCanceled(this Exception? @this)
        {
            if (@this == null)
            {
                return false;
            }
            if (@this is OperationCanceledException)
            {
                return true;
            }
            if (@this is AggregateException aggregate)
            {
                if (aggregate.InnerExceptions.Count == 1 && aggregate.InnerExceptions[0] is OperationCanceledException)
                {
                    return true;
                }
                return !aggregate.AsEnumerable().Any(ex => !(ex is OperationCanceledException));
            }
            return false;
        }
    }
}
