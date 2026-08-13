// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using AppLogic.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

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

        public static bool TryGetOption(string name, [NotNullWhen(true)] out string? value) =>
            TryGetOption(RoamingOptions, LocalOptions, name, out value);

        /// <summary>
        /// The option resolution rule, kept separate from the config files so it can be tested.
        /// Roaming gets precedence over Local, the same way as it does for hotkeys,
        /// see <see cref="Presenter.HotkeyHandlerHost.InitializeHotkeys"/>
        /// </summary>
        internal static bool TryGetOption(
            OptionCollection roaming,
            OptionCollection local,
            string name,
            [NotNullWhen(true)] out string? value)
        {
            if (roaming.TryGetValue(name, out value))
            {
                return true;
            }
            if (local.TryGetValue(name, out value))
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
        /// All configured hotkeys, with the Roaming config applied over the Local one
        /// </summary>
        public static HotkeyCollection GetHotkeys() =>
            MergeHotkeys(RoamingHotkeys, LocalHotkeys);

        /// <summary>
        /// More than one hotkey can have the same name, which is how an action gets
        /// more than one key combination. So Roaming takes precedence per name rather
        /// than per entry: if the Roaming config names an action at all, its entries
        /// replace every Local entry for that action.
        /// Kept separate from the config files so it can be tested.
        /// </summary>
        internal static HotkeyCollection MergeHotkeys(
            IEnumerable<Hotkey> roaming,
            IEnumerable<Hotkey> local)
        {
            var overridden = new HashSet<string>(
                roaming.Select(hotkey => hotkey.Name),
                StringComparer.Ordinal);

            var result = new HotkeyCollection();
            result.AddRange(roaming);
            result.AddRange(local.Where(hotkey => !overridden.Contains(hotkey.Name)));
            return result;
        }

        /// <summary>
        /// Persist an option to the Roaming config, which is the config the user owns:
        /// it isn't overwritten when the app is upgraded, and it doesn't need admin
        /// rights to write to. The in-memory copy is updated as well, so the new value
        /// takes effect without a restart.
        /// </summary>
        public static void SetRoamingOption(string name, string value)
        {
            SaveRoamingOption(name, value);
            RoamingOptions[name] = value;
        }

        private static void SaveRoamingOption(string name, string value)
        {
            var path = RoamingConfigPath;

            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder!);
            }

            var document = new XmlDocument();
            if (File.Exists(path) && !File.ReadAllText(path).IsNullOrWhiteSpace())
            {
                document.Load(path);
            }
            else
            {
                // start off the same template as "Edit Roaming Config" does
                document.LoadXml(GetDefaultRoamingConfig());
            }

            SetOption(document, name, value);

            // NB: save via the path rather than a TextWriter,
            // so the declared utf-8 encoding is honored
            document.Save(path);
        }

        /// <summary>
        /// Add or update an option element within a config document,
        /// kept separate from the file access so it can be tested
        /// </summary>
        internal static void SetOption(XmlDocument document, string name, string value)
        {
            var root = document.DocumentElement;
            if (root == null)
            {
                throw new FormatException(nameof(document));
            }

            var options = root["options"];
            if (options == null)
            {
                options = document.CreateElement("options");
                root.AppendChild(options);
            }

            var option = options.ChildNodes
                .Cast<XmlNode>()
                .OfType<XmlElement>()
                .FirstOrDefault(node =>
                    node.Name == "option" &&
                    String.CompareOrdinal(node.GetAttribute("name"), name) == 0);

            if (option == null)
            {
                option = document.CreateElement("option");
                option.SetAttribute("name", name);
                options.AppendChild(option);
            }

            option.SetAttribute("value", value);
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
