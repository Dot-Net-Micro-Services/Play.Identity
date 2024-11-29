using System;

namespace Play.Identity.Service.Exceptions;

[Serializable]
internal class UnknownUserException : Exception
{
    private Guid userId;

    public UnknownUserException(Guid userId) : base($"Unknown User '{userId}'")
    {
        this.userId = userId;
    }
}