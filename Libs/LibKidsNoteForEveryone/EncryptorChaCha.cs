using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Crypto.Engines;

namespace LibKidsNoteForEveryone
{
    public class EncryptorChaCha
    {
        public static byte[] DefaultChaChaEncKey = Encoding.UTF8.GetBytes("나의차차기본키입니다^^");//("`~1!2@3#4$5%6^7&8*9(0)-_=+]}[{/?");
        public static byte[] DefaultChaChaEncNonce = Encoding.UTF8.GetBytes("19530124");

        private bool Encrypting;
        public byte[] Key { get; }
        public byte[] Nonce { get; }
        private ChaChaEngine Engine;

        public EncryptorChaCha(bool encrypting, byte[] key, byte[] nonce)
        {
            Encrypting = encrypting;
            Key = key;
            Nonce = nonce;

            ParametersWithIV parameters = new ParametersWithIV(new KeyParameter(DefaultChaChaEncKey), DefaultChaChaEncNonce);

            Engine = new ChaChaEngine(20);
            Engine.Init(encrypting, parameters);
        }

        public void Process(byte[] input, int srcPos, int srcLen, byte[] output, int outPos)
        {
            Engine.ProcessBytes(input, srcPos, srcLen, output, outPos);
        }
    }
}
