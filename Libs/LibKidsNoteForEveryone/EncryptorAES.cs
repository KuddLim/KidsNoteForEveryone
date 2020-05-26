using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LibKidsNoteForEveryone
{
    public class EncryptorAES
    {
        public static byte[] DefaultAesEncKey = Encoding.UTF8.GetBytes("KiDs_nOTe_BoT!!!");   // 변경해서 사용하는 것을 권장
        private static byte[] DefaultAesEncIV = Encoding.UTF8.GetBytes("~!@#$%^&*()_+|}{");    // 변경해서 사용하는 것을 권장

        static private AesCryptoServiceProvider CreateAesProvider(byte[] key)
        {
            return new AesCryptoServiceProvider
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key,
                IV = DefaultAesEncIV,
                Padding = PaddingMode.PKCS7,
                Mode = CipherMode.CBC
            };
        }

        static public string EncryptAes(string plain, byte[] key)
        {
            byte[] source = System.Text.Encoding.UTF8.GetBytes(plain);
            AesCryptoServiceProvider provider = CreateAesProvider(key);
            ICryptoTransform encryptor = provider.CreateEncryptor();

            byte[] encryptedBytes = encryptor.TransformFinalBlock(source, 0, source.Length);
            string encrypted = Convert.ToBase64String(encryptedBytes);
            return encrypted;
        }

        static public string DecryptAes(string encrypted, byte[] key)
        {
            byte[] source = Convert.FromBase64String(encrypted);
            AesCryptoServiceProvider provider = CreateAesProvider(key);
            ICryptoTransform encryptor = provider.CreateDecryptor();

            byte[] decryptedBytes = encryptor.TransformFinalBlock(source, 0, source.Length);
            string decrypted = System.Text.Encoding.UTF8.GetString(decryptedBytes);
            return decrypted;
        }

        public void EncryptChaCha(FileStream input, FileStream output, byte[] key)
        {
            //ParametersWithIV parameters = new ParametersWithIV(new KeyParameter(DefaultChaChaEncKey), DefaultChaChaEncNonce);
            //ChaChaEngine engine = new ChaChaEngine(20);
            //engine.Init(true, parameters);
            //engine.ProcessBytes(FullPacket, 0, FullPacket.Length, outData, 8);
            //Buffer.BlockCopy(nonce, 0, outData, 0, 8);
        }
    }
}
