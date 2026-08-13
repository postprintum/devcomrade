// Copyright (C) 2020-2026 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information.
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Tests for the text mangling behind the paste actions
    /// </summary>
    [TestClass]
    public class StringExtensionsTest
    {
        [TestMethod]
        public void Test_Unindent_removes_the_common_indent()
        {
            Assert.AreEqual(
                "int a = 1;\nint b = 2;",
                "    int a = 1;\n    int b = 2;".Unindent());
        }

        [TestMethod]
        public void Test_Unindent_keeps_the_relative_indent()
        {
            Assert.AreEqual(
                "if (a)\n{\n    b();\n}",
                "        if (a)\n        {\n            b();\n        }".Unindent());
        }

        /// <summary>
        /// Regression: an interior blank line used to make Unindent return
        /// the input unchanged, so PasteUnindented did nothing for most real code
        /// </summary>
        [TestMethod]
        public void Test_Unindent_is_not_defeated_by_a_blank_line()
        {
            Assert.AreEqual(
                "int a = 1;\n\nint b = 2;",
                "    int a = 1;\n\n    int b = 2;".Unindent());
        }

        [TestMethod]
        public void Test_Unindent_is_not_defeated_by_a_whitespace_only_line()
        {
            // the middle line contains two spaces only
            Assert.AreEqual(
                "int a = 1;\n  \nint b = 2;",
                "    int a = 1;\n  \n    int b = 2;".Unindent());
        }

        [TestMethod]
        public void Test_Unindent_does_nothing_when_a_line_starts_at_column_0()
        {
            var text = "int a = 1;\n    int b = 2;";
            Assert.AreEqual(text, text.Unindent());
        }

        [TestMethod]
        public void Test_Unindent_does_nothing_for_blank_only_text()
        {
            Assert.AreEqual("", "".Unindent());
            Assert.AreEqual("\n", "\n".Unindent());
        }

        [TestMethod]
        public void Test_UnixifyLineEndings_normalizes_all_line_break_styles()
        {
            Assert.AreEqual("a\nb\nc\nd", "a\r\nb\rc\nd".UnixifyLineEndings());

            // a CRLF pair must become one line break, not two;
            // KeyboardInput.FeedTextAsync relies on this
            Assert.AreEqual("a\nb", "a\r\nb".UnixifyLineEndings());
        }

        [TestMethod]
        public void Test_TrimTrailingEmptyLines_trims_only_the_outer_blank_lines()
        {
            Assert.AreEqual(
                "a\n\nb",
                "\n \na\n\nb\n\n ".TrimTrailingEmptyLines());
        }
    }
}
