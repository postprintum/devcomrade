// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tests
{
    [TestClass]
    public class InputHelpersTest
    {
        [TestMethod]
        public async Task Test_TimerYield_in_WinFormsApartment()
        {
            await using var apartment = new WinFormsApartment();
            var sw = new Stopwatch();

            await apartment.Run(async () =>
            {
                Assert.IsTrue(SynchronizationContext.Current is WindowsFormsSynchronizationContext);

                sw.Start();
                await InputUtils.TimerYield(1000);
                sw.Stop();
            });

            var lapse = sw.ElapsedMilliseconds;
            Assert.IsTrue(lapse > 900 && lapse < 1100);
        }
    }
}
