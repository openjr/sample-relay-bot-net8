using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.PowerVirtualAgents.Samples.RelayBotSample.Bots;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerVirtualAgents.Samples.RelayBotSample;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var botService = new BotService();
builder.Configuration.GetSection("BotService").Bind(botService);
builder.Services.AddSingleton<IBotService>(botService);

var conversationManager = new ConversationManager();
builder.Configuration.GetSection("ConversationPool").Bind(conversationManager);
builder.Services.AddSingleton(conversationManager);

// Configuration: put these in appsettings.json or environment variables in dev
// "MicrosoftAppType", "MicrosoftAppId", "MicrosoftAppPassword" (and any custom settings)
builder.Services.AddHttpClient();

// Bot auth + adapter (CloudAdapter replaces BotFrameworkHttpAdapter)
builder.Services.AddSingleton<BotFrameworkAuthentication>(sp =>
    new ConfigurationBotFrameworkAuthentication(builder.Configuration));

builder.Services.AddSingleton<IBotFrameworkHttpAdapter, CloudAdapterWithErrorHandler>();
builder.Services.AddTransient<IBot, RelayBot>();


// Register any services your relay uses (e.g., Direct Line client/service, storage, etc.)
builder.Services.AddSingleton<ConversationState>(sp =>
{
    var storage = new MemoryStorage(); // swap for BlobStorage/Redis in prod
    return new ConversationState(storage);
});

// If you used Controllers before, you can remove them; we’ll just map the BF endpoint directly.
var app = builder.Build();

// Bot Framework endpoint required by Azure Bot Service & Emulator
app.MapPost("/api/messages",
    async (HttpRequest req, HttpResponse res, IBotFrameworkHttpAdapter adapter, IBot bot, CancellationToken ct) =>
        await adapter.ProcessAsync(req, res, bot, ct));

app.Run();

// dumb comment 
