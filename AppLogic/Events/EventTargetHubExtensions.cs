// Copyright (C) 2020+ by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppLogic.Events
{
    public static class EventTargetHubExtensions
    {
        public static bool HasEventTarget<T>(this IEventTargetHub @this) 
            where T : EventArgs =>
            @this.GetEventTarget<T>() != null;

        public static void AddListener<T>(this IEventTargetHub @this, EventHandler<T> listener)
            where T : EventArgs =>
            @this.GetEventTarget<T>()!.Event += listener;

        public static void RemoveListener<T>(this IEventTargetHub @this, EventHandler<T> listener)
            where T : EventArgs =>
            @this.GetEventTarget<T>()!.Event -= listener;

        public static void Dispatch<T>(this IEventTargetHub @this, object? source, T eventArgs) 
            where T : EventArgs =>
            @this.GetEventTarget<T>()?.Dispatch(source, eventArgs);
    }
}
