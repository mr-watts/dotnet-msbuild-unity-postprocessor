using System;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal sealed class UnityGuidGenerator
    {
        private readonly MD5Hasher md5Hasher;

        internal UnityGuidGenerator(MD5Hasher md5Hasher)
        {
            this.md5Hasher = md5Hasher;
        }

        internal string Generate()
        {
            return md5Hasher.Hash(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK"));
        }
    }
}