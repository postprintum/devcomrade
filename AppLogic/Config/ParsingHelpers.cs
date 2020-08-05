// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AppLogic.Config
{
    internal static class ParsingHelpers
    {
        //TODO: unit tests
        public static bool TryParseHexOrDec(string? text, out int value)
        {
            // E.g.: 0x0A or 10
            var regex = new Regex(@"^\s*((0x(?<hex>-?[0-9A-F]+))|(?<dec>-?\d+))\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var match = regex.Match(text ?? String.Empty);
            if (match.Success)
            {
                var hex = match.Groups["hex"]?.Value;
                if (!String.IsNullOrEmpty(hex))
                {
                    value = Convert.ToInt32(hex, 16);
                    return true;
                }

                var dec = match.Groups["dec"]?.Value;
                if (!String.IsNullOrEmpty(dec))
                {
                    value = Convert.ToInt32(dec, 10);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryParseHexOrDecOrChar(string? text, out int value)
        {
            // E.g.: 0x0A or 10 or 'A'
            var regex = new Regex(@"^\s*((0x(?<hex>-?[0-9A-F]+))|(?<dec>-?\d+)|('(?<char>[0-9A-Z])'))\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var match = regex.Match(text ?? String.Empty);
            if (match.Success)
            {
                var hex = match.Groups["hex"]?.Value;
                if (!String.IsNullOrEmpty(hex))
                { 				
                    value = Convert.ToInt32(hex, 16);
                    return true;
                }

                var dec = match.Groups["dec"]?.Value;
                if (!String.IsNullOrEmpty(dec))
                {
                    value = Convert.ToInt32(dec, 10);
                    return true;
                }

                var character = match.Groups["char"]?.Value;
                if (!String.IsNullOrEmpty(character))
                {
                    value = (int)character.ToUpper()[0];
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryParseBool(string? text, out bool value)
        {
            // E.g.: true or false
            var regex = new Regex(@"^\s*(?<bool>(true|false))\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var match = regex.Match(text ?? String.Empty);
            if (match.Success)
            {
                var boolText = match.Groups["bool"]?.Value;
                return Boolean.TryParse(boolText, out value);
            }

            value = default;
            return false;
        }

        public static bool TryParseKeyValue(string? text, string key, [NotNullWhen(true)] out string? value)
        {
            // E.g.: "key: value;"
            var regex = new Regex($"(^|;)\\s*({key})\\s*:\\s*(?<value>.+?)\\s*(;|$)",
                RegexOptions.Singleline);

            var match = regex.Match(text ?? String.Empty);
            value = match.Groups["value"]?.Value;

            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
