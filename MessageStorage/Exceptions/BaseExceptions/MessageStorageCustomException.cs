using System;

namespace MessageStorage.Exceptions.BaseExceptions
{
    public abstract class MessageStorageCustomException : Exception
    {
        protected MessageStorageCustomException(string friendlyMessage) : base(friendlyMessage)
        {
        }
    }
}