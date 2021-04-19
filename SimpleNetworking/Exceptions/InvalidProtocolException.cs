using System;

namespace SimpleNetworking.Exceptions
{
    public sealed class InvalidProtocolException : Exception
    {
        #pragma warning disable CS0114
        public string Message { get; private set; }

        public InvalidProtocolException(string message)
        {
            Message = message;
        }
    }
}
