using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLib.Text
{
    public static class AsciiTools
    {
        public static string GetAsciiPreview(byte[] data)
        {
            var chars = new char[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];

                // printable ASCII range
                if (b >= 32 && b <= 126)
                    chars[i] = (char)b;
                else
                    chars[i] = '.';
            }

            return new string(chars);
        }
    }
}
