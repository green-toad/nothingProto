using JabrAPI;

namespace ServerSide
{
    public class EncryptManager
    {
        private readonly RE5.BinaryKey _myReKey = new();
        private readonly RE5.BinaryKey _otherReKey = new(); 

        public byte[] GetMyKey()
        {
            return _myReKey.ExportAsBinary();
        }
        public void ChangeMyKey()
        {
            _myReKey.Next();
        }
        public void SetOtherKey(byte[] key)
        {
            _otherReKey.ImportFromBinary(key);
        }
        public byte[] Encrypt(byte[] content)
        {
            return RE5.Encrypt.WithNoise.Binary([.. content], _myReKey).ToArray();
        }
        public byte[] Decrypt(byte[] content)
        {
            return RE5.Decrypt.WithNoise.Binary([.. content], _otherReKey).ToArray();
        }
    }
}