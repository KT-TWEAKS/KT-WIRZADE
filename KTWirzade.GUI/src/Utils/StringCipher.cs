using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Utils
{
    public static class StringCipher
    {
        private const int Keysize = 256;

        private const int DerivationIterations = 1000;

        public static string Encrypt(string plainText, string passPhrase)
        {
            byte[] saltStringBytes = Generate256BitsOfRandomEntropy();
            byte[] ivStringBytes = Generate256BitsOfRandomEntropy();
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, 1000);
            byte[] keyBytes = password.GetBytes(32);
            using RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.BlockSize = 256;
            symmetricKey.Mode = CipherMode.CBC;
            symmetricKey.Padding = PaddingMode.PKCS7;
            using ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes);
            using MemoryStream memoryStream = new MemoryStream();
            using CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] inArray = saltStringBytes.Concat(ivStringBytes).ToArray().Concat(memoryStream.ToArray())
                .ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(inArray);
        }

        public static string Decrypt(string cipherText, string passPhrase)
        {
            byte[] cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            byte[] saltStringBytes = cipherTextBytesWithSaltAndIv.Take(32).ToArray();
            byte[] ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(32).Take(32).ToArray();
            byte[] cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip(64).Take(cipherTextBytesWithSaltAndIv.Length - 64).ToArray();
            using Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, 1000);
            byte[] keyBytes = password.GetBytes(32);
            using RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.BlockSize = 256;
            symmetricKey.Mode = CipherMode.CBC;
            symmetricKey.Padding = PaddingMode.PKCS7;
            using ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes);
            using MemoryStream memoryStream = new MemoryStream(cipherTextBytes);
            using CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using StreamReader streamReader = new StreamReader(cryptoStream, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }

        private static byte[] Generate256BitsOfRandomEntropy()
        {
            byte[] randomBytes = new byte[32];
            using RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
            rngCsp.GetBytes(randomBytes);
            return randomBytes;
        }
    }
}
