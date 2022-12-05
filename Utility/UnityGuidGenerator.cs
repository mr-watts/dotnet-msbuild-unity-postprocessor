using System;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class UnityGuidGenerator
    {
        private readonly MD5Hasher md5Hasher;

        public UnityGuidGenerator(MD5Hasher md5Hasher)
        {
            this.md5Hasher = md5Hasher;
        }

        public string Generate()
        {
            return md5Hasher.Hash(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK"));
        }
    }
}