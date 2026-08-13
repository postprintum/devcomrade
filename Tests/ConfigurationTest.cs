// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information.
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Config;
using AppLogic.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Xml;

namespace Tests
{
    /// <summary>
    /// Tests for how Local and Roaming options are resolved against each other.
    /// Local config gets overwritten on upgrade, Roaming is the user's own
    /// customization, so Roaming has to win.
    /// </summary>
    [TestClass]
    public class ConfigurationTest
    {
        private const string OPTION = "removeClipboardFormatting";

        [TestMethod]
        public void Test_a_Roaming_option_takes_precedence_over_the_same_Local_option()
        {
            var roaming = new OptionCollection { [OPTION] = "false" };
            var local = new OptionCollection { [OPTION] = "true" };

            Assert.IsTrue(Configuration.TryGetOption(roaming, local, OPTION, out var value));
            Assert.AreEqual("false", value);
        }

        [TestMethod]
        public void Test_a_Local_option_is_used_when_Roaming_does_not_override_it()
        {
            var roaming = new OptionCollection();
            var local = new OptionCollection { [OPTION] = "true" };

            Assert.IsTrue(Configuration.TryGetOption(roaming, local, OPTION, out var value));
            Assert.AreEqual("true", value);
        }

        [TestMethod]
        public void Test_a_Roaming_only_option_is_used_when_Local_does_not_have_it()
        {
            var roaming = new OptionCollection { [OPTION] = "false" };
            var local = new OptionCollection();

            Assert.IsTrue(Configuration.TryGetOption(roaming, local, OPTION, out var value));
            Assert.AreEqual("false", value);
        }

        [TestMethod]
        public void Test_an_unknown_option_is_not_resolved_from_either_config()
        {
            var roaming = new OptionCollection();
            var local = new OptionCollection();

            Assert.IsFalse(Configuration.TryGetOption(roaming, local, OPTION, out var value));
            Assert.IsNull(value);
        }

        #region Hotkey merging

        private static Hotkey Hotkey(string name, uint mods, uint vkey) =>
            new Hotkey { Name = name, Mods = mods, Vkey = vkey };

        [TestMethod]
        public void Test_an_action_can_have_more_than_one_hotkey_in_the_same_config()
        {
            // this is what lets ShowMenu answer both Win+F10 and Win+Alt+.
            var local = new[]
            {
                Hotkey("ShowMenu", 0x8, 0x79),
                Hotkey("ShowMenu", 0x9, 0xBE)
            };

            var merged = Configuration.MergeHotkeys(new Hotkey[0], local);

            Assert.AreEqual(2, merged.Count);
            Assert.IsTrue(merged.All(hotkey => hotkey.Name == "ShowMenu"));
            CollectionAssert.AreEquivalent(
                new uint?[] { 0x79, 0xBE },
                merged.Select(hotkey => hotkey.Vkey).ToArray());
        }

        [TestMethod]
        public void Test_a_Roaming_hotkey_replaces_every_Local_hotkey_of_the_same_name()
        {
            var roaming = new[] { Hotkey("ShowMenu", 0x2, 0x70) };
            var local = new[]
            {
                Hotkey("ShowMenu", 0x8, 0x79),
                Hotkey("ShowMenu", 0x9, 0xBE)
            };

            var merged = Configuration.MergeHotkeys(roaming, local);

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual((uint?)0x70, merged[0].Vkey);
        }

        [TestMethod]
        public void Test_Roaming_hotkeys_do_not_disturb_Local_hotkeys_of_other_names()
        {
            var roaming = new[] { Hotkey("ShowMenu", 0x2, 0x70) };
            var local = new[]
            {
                Hotkey("ShowMenu", 0x8, 0x79),
                Hotkey("InsertGuid", 0x9, 0x47)
            };

            var merged = Configuration.MergeHotkeys(roaming, local);

            Assert.AreEqual(2, merged.Count);
            Assert.IsNotNull(merged.SingleOrDefault(hotkey => hotkey.Name == "InsertGuid"));
            Assert.AreEqual(
                (uint?)0x70,
                merged.Single(hotkey => hotkey.Name == "ShowMenu").Vkey);
        }

        [TestMethod]
        public void Test_the_Roaming_config_can_also_give_an_action_more_than_one_hotkey()
        {
            var roaming = new[]
            {
                Hotkey("ShowMenu", 0x2, 0x70),
                Hotkey("ShowMenu", 0x4, 0x71)
            };
            var local = new[] { Hotkey("ShowMenu", 0x8, 0x79) };

            var merged = Configuration.MergeHotkeys(roaming, local);

            Assert.AreEqual(2, merged.Count);
            CollectionAssert.AreEquivalent(
                new uint?[] { 0x70, 0x71 },
                merged.Select(hotkey => hotkey.Vkey).ToArray());
        }

        /// <summary>
        /// An action can appear in the menu without a hotkey. This is how
        /// ConvertToPreformattedHtml, InsertGuid and PresentationSettings are configured.
        /// </summary>
        [TestMethod]
        public void Test_a_hotkey_element_with_no_mods_or_vkey_is_a_menu_item_only()
        {
            var document = new XmlDocument();
            document.LoadXml(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><hotkeys>" +
                "<hotkey name=\"ConvertToPreformattedHtml\" menuItem=\"Convert to &lt;pre&gt;\" />" +
                "<hotkey name=\"PasteAsNumber\" menuItem=\"Paste as Decimal Number\" mods=\"0x9\" vkey=\"'N'\" />" +
                "</hotkeys>");

            var hotkeys = (HotkeyCollection)new HotkeyConfigSection()
                .Create(null!, null!, document.DocumentElement!);

            Assert.AreEqual(2, hotkeys.Count);

            var menuOnly = hotkeys.Single(hotkey => hotkey.Name == "ConvertToPreformattedHtml");
            Assert.IsFalse(menuOnly.HasHotkey, "it must not try to register a hotkey");
            Assert.AreEqual("Convert to <pre>", menuOnly.MenuItem, "it must stay in the menu");

            // the other entry still gets its hotkey
            var withHotkey = hotkeys.Single(hotkey => hotkey.Name == "PasteAsNumber");
            Assert.IsTrue(withHotkey.HasHotkey);
            Assert.AreEqual((uint?)0x9, withHotkey.Mods);
            Assert.AreEqual((uint?)'N', withHotkey.Vkey);
        }

        #endregion

        /// <summary>
        /// Parse a config document the same way the app itself does at startup
        /// </summary>
        private static OptionCollection ReadOptions(XmlDocument document)
        {
            var options = document.DocumentElement!["options"];
            Assert.IsNotNull(options);
            return (OptionCollection)new OptionConfigSection().Create(null!, null!, options!);
        }

        private static XmlDocument LoadDefaultRoamingConfig()
        {
            var document = new XmlDocument();
            document.LoadXml(Configuration.GetDefaultRoamingConfig());
            return document;
        }

        [TestMethod]
        public void Test_an_option_saved_to_the_default_roaming_config_can_be_read_back()
        {
            var document = LoadDefaultRoamingConfig();

            // the shipped template has no options set, only a comment
            Assert.AreEqual(0, ReadOptions(document).Count);

            Configuration.SetOption(document, OPTION, "false");

            var options = ReadOptions(document);
            Assert.AreEqual(1, options.Count);
            Assert.AreEqual("false", options[OPTION]);
        }

        [TestMethod]
        public void Test_saving_an_option_twice_updates_it_rather_than_duplicating_it()
        {
            var document = LoadDefaultRoamingConfig();

            Configuration.SetOption(document, OPTION, "false");
            Configuration.SetOption(document, OPTION, "true");

            // a duplicate would make OptionCollection.Add throw on the second entry
            var options = ReadOptions(document);
            Assert.AreEqual(1, options.Count);
            Assert.AreEqual("true", options[OPTION]);
        }

        [TestMethod]
        public void Test_an_option_can_be_saved_to_a_config_which_has_no_options_section()
        {
            var document = new XmlDocument();
            document.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration></configuration>");

            Configuration.SetOption(document, OPTION, "false");

            var options = ReadOptions(document);
            Assert.AreEqual(1, options.Count);
            Assert.AreEqual("false", options[OPTION]);
        }

        [TestMethod]
        public void Test_saving_an_option_leaves_the_other_options_alone()
        {
            var document = new XmlDocument();
            document.LoadXml(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><options>" +
                "<option name=\"tabSize\" value=\"2\" />" +
                "</options></configuration>");

            Configuration.SetOption(document, OPTION, "false");

            var options = ReadOptions(document);
            Assert.AreEqual(2, options.Count);
            Assert.AreEqual("2", options["tabSize"]);
            Assert.AreEqual("false", options[OPTION]);
        }
    }
}
