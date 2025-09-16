using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

public class CloudAdapterWithErrorHandler : CloudAdapter
{
    public CloudAdapterWithErrorHandler(BotFrameworkAuthentication auth, ILogger<CloudAdapter> logger)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            logger.LogError(exception, "Unhandled error in bot turn.");
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Oops—something went wrong and I couldn’t process that."), default);
        };
    }
}
