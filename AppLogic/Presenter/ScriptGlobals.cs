// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System.Threading;

namespace AppLogic.Presenter
{
    public class ScriptGlobals : IScriptGlobals
    {
        public IHotkeyHandlerHost Host { get; }
        public CancellationToken Token { get; }

        public ScriptGlobals(IHotkeyHandlerHost host, CancellationToken token)
        {
            this.Host = host;
            this.Token = token;
        }
    }
}
