// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using AppLogic.Models;
using System;
using System.Configuration;
using System.Linq;
using System.Xml;

namespace AppLogic.Config
{
    internal class HotkeyConfigSection : IConfigurationSectionHandler
    {
        public object Create(object parent, object context, XmlNode section)
        {
            var result = new HotkeyCollection();

            foreach (var node in section.ChildNodes
                .Cast<XmlNode>()
                .Where(child => child.Name == "hotkey"))
            {
                void throwFormatException() => throw new FormatException(node.OuterXml);

                var name = node!.Attributes!["name"]?.Value;
                if (name.IsNullOrWhiteSpace())
                {
                    throwFormatException();
                }

                var menuItem = node.Attributes["menuItem"]?.Value;

                uint? mods = null;
                var modsText = node.Attributes["mods"]?.Value;
                if (modsText != null)
                {
                    if (!ParsingHelpers.TryParseHexOrDec(modsText, out var modsValue))
                    {
                        throwFormatException();
                    }
                    mods = (uint)modsValue;
                }

                uint? vkey = null;
                var vkeyText = node.Attributes["vkey"]?.Value;
                if (vkeyText != null)
                {
                    if (!ParsingHelpers.TryParseHexOrDecOrChar(vkeyText, out var vkeyValue))
                    {
                        throwFormatException();
                    }
                    vkey = (uint)vkeyValue;
                }

                var addSeparator = false;
                var addSeparatorText = node.Attributes["hasSeparator"]?.Value;
                if (addSeparatorText != null && !ParsingHelpers.TryParseBool(addSeparatorText, out addSeparator))
                {
                    throwFormatException();
                }

                result.Add(new Hotkey
                {
                    Name = name!,
                    MenuItem = menuItem,
                    Mods = mods,
                    Vkey = vkey,
                    AddSeparator = addSeparator
                });
            }

            return result;
        }
    }
}