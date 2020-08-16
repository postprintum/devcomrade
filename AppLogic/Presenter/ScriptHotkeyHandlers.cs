// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using AppLogic.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace AppLogic.Presenter
{
    /// <summary>
    /// Script actions for hotkeys
    /// </summary>
    internal class ScriptHotkeyHandlers: IHotkeyHandlerProvider
    {
        public IHotkeyHandlerHost Host { get; }

        // C# scripts as hotkey handlers: 
        // https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples
        private readonly Lazy<Dictionary<string, ScriptRunner<object>>> _scriptRunners =
            new Lazy<Dictionary<string, ScriptRunner<object>>>(isThreadSafe: false);

        public ScriptHotkeyHandlers(IHotkeyHandlerHost host)
        {
            Host = host;
        }

        private static Lazy<ScriptOptions> ScriptOptions { get; } =
            new Lazy<ScriptOptions>(CreateScriptOptions, isThreadSafe: false);

        private static ScriptOptions CreateScriptOptions()
        {
            return Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                .WithReferences(
                    typeof(Enumerable).Assembly,
                    typeof(CancellationToken).Assembly,
                    typeof(StringBuilder).Assembly,
                    typeof(Regex).Assembly)
                .WithImports(
                    "System",
                    "System.Text",
                    "System.Text.RegularExpressions",
                    "System.Collections.Generic",
                    "System.Linq");
        }

        public async Task ExecuteScript(Hotkey hotkey, CancellationToken token)
        {
            using var threadInputScope = AttachedThreadInputScope.Create();
            using var waitCursor = WaitCursorScope.Create();

            var scriptRunners = _scriptRunners.Value;
            if (!scriptRunners.TryGetValue(hotkey.Name, out var scriptRunner))
            {
                var code = hotkey.Data!.ToString();
                scriptRunner = await Task.Run(() =>
                {
                    var script = CSharpScript.Create<object>(
                        code: code,
                        options: ScriptOptions.Value,
                        globalsType: typeof(IScriptGlobals));

                    script.Compile(token);
                    return script.CreateDelegate();
                }, token);

                scriptRunners.Add(hotkey.Name, scriptRunner);
            }

            var globals = new ScriptGlobals(this.Host, token);
            var result = await scriptRunner(globals, token);
            if (result is Task task)
            {
                await task;
            }

            Host.PlayNotificationSound();
        }

        bool IHotkeyHandlerProvider.CanHandle(Hotkey hotkey, [NotNullWhen(true)] out HotkeyHandlerCallback? callback)
        {
            if (hotkey.IsScript && hotkey.Data.IsNotNullNorWhiteSpace())
            {
                callback = this.ExecuteScript;
                return true;
            }
            callback = default;
            return false;
        }
    }
}
