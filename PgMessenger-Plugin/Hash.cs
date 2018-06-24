using System.Security.Cryptography;
using System.Text;

namespace PgMessenger
{
    public static class MD5Hash
    {
        static MD5Hash()
        {
            algorithm = MD5.Create();  //or use SHA256.Create();
        }

        public static string GetHashString(string inputString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        private static byte[] GetHash(string inputString)
        {
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        private static HashAlgorithm algorithm;
    }
}
