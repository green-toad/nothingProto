using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.IO;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Security;

namespace Shared
{
    public interface IAsymetricEncryptor : IAsyncDisposable
    {
        byte[] ExportPublicKey();

        bool ImportPublicKey(byte[] key);

        byte[]? TryEncrypt(byte[] content);

        byte[]? TryDecrypt(byte[] content);
    }
    public class EccEncryptor : IAsymetricEncryptor
    {
        private const int KeySize = 65;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly ECDiffieHellman _static;
        private readonly byte[] _staticPublicKeyBytes;
        private byte[]? _peerPublicKeyBytes;

        public EccEncryptor()
        {
            _static = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            _staticPublicKeyBytes = ExportPublicKeyBytes(_static);
        }

        // Экспорт публичного ключа в виде несжатой точки (65 байт)
        private static byte[] ExportPublicKeyBytes(ECDiffieHellman ecdh)
        {
            var parameters = ecdh.ExportParameters(false);
            var result = new byte[1 + 32 + 32];
            result[0] = 0x04;
            Buffer.BlockCopy(parameters.Q.X, 0, result, 1, 32);
            Buffer.BlockCopy(parameters.Q.Y, 0, result, 1 + 32, 32);
            return result;
        }

        public byte[] ExportPublicKey() => (byte[])_staticPublicKeyBytes.Clone();

        public bool ImportPublicKey(byte[] key)
        {
            if (key == null || key.Length != KeySize || key[0] != 0x04)
                return false;

            try
            {
                _peerPublicKeyBytes = (byte[])key.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ECDiffieHellmanPublicKey ImportPublicKeyFromBytes(byte[] keyBytes)
        {
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = keyBytes[1..33],
                    Y = keyBytes[33..65]
                }
            };
            using var temp = ECDiffieHellman.Create();
            temp.ImportParameters(parameters);
            return temp.PublicKey;
        }

        public byte[]? TryEncrypt(byte[] content)
        {
            if (_peerPublicKeyBytes == null || content == null)
                return null;

            try
            {
                using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
                byte[] ephemeralPublic = ExportPublicKeyBytes(ephemeral);

                using var peerPublic = ImportPublicKeyFromBytes(_peerPublicKeyBytes);
                byte[] sharedSecret = ephemeral.DeriveKeyMaterial(peerPublic);

                byte[] info = new byte[ephemeralPublic.Length + _staticPublicKeyBytes.Length];
                Buffer.BlockCopy(ephemeralPublic, 0, info, 0, ephemeralPublic.Length);
                Buffer.BlockCopy(_staticPublicKeyBytes, 0, info, ephemeralPublic.Length, _staticPublicKeyBytes.Length);

                byte[] aesKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, outputLength: 32, salt: null, info: info);

                byte[] nonce = new byte[NonceSize];
                RandomNumberGenerator.Fill(nonce);

                byte[] cipherText = new byte[content.Length];
                byte[] tag = new byte[TagSize];

                using (var aesGcm = new AesGcm(aesKey, TagSize))
                {
                    aesGcm.Encrypt(nonce, content, cipherText, tag);
                }

                using var ms = new MemoryStream();
                ms.Write(ephemeralPublic, 0, ephemeralPublic.Length);
                ms.Write(nonce, 0, nonce.Length);
                ms.Write(tag, 0, tag.Length);
                ms.Write(cipherText, 0, cipherText.Length);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public byte[]? TryDecrypt(byte[] content)
        {
            if (_static == null || content == null)
                return null;

            const int headerSize = KeySize + NonceSize + TagSize;
            if (content.Length < headerSize)
                return null;

            try
            {
                // 1. Извлекаем компоненты
                byte[] ephemeralPublic = new byte[KeySize];
                byte[] nonce = new byte[NonceSize];
                byte[] tag = new byte[TagSize];

                Array.Copy(content, 0, ephemeralPublic, 0, KeySize);
                Array.Copy(content, KeySize, nonce, 0, NonceSize);
                Array.Copy(content, KeySize + NonceSize, tag, 0, TagSize);

                int cipherTextLength = content.Length - headerSize;
                byte[] cipherText = new byte[cipherTextLength];
                Array.Copy(content, headerSize, cipherText, 0, cipherTextLength);

                // 2. Восстанавливаем эфемерный публичный ключ и вычисляем общий секрет
                using var ephemeralPublicKey = ImportPublicKeyFromBytes(ephemeralPublic);
                byte[] sharedSecret = _static.DeriveKeyMaterial(ephemeralPublicKey);

                // 3. Формируем info (как при шифровании)
                byte[] info = new byte[ephemeralPublic.Length + _staticPublicKeyBytes.Length];
                Buffer.BlockCopy(ephemeralPublic, 0, info, 0, ephemeralPublic.Length);
                Buffer.BlockCopy(_staticPublicKeyBytes, 0, info, ephemeralPublic.Length, _staticPublicKeyBytes.Length);

                // 4. Получаем ключ AES
                byte[] aesKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, outputLength: 32, salt: null, info: info);

                // 5. Расшифровываем
                byte[] plainText = new byte[cipherTextLength];
                using (var aesGcm = new AesGcm(aesKey, TagSize))
                {
                    aesGcm.Decrypt(nonce, cipherText, tag, plainText);
                }

                return plainText;
            }
            catch
            {
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _static?.Dispose();
            await ValueTask.CompletedTask;
        }
    }

    public class X25519Encryptor : IAsymetricEncryptor
    {
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly SecureRandom _random = new SecureRandom();
        private X25519PrivateKeyParameters? _staticPrivateKey;
        private X25519PublicKeyParameters? _staticPublicKey;

        private X25519PublicKeyParameters? _peerPublicKey;

        public X25519Encryptor()
        {
            var generator = new X25519KeyPairGenerator();
            generator.Init(new X25519KeyGenerationParameters(_random));

            var keyPair = generator.GenerateKeyPair();
            _staticPrivateKey = (X25519PrivateKeyParameters)keyPair.Private;
            _staticPublicKey = (X25519PublicKeyParameters)keyPair.Public;
        }

        public byte[] ExportPublicKey()
        {
            return _staticPublicKey!.GetEncoded();
        }

        public bool ImportPublicKey(byte[] key)
        {
            if (key == null || key.Length != KeySize)
                return false;

            try
            {
                _peerPublicKey = new X25519PublicKeyParameters(key, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[]? TryEncrypt(byte[] content)
        {
            if (_peerPublicKey == null || content == null)
                return null;

            try
            {
                var ephemeralGenerator = new X25519KeyPairGenerator();
                ephemeralGenerator.Init(new X25519KeyGenerationParameters(_random));
                var ephemeralKeyPair = ephemeralGenerator.GenerateKeyPair();

                var ephemeralPrivate = (X25519PrivateKeyParameters)ephemeralKeyPair.Private;
                var ephemeralPublic = (X25519PublicKeyParameters)ephemeralKeyPair.Public;

                var agreement = new X25519Agreement();
                agreement.Init(ephemeralPrivate);
                byte[] sharedSecret = new byte[agreement.AgreementSize];
                agreement.CalculateAgreement(_peerPublicKey, sharedSecret, 0);

                byte[] ephemeralPublicEncoded = ephemeralPublic.GetEncoded();

                byte[] aesKey = DeriveKey(sharedSecret, ephemeralPublicEncoded, _staticPublicKey!.GetEncoded());

                byte[] nonce = new byte[NonceSize];
                RandomNumberGenerator.Fill(nonce);

                byte[] cipherText = new byte[content.Length];
                byte[] tag = new byte[TagSize];

                using (var aesGcm = new AesGcm(aesKey, TagSize))
                {
                    aesGcm.Encrypt(nonce, content, cipherText, tag);
                }
                using var ms = new MemoryStream();
                ms.Write(ephemeralPublicEncoded, 0, ephemeralPublicEncoded.Length);
                ms.Write(nonce, 0, nonce.Length);
                ms.Write(tag, 0, tag.Length);
                ms.Write(cipherText, 0, cipherText.Length);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public byte[]? TryDecrypt(byte[] content)
        {
            if (_staticPrivateKey == null || content == null)
                return null;

            const int headerSize = KeySize + NonceSize + TagSize;
            if (content.Length < headerSize)
                return null;

            try
            {
                byte[] ephemeralPublicEncoded = new byte[KeySize];
                byte[] nonce = new byte[NonceSize];
                byte[] tag = new byte[TagSize];

                Array.Copy(content, 0, ephemeralPublicEncoded, 0, KeySize);
                Array.Copy(content, KeySize, nonce, 0, NonceSize);
                Array.Copy(content, KeySize + NonceSize, tag, 0, TagSize);

                int cipherTextLength = content.Length - headerSize;
                byte[] cipherText = new byte[cipherTextLength];
                Array.Copy(content, headerSize, cipherText, 0, cipherTextLength);

                var ephemeralPublic = new X25519PublicKeyParameters(ephemeralPublicEncoded, 0);

                var agreement = new X25519Agreement();
                agreement.Init(_staticPrivateKey);
                byte[] sharedSecret = new byte[agreement.AgreementSize];
                agreement.CalculateAgreement(ephemeralPublic, sharedSecret, 0);

                byte[] aesKey = DeriveKey(sharedSecret, ephemeralPublicEncoded, _staticPublicKey!.GetEncoded());

                byte[] plainText = new byte[cipherTextLength];
                using (var aesGcm = new AesGcm(aesKey, TagSize))
                {
                    aesGcm.Decrypt(nonce, cipherText, tag, plainText);
                }

                return plainText;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DeriveKey(byte[] sharedSecret, byte[] ephemeralPub, byte[] staticPub)
        {
            byte[] info = new byte[ephemeralPub.Length + staticPub.Length];
            Buffer.BlockCopy(ephemeralPub, 0, info, 0, ephemeralPub.Length);
            Buffer.BlockCopy(staticPub, 0, info, ephemeralPub.Length, staticPub.Length);

            return HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, outputLength: 32, salt: null, info: info);
        }

        public ValueTask DisposeAsync()
        {
            _staticPrivateKey = null;
            _staticPublicKey = null;
            _peerPublicKey = null;
            return ValueTask.CompletedTask;
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