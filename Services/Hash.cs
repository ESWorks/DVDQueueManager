using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// ReSharper disable InconsistentNaming

namespace DVDOrders.Services
{
    public enum HashType
    {
        MD5,
        SHA1,
        SHA256,
        SHA358,
        SHA512
    }
    public static class Hash
    {
        public static string StringHash(HashType type, string value, string salt = "")
        {
            return Converter.ByteArrayToHexString(ByteBlockHash(Encoding.UTF8.GetBytes(value + salt), type));
        }
        public static byte[] ByteBlockHash(byte[] file, HashType type)
        {
            switch (type)
            {
                case HashType.MD5:
                    return MD5H(file);
                case HashType.SHA1:
                    return SHA1H(file);
                case HashType.SHA256:
                    return SHA256H(file);
                case HashType.SHA358:
                    return SHA358H(file);
                case HashType.SHA512:
                    return SHA512H(file);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        public static byte[] MD5H(byte[] block)
        {
            using MD5 hash = MD5.Create();
            return hash.ComputeHash(block);
        }
        public static byte[] SHA1H(byte[] block)
        {
            using SHA1 hash = SHA1.Create();
            return hash.ComputeHash(block);
        }
        public static byte[] SHA256H(byte[] block)
        {
            using SHA256 hash = SHA256.Create();
            return hash.ComputeHash(block);
        }
        public static byte[] SHA358H(byte[] block)
        {
            using SHA384 hash = SHA384.Create();
            return hash.ComputeHash(block);
        }
        public static byte[] SHA512H(byte[] block)
        {
            using SHA512 hash = SHA512.Create();
            return hash.ComputeHash(block);
        }
    }
}
