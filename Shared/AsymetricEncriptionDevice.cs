using System;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Pqc.Crypto.Ntru;
using Org.BouncyCastle.Security;


namespace Shared
{
    public interface IAsymetricEncryptor
    {
        byte[] ExportPublicKey();

        bool ImportPublicKey(byte[] key);

        byte[]? TryEncrypt(byte[] content);

        byte[]? TryDecrypt(byte[] content);
    }

    public class NtruEncryptor : IAsymetricEncryptor
    {
        public byte[] ExportPublicKey()
        {
            throw new NotImplementedException();
        }

        public bool ImportPublicKey(byte[] key)
        {
            throw new NotImplementedException();
        }

        public byte[]? TryDecrypt(byte[] content)
        {
            throw new NotImplementedException();
        }

        public byte[]? TryEncrypt(byte[] content)
        {
            throw new NotImplementedException();
        }
    }
}