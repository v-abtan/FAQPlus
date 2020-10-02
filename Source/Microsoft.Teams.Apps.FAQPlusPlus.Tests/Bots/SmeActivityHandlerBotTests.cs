using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Teams.Apps.FAQPlusPlus.Bots;
using Microsoft.Teams.Apps.FAQPlusPlus.Common;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Providers;
using Microsoft.Teams.Apps.FAQPlusPlus.Properties;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Teams.Apps.FAQPlusPlus.Tests.Bots
{
    /// <summary>
    /// Add unit test coverage for user bot
    /// </summary>
    public class SmeActivityHandlerBotTests
    {
        private readonly SmeActivityHandler sut;
        private readonly TestAdapter smeBotAdapter;
        private readonly ChannelAccount expertAccount;
        private const string BotGenericAnswer = "This is generic bot answer.";

        public SmeActivityHandlerBotTests()
        {
            var mockConfigProvider = new Mock<IConfigurationDataProvider>();
            var botSettings = new BotSettings();
            var mockBotSettingsMonitor = Mock.Of<IOptionsMonitor<BotSettings>>(_ => _.CurrentValue == botSettings);
            var mockLogger = new Mock<ILogger<SmeActivityHandler>>();
            
            var ticketsProvider = new Mock<ITicketsProvider>();
            ticketsProvider.Setup(x => x.GetTicketAsync(It.IsAny<string>()))
                .Returns((string ticketId) => Task.FromResult(new TicketEntity
                {
                    TicketId = ticketId, Title = "Ticket Title", RequesterGivenName = "Expert #1",
                    RequesterUserPrincipalName = "Expert#1",
                    DateCreated = DateTime.Now.AddMinutes(-2),
                    RequesterBotId = "theSMEBot"
                }));
            ticketsProvider.Setup(x => x.UpsertTicketAsync(It.IsAny<TicketEntity>()))
                .Verifiable();

            string expertId = Guid.NewGuid().ToString(), expertName = "ExpertUser1";
            this.expertAccount = new ChannelAccount(id: expertId, aadObjectId: expertId, name: expertName);

            this.smeBotAdapter = GetSmeBotTestAdapter();
            this.sut = new SmeActivityHandler(mockConfigProvider.Object, new MicrosoftAppCredentials("", ""),
                ticketsProvider.Object, new Mock<IQnaServiceProvider>().Object,
                new Mock<IActivityStorageProvider>().Object, new Mock<ISearchService>().Object, this.smeBotAdapter, 
                new MemoryCache(new MemoryCacheOptions()), new Mock<IKnowledgeBaseSearchService>().Object,
                mockBotSettingsMonitor, mockLogger.Object);
        }

        private TestAdapter GetSmeBotTestAdapter()
        {
            var testAdapter = new TestAdapter(Channels.Msteams)
            {
                Conversation =
                {
                    Conversation = new ConversationAccount
                    {
                        ConversationType = ConversationTypes.ConversationTypeChannel
                    },
                    //Bot = new TeamsChannelAccount(id: "SMEBotId",)
                }
            };
            return testAdapter;
        }

        private Activity GetActivityWithText(string text, dynamic value = null)
        {
            var conversationActivity = new Activity
            {
                Text = text,
                TextFormat = "plain",
                Type = ActivityTypes.Message,
                ChannelId = "msteams",
                Recipient = new TeamsChannelAccount { Id = "theSMEBot" },
                Value = value == null ? null : JObject.FromObject(value),
                From = this.expertAccount
            };
            return conversationActivity;
        }

        [Fact]
        public async Task ReturnsHelpCardsOnTeamTour()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.TeamTour);

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received 3 hero cards.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Message, reply.Type);
            Assert.Equal(3, reply.Attachments.Count);
            Assert.Equal("application/vnd.microsoft.card.hero", reply.Attachments.First().ContentType);
        }

        [Fact]
        public async Task ReturnsOnlyTypingMessageOnNoCommand()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.NoCommand);

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that no extra reply received.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Null(reply);
        }

        [Fact]
        public async Task ReturnsUnrecognizedCardOnRandomMessage()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText("Non-existing action");

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received AskExpert card.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(1, reply.Attachments.Count);

            var shareFeedbackAttachment = reply.Attachments.First();
            Assert.Equal(HeroCard.ContentType, shareFeedbackAttachment.ContentType);
            Assert.IsType<HeroCard>(shareFeedbackAttachment.Content);
            var shareFeedbackCard = shareFeedbackAttachment.Content as HeroCard;

            // 5 AdaptiveElements in body
            Assert.NotNull(shareFeedbackCard);
            Assert.Equal(Strings.TeamCustomMessage, shareFeedbackCard.Text);

            // Submit action
            Assert.Single(shareFeedbackCard.Buttons);
            var messageBackAction = shareFeedbackCard.Buttons.First();
            Assert.IsType<CardAction>(messageBackAction);
            Assert.Equal(Strings.TakeATeamTourButtonText, messageBackAction.Title);
        }

        [Fact]
        public async Task ReturnsAssignedStatusCardOnChangeStatus()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText("change status", new ChangeTicketStatusPayload
            {
                Action = ChangeTicketStatusPayload.AssignToSelfAction,
                TicketId = "TICKETID"
            });

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received an confirmation to end-user.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal($"This request is now assigned. Assigned to {this.expertAccount.Name}.", reply.Text);

            //await this.smeBotAdapter.ContinueConversationAsync()
            // Assert that we received the card in teams expert channel.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(Strings.AssignedTicketUserNotification, reply.Summary);
            Assert.Equal(1, reply.Attachments.Count);

            var attachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, attachment.ContentType);
            var card = attachment.Content as AdaptiveCard;

            // Card should have this content: 
            // Here is what I found
            // Bot Answer
            Assert.Equal(2, card.Body.Count);
            Assert.IsType<AdaptiveFactSet>(card.Body[1]);
            Assert.NotNull(card.Body[1]);
            var factSet = card.Body[1] as AdaptiveFactSet;
            Assert.Equal(3, factSet.Facts.Count);
            var statusFact = factSet.Facts[0];
            Assert.Equal(Strings.StatusFactTitle, statusFact.Title);
            Assert.Equal(Strings.AssignedUserNotificationStatus, statusFact.Value);

            // Actions should be present only if ticket is closed
            Assert.Null(card.Actions);
        }
    }
}
