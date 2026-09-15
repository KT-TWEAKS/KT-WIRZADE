using System;

namespace KTWirzade.Shared.Exceptions
{
    public class InvalidRegistryEntryException : Exception
    {
        public InvalidRegistryEntryException() { }
        public InvalidRegistryEntryException(string message) : base(message) { }
        public InvalidRegistryEntryException(string message, Exception inner) : base(message, inner) { }
    }
}
