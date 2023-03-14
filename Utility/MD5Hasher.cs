using System.Linq;
using System.Security.Cryptography;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal sealed class MD5Hasher
    {
        internal string Hash(string @string)
        {
            using MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(@string));

            return hashBytes.Select((x) => x.ToString("x2")).Aggregate(string.Empty, (x, acc) => acc + x);
        }
    }
}