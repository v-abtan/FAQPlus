using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
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
    /// Add unit test coverage for SME bot
    /// </summary>
    public class SmeActivityHandlerBotTests
    {
        private readonly SmeActivityHandler sut;
        private readonly TestAdapter smeBotAdapter;
        private readonly ChannelAccount expertAccount;
        private readonly Mock<IQnaServiceProvider> qnaServiceProvider;
        private const string ChangeStatus = "change status";
        private const string TicketId = "TICKETID";

        public SmeActivityHandlerBotTests()
        {
            var mockConfigProvider = new Mock<IConfigurationDataProvider>();
            var botSettings = new BotSettings();
            var mockBotSettingsMonitor = Mock.Of<IOptionsMonitor<BotSettings>>(_ => _.CurrentValue == botSettings);
            var mockLogger = new Mock<ILogger<SmeActivityHandler>>();

            string expertId = Guid.NewGuid().ToString(), expertName = "ExpertUser1";
            this.expertAccount = new ChannelAccount(id: expertId, aadObjectId: expertId, name: expertName);

            var ticketsProvider = new Mock<ITicketsProvider>();
            ticketsProvider.Setup(x => x.GetTicketAsync(It.IsAny<string>()))
                .Returns((string ticketId) => Task.FromResult(new TicketEntity
                {
                    TicketId = ticketId, Title = "Ticket Title", RequesterGivenName = this.expertAccount.Name,
                    RequesterUserPrincipalName = this.expertAccount.Name,
                    DateCreated = DateTime.Now.AddMinutes(-2),
                    RequesterBotId = "theSMEBot",
                    LastModifiedByName = this.expertAccount.Name
                }));

            this.qnaServiceProvider = new Mock<IQnaServiceProvider>();
            this.smeBotAdapter = GetSmeBotTestAdapter();
            this.sut = new SmeActivityHandler(mockConfigProvider.Object, new MicrosoftAppCredentials("", ""),
                ticketsProvider.Object, qnaServiceProvider.Object,
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
                    }
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
            var  conversationActivity = GetActivityWithText(Constants.TeamTour);

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
            Assert.Equal(HeroCard.ContentType, reply.Attachments.First().ContentType);
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

            Assert.NotNull(shareFeedbackCard);
            Assert.Equal(Strings.TeamCustomMessage, shareFeedbackCard.Text);

            // Submit action exist
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
            var conversationActivity = GetActivityWithText(ChangeStatus, new ChangeTicketStatusPayload
            {
                Action = ChangeTicketStatusPayload.AssignToSelfAction,
                TicketId = TicketId
            });

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received an confirmation to end-user.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, Strings.SMEAssignedStatus, this.expertAccount.Name), reply.Text);

            // Assert that we received the card in teams expert channel.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(Strings.AssignedTicketUserNotification, reply.Summary);
            Assert.Equal(1, reply.Attachments.Count);

            var attachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, attachment.ContentType);
            var card = attachment.Content as AdaptiveCard;

            // Card should have this content: 
            // Description:
            // Status: Assigned to *****
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
        
        [Fact]
        public async Task ReturnsClosedStatusCardOnChangeStatus()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(ChangeStatus, new ChangeTicketStatusPayload
            {
                Action = ChangeTicketStatusPayload.CloseAction,
                TicketId = TicketId
            });

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received an confirmation to end-user.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, Strings.SMEClosedStatus, this.expertAccount.Name), reply.Text);
            
            // Assert that we received the card in teams expert channel.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(Strings.ClosedTicketUserNotification, reply.Summary);
            Assert.Equal(1, reply.Attachments.Count);

            var attachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, attachment.ContentType);
            var card = attachment.Content as AdaptiveCard;

            // Card should have this content: 
            // Description:
            // Status: Assigned to *****
            Assert.Equal(2, card.Body.Count);
            Assert.IsType<AdaptiveFactSet>(card.Body[1]);
            Assert.NotNull(card.Body[1]);
            var factSet = card.Body[1] as AdaptiveFactSet;
            Assert.Equal(4, factSet.Facts.Count);
            var statusFact = factSet.Facts[0];
            Assert.Equal(Strings.StatusFactTitle, statusFact.Title);
            Assert.Equal(Strings.ClosedUserNotificationStatus, statusFact.Value);

            // Last card should be closed card
            Assert.Equal(Strings.ClosedFactTitle, factSet.Facts[factSet.Facts.Count - 1].Title);
            
            // Actions should be present only if ticket is closed
            Assert.NotNull(card.Actions);
        }

        [Fact]
        public async Task ReturnsReOpenStatusCardOnChangeStatus()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(ChangeStatus, new ChangeTicketStatusPayload
            {
                Action = ChangeTicketStatusPayload.ReopenAction,
                TicketId = TicketId
            });

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received an confirmation to end-user.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, Strings.SMEOpenedStatus, this.expertAccount.Name), reply.Text);

            // Assert that we received the card in teams expert channel.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(Strings.ReopenedTicketUserNotification, reply.Summary);
            Assert.Equal(1, reply.Attachments.Count);

            var attachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, attachment.ContentType);
            var card = attachment.Content as AdaptiveCard;

            // Card should have this content: 
            // Description:
            // Status: Assigned to *****
            Assert.Equal(2, card.Body.Count);
            Assert.IsType<AdaptiveFactSet>(card.Body[1]);
            Assert.NotNull(card.Body[1]);
            var factSet = card.Body[1] as AdaptiveFactSet;
            Assert.Equal(3, factSet.Facts.Count);
            var statusFact = factSet.Facts[0];
            Assert.Equal(Strings.StatusFactTitle, statusFact.Title);
            Assert.Equal(Strings.UnassignedUserNotificationStatus, statusFact.Value);

            // Actions should be present only if ticket is closed
            Assert.Null(card.Actions);
        }

        [Fact]
        public async Task ReturnsSuccessfulOnDeleteQNAPair()
        {
            // Arrange
            qnaServiceProvider.Setup(x => x.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<bool>(), null, null))
                .Returns((string question, bool isTestKnowledgeBase, string previousQnAId, string previousUserQuery) =>
                    Task.FromResult(new QnASearchResultList
                    {
                        Answers = new List<QnASearchResult>
                        {
                            new QnASearchResult(new List<string>
                            {
                                question
                            }) {Id = 1, Metadata = new List<MetadataDTO>()}
                        }
                    }));

            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.DeleteCommand, new AdaptiveSubmitActionData
            {
                UpdateHistoryData = string.Empty,
                OriginalQuestion = nameof(ReturnsSuccessfulOnDeleteQNAPair)
            });

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that no extra reply received
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Null(reply);
        }


        [Fact]
        public async Task ReturnsWaitingOnDeleteQNANonPublishedPair()
        {
            // Arrange
            qnaServiceProvider.Setup(x => x.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<bool>(), null, null))
                .Returns((string question, bool isTestKnowledgeBase, string previousQnAId, string previousUserQuery) =>
                    Task.FromResult(new QnASearchResultList
                    {
                        Answers = new List<QnASearchResult>
                        {
                            new QnASearchResult(new List<string>
                            {
                                question
                            }) {Id = isTestKnowledgeBase ? 1 : -1, Metadata = new List<MetadataDTO>()}
                        }
                    }));

            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.DeleteCommand, new AdaptiveSubmitActionData
            {
                UpdateHistoryData = string.Empty,
                OriginalQuestion = nameof(ReturnsWaitingOnDeleteQNANonPublishedPair)
            });

            // Act
            // Send the message activity to the bot.
            await this.smeBotAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received a wait message.
            reply = (IMessageActivity)this.smeBotAdapter.GetNextReply();
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, Strings.WaitMessage, nameof(ReturnsWaitingOnDeleteQNANonPublishedPair)), reply.Text);
        }
    }
}
