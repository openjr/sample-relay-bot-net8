// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Universal.Microsoft.Bot.Connector.DirectLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using ChannelAccount = Universal.Microsoft.Bot.Connector.DirectLine.ChannelAccount;
using DirectLineActivity = Universal.Microsoft.Bot.Connector.DirectLine.Activity;
using DirectLineActivityTypes = Universal.Microsoft.Bot.Connector.DirectLine.ActivityTypes;
using IConversationUpdateActivity = Microsoft.Bot.Schema.IConversationUpdateActivity;
using IMessageActivity = Microsoft.Bot.Schema.IMessageActivity;

namespace Microsoft.PowerVirtualAgents.Samples.RelayBotSample.Bots
{
    /// <summary>
    /// This IBot implementation shows how to connect
    /// an external Azure Bot Service channel bot (external bot)
    /// to your Power Virtual Agent bot
    /// </summary>
    public class RelayBot : ActivityHandler
    {
        private const int WaitForBotResponseMaxMilSec = 5 * 1000;
        private const int PollForBotResponseIntervalMilSec = 1000;
        private static ConversationManager s_conversationManager = ConversationManager.Instance;
        private ResponseConverter _responseConverter;
        private IBotService _botService;

        public RelayBot(IBotService botService, ConversationManager conversationManager)
        {
            _botService = botService;
            _responseConverter = new ResponseConverter();
        }

        // Invoked when a conversation update activity is received from the external Azure Bot Service channel
        // Start a Power Virtual Agents bot conversation and store the mapping
        protected override async Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            await s_conversationManager.GetOrCreateBotConversationAsync(turnContext.Activity.Conversation.Id, _botService);
        }

        // Invoked when a message activity is received from the user
        // Send the user message to Power Virtual Agent bot and get response
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var currentConversation = await s_conversationManager.GetOrCreateBotConversationAsync(turnContext.Activity.Conversation.Id, _botService);

            using (DirectLineClient client = new DirectLineClient(currentConversation.Token))
            {
                // Send user message using directlineClient
                await client.PostActivityAsync(currentConversation.ConversationtId, new DirectLineActivity()
                {
                    Type = DirectLineActivityTypes.Message,
                    From = new ChannelAccount { Id = turnContext.Activity.From.Id, Name = turnContext.Activity.From.Name },
                    Text = turnContext.Activity.Text,
                    TextFormat = turnContext.Activity.TextFormat,
                    Locale = turnContext.Activity.Locale,
                }, cancellationToken);

                await RespondPowerVirtualAgentsBotReplyAsync(client, currentConversation, turnContext);
            }

            // Update LastConversationUpdateTime for session management
            currentConversation.LastConversationUpdateTime = DateTime.Now;
        }

        private async Task RespondPowerVirtualAgentsBotReplyAsync(
            DirectLineClient client, RelayConversation currentConversation, ITurnContext<IMessageActivity> turnContext)
        {
            var retryMax = WaitForBotResponseMaxMilSec / PollForBotResponseIntervalMilSec;
            for (int retry = 0; retry < retryMax; retry++)
            {
                Console.WriteLine($"[DL] poll {retry}, in-wm={currentConversation.WaterMark ?? "<null>"}");

                var response = await client.GetActivitiesAsync(currentConversation.ConversationtId, currentConversation.WaterMark);

                IEnumerable<DirectLineActivity> all = response?.Activities ?? [];
                Console.WriteLine($"[DL] got {all.Count()} activities, out-wm={response?.Watermark ?? "<null>"}");

                foreach (var a in all)
                    Console.WriteLine($"[DL] act type={a.Type} from.id={a.From?.Id} from.name={a.From?.Name} text='{a.Text}'");
                
                var botResponses = all
                    .Where(x => x.Type == DirectLineActivityTypes.Message)
                    .Where(x => string.Equals(x.From.Name, _botService.GetBotName(), StringComparison.Ordinal))
                    .ToList();

                if (botResponses.Count > 0)
                {
                    // OPTIONAL: comment out this equality check for now to avoid early return
                    // if (int.Parse(response?.Watermark ?? "0") <= int.Parse(currentConversation.WaterMark ?? "0")) return;
                    
                    if (int.Parse(response?.Watermark ?? "0") <= int.Parse(currentConversation.WaterMark ?? "0"))
                    {
                        // means user sends new message, should break previous response poll
                        return;
                    }
                    if (response != null) currentConversation.WaterMark = response.Watermark;
                    await turnContext.SendActivitiesAsync(_responseConverter.ConvertToBotSchemaActivities(botResponses).ToArray<IActivity>());
                }

                Thread.Sleep(PollForBotResponseIntervalMilSec);
            }

            Console.WriteLine("[DL] no replies within timeout");
        }
    }
}
