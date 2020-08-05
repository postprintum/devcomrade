// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Configuration;
using System.Xml;

namespace AppLogic.Config
{
    internal static class ConfigurationExtensions
   {
        /// <summary>
        /// Get custom object for a section listed in <configSections>
        /// </summary>
        public static object? GetCustomSection(this System.Configuration.Configuration @this, string name)
        {
            var info = @this.GetSection(name)?.SectionInformation;
            if (info == null)
            {
                return null;
            }

            var type = Type.GetType(info.Type);
            if (type == null)
            {
                return null;
            }

            var xml = info.GetRawXml();
            if (String.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xml);

            var sectionHandler = Activator.CreateInstance(type) as IConfigurationSectionHandler;
            if (sectionHandler == null)
            {
                return null;
            }

            return sectionHandler.Create(null, null, xmlDocument.DocumentElement);
        }

        public static T? GetCustomSection<T>(this System.Configuration.Configuration @this, string name) 
            where T: class, new()
        {
            return @this.GetCustomSection(name) as T ?? new T();
        }
    }
}
