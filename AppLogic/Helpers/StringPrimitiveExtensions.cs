// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AppLogic.Helpers
{
    /// <summary>
    /// Primitive String extension 
    /// TODO: unit tests
    /// </summary>
    internal static class StringPrimitiveExtensions
    {
        /// <summary>
        /// Using IsNotNullNorEmpty, especially as an extension method, may be an unpopular opinion,
        /// yet the typical <c>!String.IsNullOrEmpty(s)</c> has often been error-prone to me.
        /// Now, this is a personal project and I am not bound by corporate coding standards :)
        /// Using <c>#nullable enable</c> and <c>[NotNullWhen]</c> helps, as well.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNotNullNorEmpty([NotNullWhen(true)] this string? @this) =>
            !String.IsNullOrEmpty(@this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? @this) =>
            String.IsNullOrEmpty(@this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNotNullNorWhiteSpace([NotNullWhen(true)] this string? @this) =>
            !String.IsNullOrWhiteSpace(@this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? @this) =>
            String.IsNullOrWhiteSpace(@this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(this string @this) =>
            @this.Length == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNotEmpty(this string @this) =>
            (uint)@this.Length > 0u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Replace(this string @this, Regex regex, string replacement) =>
            regex.Replace(@this, replacement);
    }
}
