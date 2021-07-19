using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppLogic.Helpers
{
    internal static class ClipboardFormats
    {
        static readonly string HEADER = 
            "Version:0.9:\n" +
            "StartHTML:{0:00000}\n" +
            "EndHTML:{1:00000}\n" +
            "StartFragment:{2:00000}\n" +
            "EndFragment:{3:00000}\n";

        static readonly string HTML_START =
            "<html>\n" +
            "<body>\n" +
            "<!–StartFragment–>";

        static readonly string HTML_END =
            "<!–EndFragment–>\n" +
            "</body>\n" +
            "</html>\n";

        public static byte[] ConvertHtmlToClipboardData(string html)
        {
            var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var data = Array.Empty<byte>();

            var header = encoding.GetBytes(String.Format(HEADER, 0, 1, 2, 3));
            data = data.Concat(header).ToArray();

            var startHtml = data.Length;
            data = data.Concat(encoding.GetBytes(HTML_START)).ToArray();

            var startFragment = data.Length;
            data = data.Concat(encoding.GetBytes(html)).ToArray();

            var endFragment = data.Length;
            data = data.Concat(encoding.GetBytes(HTML_END)).ToArray();

            var endHtml = data.Length;

            // patch the header
            var newHeader = encoding.GetBytes(
                String.Format(HEADER, startHtml, startFragment, endFragment, endHtml));
            if (newHeader.Length != startHtml)
            {
                throw new InvalidOperationException(nameof(ConvertHtmlToClipboardData));
            }

            Array.Copy(header, data, length: startHtml);
            return data;
        } 
    }
}
