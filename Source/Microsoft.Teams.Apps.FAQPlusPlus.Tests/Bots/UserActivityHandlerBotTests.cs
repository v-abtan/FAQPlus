﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Teams.Apps.FAQPlusPlus.Bots;
using Microsoft.Teams.Apps.FAQPlusPlus.Common;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Providers;
using Microsoft.Teams.Apps.FAQPlusPlus.Properties;
using Moq;
using Xunit;

namespace Microsoft.Teams.Apps.FAQPlusPlus.Tests.Bots
{
    /// <summary>
    /// Add unit test coverage for user bot
    /// </summary>
    public class UserActivityHandlerBotTests
    {
        private readonly UserActivityHandler sut;
        private const string BotGenericAnswer = "This is generic bot answer.";

        public UserActivityHandlerBotTests()
        {
            var mockConfigProvider = new Mock<IConfigurationDataProvider>();
            var botSettings = new BotSettings();
            var mockBotSettingsMonitor = Mock.Of<IOptionsMonitor<BotSettings>>(_ => _.CurrentValue == botSettings);
            var mockLogger = new Mock<ILogger<UserActivityHandler>>();
            var mockQnaService = new Mock<IQnaServiceProvider>();
            mockQnaService.Setup(x => x.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<bool>(), null, null))
                .Returns(() =>
                    Task.FromResult(new QnASearchResultList(new List<QnASearchResult>
                    {
                        new QnASearchResult
                        {
                            Id = 0,
                            Answer = BotGenericAnswer,
                            Context = new QnASearchResultContext(prompts: new List<PromptDTO>())
                        }
                    })));
            
            this.sut = new UserActivityHandler(mockConfigProvider.Object, new MicrosoftAppCredentials("", ""),
                new Mock<ITicketsProvider>().Object, mockQnaService.Object,
                mockBotSettingsMonitor, mockLogger.Object);
        }

        private TestAdapter GetUserBotTestAdapter()
        {
            var testAdapter = new TestAdapter(Channels.Msteams)
            {
                Conversation =
                {
                    Conversation = new ConversationAccount
                    {
                        ConversationType = ConversationTypes.ConversationTypePersonal
                    }
                }
            };
            return testAdapter;
        }

        private Activity GetActivityWithText(string text)
        {
            var conversationActivity = new Activity
            {
                Text = text,
                TextFormat = "plain",
                Type = ActivityTypes.Message,
                ChannelId = "msteams",
                Recipient = new ChannelAccount { Id = "theBot" },
            };
            return conversationActivity;
        }

        [Fact]
        public async Task ReturnsHelpCardsOnTakeATour()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.TakeATour);
            var testAdapter = GetUserBotTestAdapter();

            // Act
            // Send the message activity to the bot.
            await testAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received 3 hero cards.
            reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Message, reply.Type);
            Assert.Equal(3, reply.Attachments.Count);
            Assert.Equal("application/vnd.microsoft.card.hero", reply.Attachments.First().ContentType);
        }

        [Fact]
        public async Task ReturnsAskExpertCardsOnAskExpertMessage()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.AskAnExpert);
            var testAdapter = GetUserBotTestAdapter();

            // Act
            // Send the message activity to the bot.
            await testAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received AskExpert card.
            reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Message, reply.Type);
            Assert.Equal(1, reply.Attachments.Count);
            var askExpertAttachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, askExpertAttachment.ContentType);
            Assert.IsType<AdaptiveCard>(askExpertAttachment.Content);
            var askExpertCard = askExpertAttachment.Content as AdaptiveCard;
            
            // Submit action
            Assert.NotNull(askExpertCard);
            Assert.Single(askExpertCard.Actions);
            var submitAction = askExpertCard.Actions.First();
            Assert.IsType<AdaptiveSubmitAction>(submitAction);
            Assert.Equal(Strings.AskAnExpertButtonText, submitAction.Title);
        }

        [Fact]
        public async Task ReturnsThankYouCardsOnShareFeedback()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText(Constants.ShareFeedback);
            var testAdapter = GetUserBotTestAdapter();

            // Act
            // Send the message activity to the bot.
            await testAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received AskExpert card.
            reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Message, reply.Type);
            Assert.Equal(1, reply.Attachments.Count);
            
            var shareFeedbackAttachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, shareFeedbackAttachment.ContentType);
            Assert.IsType<AdaptiveCard>(shareFeedbackAttachment.Content);
            var shareFeedbackCard = shareFeedbackAttachment.Content as AdaptiveCard;

            // 5 AdaptiveElements in body
            Assert.NotNull(shareFeedbackCard);
            Assert.Equal(5, shareFeedbackCard.Body.Count);

            // Submit action
            Assert.Single(shareFeedbackCard.Actions);
            var submitAction = shareFeedbackCard.Actions.First();
            Assert.IsType<AdaptiveSubmitAction>(submitAction);
            Assert.Equal(Strings.ShareFeedbackButtonText, submitAction.Title);
        }

        [Fact]
        public async Task ReturnsQnaGenericAnswerCardOnQuestion()
        {
            // Arrange
            // Create conversation activity
            var conversationActivity = GetActivityWithText("Basic Question?");
            var testAdapter = GetUserBotTestAdapter();

            // Act
            // Send the message activity to the bot.
            await testAdapter.ProcessActivityAsync(conversationActivity, this.sut.OnTurnAsync, CancellationToken.None);

            // Assert we got the typing message
            var reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Typing, reply.Type);

            // Assert that we received an answer from bot QNA service.
            reply = (IMessageActivity)testAdapter.GetNextReply();
            Assert.Equal(ActivityTypes.Message, reply.Type);
            Assert.Equal(1, reply.Attachments.Count);

            var attachment = reply.Attachments.First();
            Assert.Equal(AdaptiveCard.ContentType, attachment.ContentType);
            var card = attachment.Content as AdaptiveCard;
            
            // Card should have this content: 
            // Here is what I found
            // Bot Answer
            Assert.Equal(2, card.Body.Count);
            Assert.IsType<AdaptiveTextBlock>(card.Body[1]);
            Assert.Equal(BotGenericAnswer, (card.Body[card.Body.Count - 1] as AdaptiveTextBlock).Text);
        }
    }
}