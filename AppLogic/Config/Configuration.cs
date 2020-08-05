// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Models;
using System;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;

namespace AppLogic.Config
{
    /// <summary>
    /// Local settings might get overwritten every time the app is upgraded
    /// Roaming settings are for customization, they doesn't get overwritten 
    /// and they have precedence over Local settings
    /// </summary>
    internal static class Configuration
    {
        public static OptionCollection LocalOptions => _localOptions.Value;
        public static OptionCollection RoamingOptions => _roamingOptions.Value;

        public static HotkeyCollection LocalHotkeys => _localHotkeys.Value;
        public static HotkeyCollection RoamingHotkeys => _roamingHotkeys.Value;

        public static string LocalConfigPath => _localConfig.Value.FilePath;
        public static string RoamingConfigPath => _roamingConfig.Value.FilePath;

        private static readonly Lazy<System.Configuration.Configuration> _localConfig;
        private static readonly Lazy<System.Configuration.Configuration> _roamingConfig;

        private static readonly Lazy<OptionCollection> _localOptions;
        private static readonly Lazy<OptionCollection> _roamingOptions;

        private static readonly Lazy<HotkeyCollection> _localHotkeys;
        private static readonly Lazy<HotkeyCollection> _roamingHotkeys;

        static Configuration()
        {
            // lazy initialization for proper error reporting
            _localConfig = new Lazy<System.Configuration.Configuration>(() => 
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None), 
                isThreadSafe: false);
            _roamingConfig = new Lazy<System.Configuration.Configuration>(() =>
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoaming),
                isThreadSafe: false);

            const string optionsSection = "options";
            _localOptions = new Lazy<OptionCollection>(() =>
                _localConfig.Value.GetCustomSection<OptionCollection>(optionsSection) ?? new OptionCollection(),
                isThreadSafe: false);
            _roamingOptions = new Lazy<OptionCollection>(() =>
                _roamingConfig.Value.GetCustomSection<OptionCollection>(optionsSection) ?? new OptionCollection(),
                isThreadSafe: false);

            const string hotkeysSection = "hotkeys";
            _localHotkeys = new Lazy<HotkeyCollection>(() =>
                _localConfig.Value.GetCustomSection<HotkeyCollection>(hotkeysSection) ?? new HotkeyCollection(),
                isThreadSafe: false);
            _roamingHotkeys = new Lazy<HotkeyCollection>(() =>
                _roamingConfig.Value.GetCustomSection<HotkeyCollection>(hotkeysSection) ?? new HotkeyCollection(),
                isThreadSafe: false);
        }

        public static bool TryGetOption(string name, [NotNullWhen(true)] out string? value)
        {
            if (LocalOptions.TryGetValue(name, out value))
            {
                return true;
            }
            if (RoamingOptions.TryGetValue(name, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        public static string GetOption(string name, string defaultValue)
        {
            if (TryGetOption(name, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        public static bool GetOption(string name, bool defaultValue)
        {
            if (TryGetOption(name, out var textValue))
            {
                if (ParsingHelpers.TryParseBool(textValue, out var value))
                {
                    return value;
                }
            }
            return defaultValue;
        }

        public static int GetOption(string name, int defaultValue)
        {
            if (TryGetOption(name, out var textValue))
            {
                if (int.TryParse(textValue, out var value))
                {
                    return value;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Template for roaming user settings
        /// </summary>
        public static string GetDefaultRoamingConfig()
        {
            var resource = $"{nameof(AppLogic)}.User.config";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null)
            {
                throw new FileNotFoundException(resource);
            }
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
