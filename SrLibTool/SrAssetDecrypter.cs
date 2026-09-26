using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SrLibTool
{
    public static class SrAssetDecrypter
    {
        public const string KeyString = "zR4mXk9pL2qW7nT1vJ8cB5hF0dY3eS6u";

        private static readonly byte[] Salt = { 0x00, 0x49, 0x56 };  // "\0IV"

        private static (byte[] key, byte[] iv) _DeriveKeyAndIv(string keyString)
        {
            using var sha = SHA256.Create();
            var key = sha.ComputeHash(Encoding.UTF8.GetBytes(keyString));
            var iv = sha.ComputeHash(Encoding.UTF8.GetBytes(keyString).Concat(Salt).ToArray())[..16];
            return (key, iv);
        }

        public static byte[] LoadEncryptedAsset(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using var ms = new MemoryStream(encryptedData);
            return _Decrypt(ms, key, iv);
        }

        public static byte[] LoadEncryptedAsset(byte[] encryptedData, string keyString)
        {
            var (key, iv) = _DeriveKeyAndIv(keyString);
            return LoadEncryptedAsset(encryptedData, key, iv);
        }

        public static byte[] LoadEncryptedAsset(string filePath, byte[] key, byte[] iv)
        {
            return LoadEncryptedAsset(File.ReadAllBytes(filePath), key, iv);
        }

        public static byte[] LoadEncryptedAsset(string filePath, string keyString)
        {
            return LoadEncryptedAsset(File.ReadAllBytes(filePath), keyString);
        }

        private static byte[] _Decrypt(Stream inputStream, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
            using var gzipStream = new GZipStream(cryptoStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzipStream.CopyTo(output);
            return output.ToArray();
        }
    }
}
