using System;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Pqc.Crypto.Ntru;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;


namespace Shared
{
    public interface IAsymetricEncryptor : IAsyncDisposable
    {
        byte[] ExportPublicKey();

        bool ImportPublicKey(byte[] key);

        byte[]? TryEncrypt(byte[] content);

        byte[]? TryDecrypt(byte[] content);
    }

    public class NtruEncryptor : IAsymetricEncryptor
    {
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

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

    public class RsaAsymetricEncryptor : IAsymetricEncryptor
    {
        private readonly RSA _Rsa;

        public RsaAsymetricEncryptor()
        {
            _Rsa = RSA.Create();
        }

        public byte[] ExportPublicKey()
        {
            return _Rsa.ExportSubjectPublicKeyInfo();
        }

        public bool ImportPublicKey(byte[] key)
        {
            try
            {
                _Rsa.ImportSubjectPublicKeyInfo(key, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[]? TryEncrypt(byte[] content)
        {
            if (_Rsa == null || content == null)
                return null;

            try
            {
                return _Rsa.Encrypt(content, RSAEncryptionPadding.OaepSHA256);
            }
            catch
            {
                return null;
            }
        }

        public byte[]? TryDecrypt(byte[] content)
        {
            if (content == null)
                return null;

            try
            {
                return _Rsa.Decrypt(content, RSAEncryptionPadding.OaepSHA256);
            }
            catch
            {
                return null;
            }
        }
        public async ValueTask DisposeAsync()
        {
            _Rsa?.Dispose();
        }
    }
}