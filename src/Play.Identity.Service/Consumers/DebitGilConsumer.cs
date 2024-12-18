using System;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Util;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Play.Identity.Contracts;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;

namespace Play.Identity.Consumers;

public class DebitGilConsumer : IConsumer<DebitGil>
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ILogger<DebitGilConsumer> logger;

    public DebitGilConsumer(UserManager<ApplicationUser> userManager, ILogger<DebitGilConsumer> logger)
    {
        this.userManager = userManager;
        this.logger = logger;
    }
    public async Task Consume(ConsumeContext<DebitGil> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Received Request to Debit {Gil} from the user {userId} for the purchase order with CorrelationId {CorrelationId}",
            message.Gil,
            message.UserId,
            message.CorrelationId
        );
        var user = await userManager.FindByIdAsync(message.UserId.ToString());
        if (user == null)
        {
            throw new UnknownUserException(message.UserId);
        }

        if(user.MessageIds.Contains(context.MessageId.Value))
        {
            await context.Publish(new GilDebited(message.CorrelationId));
            return;
        }

        if(user.Gil - message.Gil < 0)
        {
            logger.LogError(
                "Insufficient Funds to Debit {Gil} from the user {userId} for the purchase order with CorrelationId {CorrelationId}",
                message.Gil,
                message.UserId,
                message.CorrelationId
            );
            throw new InSufficientFundsException(message.UserId, message.Gil, user.Gil);
        }

        user.Gil -= message.Gil;

        user.MessageIds.Add(context.MessageId.Value);
        await userManager.UpdateAsync(user);

        var gilDebitedTask = context.Publish(new GilDebited(message.CorrelationId));
        var userUpdatedTask = context.Publish(new UserUpdated(user.Id, user.Email, user.Gil));

        await Task.WhenAll(gilDebitedTask, userUpdatedTask);
    }
}