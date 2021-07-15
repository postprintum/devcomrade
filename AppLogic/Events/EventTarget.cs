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
    /// <summary>
    /// A simple event container for pub/sup scenarios, resembling JavaScript's EventTarget
    /// </summary>
    public class EventTarget<T> where T : EventArgs
    {
        public event EventHandler<T>? Event;

        public void Dispatch(object? source, T eventArgs) => this.Event?.Invoke(source, eventArgs);
    }
}
