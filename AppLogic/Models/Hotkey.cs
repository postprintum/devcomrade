// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;

namespace AppLogic.Models
{
    public class Hotkey
    {
        public string Name { get; set; } = String.Empty;
        public string? MenuItem { get; set; }
        public uint? Mods { get; set; }
        public uint? Vkey { get; set; }
        public bool IsScript { get; set; }
        public bool AddSeparator { get; set; }
        public string? Data { get; set; }

        public bool HasHotkey => Vkey.HasValue && Mods.HasValue;

        public override bool Equals(object? obj)
        {
            return (obj is Hotkey other) && Name.Equals(other.Name);
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
