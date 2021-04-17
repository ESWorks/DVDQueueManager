using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DVDOrders.Services
{
    public static class Converter
    {
        public static Stream ToStream(string s)
        {
            return ToStream(s, Encoding.UTF8);
        }
        public static Stream ToStream(string s, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(s ?? ""));
        }
        public static string ByteArrayToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
