using System.Security.Cryptography;

namespace MeshtasticMqttExplorer.Services;

public class EncryptionHelper
{
    public static byte[] CreateNonce(ulong packetId, int fromNode)
    {
        // Create a buffer for the nonce
        var nonce = new byte[16];

        // Write packetId (64-bit)
        BitConverter.GetBytes(packetId).CopyTo(nonce, 0);

        // Write fromNode (32-bit)
        BitConverter.GetBytes(fromNode).CopyTo(nonce, 8);

        return nonce;
    }

    public static byte[] DecryptPacket(byte[] encryptedPacket, string decryptionKeyBase64, ulong packetId, int fromNode)
    {
        // Convert decryption key from Base64
        var key = Convert.FromBase64String(decryptionKeyBase64);

        key = PadKeyTo16Bytes(key);

        // Create decryption nonce for this packet
        var nonce = CreateNonce(packetId, fromNode);

        // Determine algorithm based on key length
        if (key.Length != 16 && key.Length != 32)
        {
            // Skip this key, try the next one...
            throw new ArgumentException($"Skipping decryption key with invalid length: {key.Length}");
        }

        // Create AES instance
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        // Create decryptor
        using var decryptor = aes.CreateDecryptor();
        return AesCtrTransform(encryptedPacket, decryptor, nonce);
    }

    private static byte[] AesCtrTransform(byte[] data, ICryptoTransform ecbEncryptor, byte[] nonce)
    {
        var blockSize = 16;
        var output = new byte[data.Length];

        // Process each block
        for (var i = 0; i < data.Length; i += blockSize)
        {
            // Increment nonce for each block
            IncrementNonce(nonce);

            // Encrypt the nonce to create the keystream block
            var keystreamBlock = new byte[blockSize];
            ecbEncryptor.TransformBlock(nonce, 0, blockSize, keystreamBlock, 0);

            // XOR the keystream block with the data block
            var bytesToProcess = Math.Min(blockSize, data.Length - i);
            for (var j = 0; j < bytesToProcess; j++)
            {
                output[i + j] = (byte)(data[i + j] ^ keystreamBlock[j]);
            }
        }

        return output;
    }

    private static void IncrementNonce(byte[] nonce)
    {
        for (var i = nonce.Length - 1; i >= 0; i--)
        {
            if (++nonce[i] != 0)
            {
                break;
            }
        }
    }
    
    public static byte[] PadKeyTo16Bytes(byte[] key)
    {
        if (key.Length >= 16)
        {
            return key;
        }

        byte[] paddedKey = new byte[16];
        Buffer.BlockCopy(key, 0, paddedKey, 0, key.Length);
        return paddedKey;
    }
}