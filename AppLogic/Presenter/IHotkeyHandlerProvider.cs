// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Models;
using System.Diagnostics.CodeAnalysis;

namespace AppLogic.Presenter
{
    public interface IHotkeyHandlerProvider
    {
        bool CanHandle(Hotkey hotkey, [NotNullWhen(true)] out HotkeyHandlerCallback? callback);
    }
}
