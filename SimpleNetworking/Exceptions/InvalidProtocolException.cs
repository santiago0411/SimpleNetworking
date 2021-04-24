using System;

namespace SimpleNetworking.Exceptions
{
    public sealed class InvalidProtocolException : Exception
    {
        public InvalidProtocolException(string message)
            :base(message)
        {
        }
    }
}
