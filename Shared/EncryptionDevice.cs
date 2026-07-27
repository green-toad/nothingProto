using JabrAPI;
using static JabrAPI.OutputInterval.IntervalFilters.FilterType;
using static JabrAPI.OutputInterval.IntervalFilters.FilterSelectionState;
using System.Collections.Generic;
using AVcontrol;


namespace Shared
{
    public class EncryptionDevice
    {
        private readonly RE5.BinaryKey _sendReKey    = new();
        private readonly RE5.BinaryKey _receiveReKey = new();

        private List<byte[]> keyparts = new();

        public byte[] ExportSendKey()    => _sendReKey.ExportAsBinary();
        public byte[] ExportReceiveKey() => _receiveReKey.ExportAsBinary();



        public void ApplyCustomSettings()
        {
            _sendReKey.Set.Default();
            _sendReKey.Noisifier.settings = new()
            {
                DynamicOutputIntervals =
                [
                    new(50.0, 100, 1000),
                    new(80.0, 300, 333)
                ],

                IntervalChoiceSetting = new
                (
                    ANY, MAX, MIN,
                    MIN, ANY, MAX,
                    [
                        ABSOLUTE_DIFFERENCE,
                        MAX_OUT_LENGTH,
                        MIN_OUT_LENGTH,
                        OUT_LENGTH_RANGE,
                        DIFFERENCE_TO_MAX,
                        DIFFERENCE_TO_MIN
                    ]
                ),

                LengthChoiceSetting = OutputInterval.LengthChoiceSetting.CHOOSE_RANDOM_FROM_VALID,

                ForceOptimalEntropy = true,
                ExpectedEntropy = ExpectedEntropy.C1_Medium,

                PrimaryNoiseBiasPercents = 50.0,
                ComplexNoisePairBiasPercents = 25.0,
                ComplexNoiseIntervalBiasPercents = 66.66
            };
        }
        public void UpdateSendKey() => _sendReKey.Next();


        public void ImportEncryptedReceiveKey(byte[] keyExport)
            => _receiveReKey.ImportFromBinary(RE5.Decrypt.WithNoise.Binary([.. keyExport], _sendReKey));
        
        public void ImportReceiveKeyWithoutDecrypt(byte[] keyExport)
            => _receiveReKey.ImportFromBinary([.. keyExport]);
        
        public byte[] EncryptWithReciveKey(byte[] content)
            => [.. RE5.Encrypt.WithNoise.Binary([.. content], _receiveReKey)];

        public int AddPartOfKey(byte[] keyFrame)
        {
            keyparts.Add(keyFrame);
            return keyparts.Count;
        }
        
        public void ApplyReceiveKeyWithParts()
            => _receiveReKey.ImportFromBinary(Combine.ToArray(keyparts));


        public byte[] Encrypt(byte[] content)
            => [.. RE5.Encrypt.WithNoise.Binary([.. content], _sendReKey)];
        public byte[] Decrypt(byte[] content)
            => [.. RE5.Decrypt.WithNoise.Binary([.. content], _receiveReKey)];
    }
}