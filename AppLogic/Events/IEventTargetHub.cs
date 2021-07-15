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
    public interface IEventTargetHub
    {
        public EventTarget<T>? GetEventTarget<T>() 
            where T : EventArgs => 
            (this as IEventTargetProp<T>)?.Value;
    }
}
