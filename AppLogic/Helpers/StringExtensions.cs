// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AppLogic.Helpers
{
    internal static class StringExtensions
    {
        //TODO: unit tests, make Regexes static
        public static string Replace(this string @this, Regex regex, string replacement)
        {
            return regex.Replace(@this, replacement);
        }

        public static string NormalizeLineEndings(this string @this)
        {
            // use only "\n" for line breaks
            return Regex.Replace(@this, @"(\r\n)|(\n\r)|(\r)", "\n", RegexOptions.Singleline);
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
    }
}
