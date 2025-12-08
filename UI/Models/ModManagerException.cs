using System;

namespace FactorioModManager.Models
{
    public class ModManagerException : Exception
    {
        public ErrorCode Code { get; }

        public ModManagerException(ErrorCode code, string message) : base(message)
        {
            Code = code;
        }

        public ModManagerException(ErrorCode code, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
        }
    }
}
