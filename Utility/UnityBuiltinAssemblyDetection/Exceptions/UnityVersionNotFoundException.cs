using System;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class UnityVersionNotFoundException : Exception
    {
        public UnityVersionNotFoundException()
        {
        }

        public UnityVersionNotFoundException(string message) : base(message)
        {
        }

        public UnityVersionNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
