using System;

namespace Play.Identity.Service.Exceptions;

[Serializable]
internal class InSufficientFundsException : Exception
{
    private Guid userId;
    private decimal userGil;
    private decimal neededGil;

    public InSufficientFundsException(Guid userId, decimal neededGil, decimal userGil) : 
        base($"Not enough gil to debit {neededGil} from User '{userId}'. Available Gil: {userGil}")
    {
        this.userId = userId;
        this.userGil = userGil;
        this.neededGil = neededGil;
    }
}