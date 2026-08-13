// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
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
        public bool AddSeparator { get; set; }

        public bool HasHotkey => Vkey.HasValue && Mods.HasValue;

        // NB: an action can have more than one hotkey, so two entries with the same
        // Name are not the same hotkey. The key combination is part of the identity.
        public override bool Equals(object? obj)
        {
            return (obj is Hotkey other) &&
                Name.Equals(other.Name) &&
                Mods == other.Mods &&
                Vkey == other.Vkey;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Mods, Vkey);
        }
    }
}
