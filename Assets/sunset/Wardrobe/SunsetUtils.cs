using System;
using System.Security.Cryptography;

namespace DefaultNamespace.sunset.Wardrobe
{
    public class SunsetUtils
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

        public static string GenerateRandomSTring(int length)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

            var chars = new char[length];
            var bytes = new byte[length];

            // 生成 length 个随机字节
            RandomNumberGenerator.Fill(bytes);

            for (int i = 0; i < length; i++)
            {
                // 将随机字节映射到可用字符索引（0-35）
                int idx = bytes[i] % Alphabet.Length;
                chars[i] = Alphabet[idx];
            }

            return new string(chars);
        }
    }
}