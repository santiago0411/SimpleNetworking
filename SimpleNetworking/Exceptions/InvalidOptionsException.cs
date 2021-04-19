using System;

namespace SimpleNetworking.Exceptions
{
    public sealed class InvalidOptionsException : Exception
    {
        #pragma warning disable CS0114
        public string Message { get; private set; }

        public InvalidOptionsException(string message)
        {
            Message = message;
        }
    }
}
