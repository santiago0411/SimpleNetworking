using System;

namespace SimpleNetworking.Exceptions
{
    public sealed class InvalidOptionsException : Exception
    {
        public InvalidOptionsException(string message)
            :base(message)
        {
        }
    }
}
