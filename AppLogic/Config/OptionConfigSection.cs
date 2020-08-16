// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
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
    internal class OptionConfigSection : IConfigurationSectionHandler
    {
        public object Create(object parent, object context, XmlNode section)
        {
            var result = new OptionCollection();

            foreach (var node in section.ChildNodes
                .Cast<XmlNode>()
                .Where(child => child.Name == "option"))
            {
                void throwFormatException() => throw new FormatException(node.OuterXml);

                var name = node.Attributes["name"]?.Value;
                if (name.IsNullOrWhiteSpace())
                {
                    throwFormatException();
                }

                var value = node.Attributes["value"]?.Value ?? String.Empty;
                result.Add(name!, value);
            }

            return result;
        }
    }
}