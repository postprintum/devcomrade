// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]
[assembly: AssemblyCopyright("Copyright (c) 2020 Postprintum Pty Ltd")]

namespace DevComrade
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppLogic.Program.Main(args);
        }
    }
}
