// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;

namespace AppLogic.Helpers
{
    /// <summary>
    /// String extension for easy fluent syntax chains
    /// TODO: unit tests, make Regexes static
    /// </summary>
    internal static class StringExtensions
    {
        public static string AsString(this IEnumerable<char> @this)
        {
            return String.Concat(@this);
        }

        public static string UnixifyLineEndings(this string @this)
        {
            // use only "\n" for line breaks
            return Regex.Replace(@this, @"(\r\n)|(\n\r)|(\r)", "\n", RegexOptions.Singleline);
        }

        public static string WindowsifyLineEndings(this string @this)
        {
            // use "\r\n" for line breaks
            return @this.Replace("\n", Environment.NewLine);
        }

        public static string RemoveSpaces(this string @this)
        {
            return String.Concat(@this.Where(c => !Char.IsWhiteSpace(c)));
        }

        public static string TrimTrailingEmptyLines(this string @this)
        {
            var regex = new Regex(@"^\s*$", RegexOptions.Singleline);

            var lines = @this.Split('\n')
                .SkipWhile(l => regex.IsMatch(l))
                .Reverse()
                .SkipWhile(l => regex.IsMatch(l))
                .Reverse();

            return String.Join('\n', lines);
        }

        public static string ConvertToSingleLine(this string @this)
        {
            return String.Join('\x20', @this.Split('\n').Select(l => l.Trim()));
        }

        public static string TabifyStart(this string @this, int tabSize)
        {
            var spaces = new String('\x20', tabSize);
            var regex = new Regex(@"^(\s+)(.+?)\s*$", RegexOptions.Multiline);
            return regex.Replace(@this, m =>
                $"{m.Groups[1].Value.Replace(spaces, "\t")}{m.Groups[2].Value}");
        }

        public static string UntabifyStart(this string @this, int tabSize)
        {
            var spaces = new String('\x20', tabSize);
            var regex = new Regex(@"^(\s+)(.+?)\s*$", RegexOptions.Multiline);
            return regex.Replace(@this, m =>
                $"{m.Groups[1].Value.Replace("\t", spaces)}{m.Groups[2].Value}");
        }

        public static string Unindent(this string @this)
        {
            var regexSpace = new Regex(@"^[\x20\t]+", RegexOptions.Singleline);

            var lines = @this.Split('\n');

            var indentSize = lines.Aggregate(
                int.MaxValue,
                (minSize, line) =>
                    regexSpace.Match(line) is var match && match.Success ?
                    (match.Value.Length is var size && size < minSize ? size : minSize) :
                    0);

            if (indentSize == 0 || indentSize == int.MaxValue)
            {
                return @this;
            }

            var removalRegex = new Regex($"^[\\x20\\t]{{{indentSize}}}", RegexOptions.Singleline);
            return String.Join('\n', lines.Select(l => removalRegex.Replace(l, String.Empty)));
        }

        public static string ToPreformattedHtml(this string @this)
        {
            return
                "<pre style=\"display: block; margin: 0; padding: 0; font-family: ui-monospace; " +
                "white-space: pre; line-height: normal; font-style: normal; font-size: 100%; " + 
                $"font-weight: normal; text-transform: none\">{HtmlEncoder.Default.Encode(@this)}\n</pre>";
        }
    }
}
