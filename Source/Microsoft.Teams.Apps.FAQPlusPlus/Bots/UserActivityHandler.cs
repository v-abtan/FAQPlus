// <copyright file="UserActivityHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Bots
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Connector.Authentication;
    using Microsoft.Bot.Schema;
    using Microsoft.Bot.Schema.Teams;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Teams.Apps.FAQPlusPlus.Cards;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Providers;
    using Microsoft.Teams.Apps.FAQPlusPlus.Helpers;
    using Microsoft.Teams.Apps.FAQPlusPlus.Properties;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using ErrorResponseException = Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models.ErrorResponseException;

    /// <summary>
    /// Class that handles the user activity of Faq Plus bot and messaging extension.
    /// </summary>
    public class UserActivityHandler : CommonTeamsActivityHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FaqPlusPlusBot"/> class.
        /// </summary>
        /// <param name="configurationProvider">Configuration Provider.</param>
        /// <param name="microsoftAppCredentials">Microsoft app credentials to use.</param>
        /// <param name="ticketsProvider">Tickets Provider.</param>
        /// <param name="qnaServiceProvider">Question and answer maker service provider.</param>
        /// <param name="optionsAccessor">A set of key/value application configuration properties for FaqPlusPlus bot.</param>
        /// <param name="logger">Instance to send logs to the Application Insights service.</param>
        public UserActivityHandler(
            IConfigurationDataProvider configurationProvider,
            MicrosoftAppCredentials microsoftAppCredentials,
            ITicketsProvider ticketsProvider,
            IQnaServiceProvider qnaServiceProvider,
            IOptionsMonitor<BotSettings> optionsAccessor,
            ILogger<UserActivityHandler> logger)
            : base(configurationProvider, microsoftAppCredentials, ticketsProvider, qnaServiceProvider, optionsAccessor.CurrentValue, logger)
        {
        }

        /// <summary>
        /// Invoked when a message activity is received from the user.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// Reference link: https://docs.microsoft.com/en-us/dotnet/api/microsoft.bot.builder.activityhandler.onmessageactivityasync?view=botbuilder-dotnet-stable.
        /// </remarks>
        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var message = turnContext?.Activity;
                this.logger.LogInformation($"from: {message.From?.Id}, conversation: {message.Conversation.Id}, replyToId: {message.ReplyToId}");
                await this.SendTypingIndicatorAsync(turnContext).ConfigureAwait(false);

                switch (message.Conversation.ConversationType.ToLower())
                {
                    case ConversationTypes.Personal:
                        await this.OnMessageActivityInPersonalChatAsync(
                            message,
                            turnContext,
                            cancellationToken).ConfigureAwait(false);
                        break;

                    default:
                        this.logger.LogWarning($"Received unexpected conversationType {message.Conversation.ConversationType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(Strings.ErrorMessage).ConfigureAwait(false);
                this.logger.LogError(ex, $"Error processing message: {ex.Message}", SeverityLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// Invoke when a conversation update activity is received from the channel.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// Reference link: https://docs.microsoft.com/en-us/dotnet/api/microsoft.bot.builder.teams.teamsactivityhandler.onconversationupdateactivityasync?view=botbuilder-dotnet-stable.
        /// </remarks>
        protected override async Task OnConversationUpdateActivityAsync(
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var activity = turnContext?.Activity;
                this.logger.LogInformation("Received conversationUpdate activity");
                this.logger.LogInformation($"conversationType: {activity.Conversation.ConversationType}, membersAdded: {activity.MembersAdded?.Count}, membersRemoved: {activity.MembersRemoved?.Count}");

                if (activity?.MembersAdded?.Count == 0)
                {
                    this.logger.LogInformation("Ignoring conversationUpdate that was not a membersAdded event");
                    return;
                }

                switch (activity.Conversation.ConversationType.ToLower())
                {
                    case ConversationTypes.Personal:
                        await this.OnMembersAddedToPersonalChatAsync(activity.MembersAdded, turnContext).ConfigureAwait(false);
                        return;

                    default:
                        this.logger.LogInformation($"Ignoring event from conversation type {activity.Conversation.ConversationType}");
                        return;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error processing conversationUpdate: {ex.Message}", SeverityLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// Handle 1:1 chat with members who started chat for the first time.
        /// </summary>
        /// <param name="membersAdded">Channel account information needed to route a message.</param>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task OnMembersAddedToPersonalChatAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext)
        {
            var activity = turnContext.Activity;
            if (membersAdded.Any(channelAccount => channelAccount.Id == activity.Recipient.Id))
            {
                // User started chat with the bot in personal scope, for the first time.
                this.logger.LogInformation($"Bot added to 1:1 chat {activity.Conversation.Id}");
                var welcomeText = await this.configurationProvider.GetSavedEntityDetailAsync(ConfigurationEntityTypes.WelcomeMessageText).ConfigureAwait(false);
                var userWelcomeCardAttachment = WelcomeCard.GetCard(welcomeText);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(userWelcomeCardAttachment)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handle message activity in 1:1 chat.
        /// </summary>
        /// <param name="message">A message in a conversation.</param>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task OnMessageActivityInPersonalChatAsync(
            IMessageActivity message,
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(message.ReplyToId) && (message.Value != null) && ((JObject)message.Value).HasValues)
            {
                this.logger.LogInformation("Card submit in 1:1 chat");
                await this.OnAdaptiveCardSubmitInPersonalChatAsync(message, turnContext, cancellationToken).ConfigureAwait(false);
                return;
            }

            string text = message.Text?.ToLower()?.Trim() ?? string.Empty;

            switch (text)
            {
                case Constants.AskAnExpert:
                    this.logger.LogInformation("Sending user ask an expert card");
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(AskAnExpertCard.GetCard())).ConfigureAwait(false);
                    break;

                case Constants.ShareFeedback:
                    this.logger.LogInformation("Sending user feedback card");
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(ShareFeedbackCard.GetCard())).ConfigureAwait(false);
                    break;

                case Constants.TakeATour:
                    this.logger.LogInformation("Sending user tour card");
                    var userTourCards = TourCarousel.GetUserTourCards(this.appBaseUri);
                    await turnContext.SendActivityAsync(MessageFactory.Carousel(userTourCards)).ConfigureAwait(false);
                    break;

                default:
                    this.logger.LogInformation("Sending input to QnAMaker");
                    await this.GetQuestionAnswerReplyAsync(turnContext, message).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Handle adaptive card submit in 1:1 chat.
        /// Submits the question or feedback to the SME team.
        /// </summary>
        /// <param name="message">A message in a conversation.</param>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task OnAdaptiveCardSubmitInPersonalChatAsync(
            IMessageActivity message,
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            Attachment smeTeamCard = null;      // Notification to SME team
            Attachment userCard = null;         // Acknowledgement to the user
            TicketEntity newTicket = null;      // New ticket

            switch (message?.Text)
            {
                case Constants.AskAnExpert:
                    this.logger.LogInformation("Sending user ask an expert card (from answer)");
                    var askAnExpertPayload = ((JObject)message.Value).ToObject<ResponseCardPayload>();
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(AskAnExpertCard.GetCard(askAnExpertPayload))).ConfigureAwait(false);
                    break;

                case Constants.ShareFeedback:
                    this.logger.LogInformation("Sending user share feedback card (from answer)");
                    var shareFeedbackPayload = ((JObject)message.Value).ToObject<ResponseCardPayload>();
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(ShareFeedbackCard.GetCard(shareFeedbackPayload))).ConfigureAwait(false);
                    break;

                case AskAnExpertCard.AskAnExpertSubmitText:
                    this.logger.LogInformation("Received question for expert");
                    newTicket = await AdaptiveCardHelper.AskAnExpertSubmitText(message, turnContext, cancellationToken, this.ticketsProvider).ConfigureAwait(false);
                    if (newTicket != null)
                    {
                        smeTeamCard = new SmeTicketCard(newTicket).ToAttachment(message?.LocalTimestamp);
                        userCard = new UserNotificationCard(newTicket).ToAttachment(Strings.NotificationCardContent, message?.LocalTimestamp);
                    }

                    break;

                case ShareFeedbackCard.ShareFeedbackSubmitText:
                    this.logger.LogInformation("Received app feedback");
                    smeTeamCard = await AdaptiveCardHelper.ShareFeedbackSubmitText(message, turnContext, cancellationToken).ConfigureAwait(false);
                    if (smeTeamCard != null)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(Strings.ThankYouTextContent)).ConfigureAwait(false);
                    }

                    break;

                default:
                    var payload = ((JObject)message.Value).ToObject<ResponseCardPayload>();

                    if (payload.IsPrompt)
                    {
                        this.logger.LogInformation("Sending input to QnAMaker for prompt");
                        await this.GetQuestionAnswerReplyAsync(turnContext, message).ConfigureAwait(false);
                    }
                    else
                    {
                        this.logger.LogWarning($"Unexpected text in submit payload: {message.Text}");
                    }

                    break;
            }

            string expertTeamId = await this.configurationProvider.GetSavedEntityDetailAsync(ConfigurationEntityTypes.TeamId).ConfigureAwait(false);

            // Send message to SME team.
            if (smeTeamCard != null)
            {
                var resourceResponse = await this.SendCardToTeamAsync(turnContext, smeTeamCard, expertTeamId, cancellationToken).ConfigureAwait(false);

                // If a ticket was created, update the ticket with the conversation info.
                if (newTicket != null)
                {
                    newTicket.SmeCardActivityId = resourceResponse.ActivityId;
                    newTicket.SmeThreadConversationId = resourceResponse.Id;
                    await this.ticketsProvider.UpsertTicketAsync(newTicket).ConfigureAwait(false);
                }
            }

            // Send acknowledgment to the user
            if (userCard != null)
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(userCard), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Send typing indicator to the user.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task SendTypingIndicatorAsync(ITurnContext turnContext)
        {
            try
            {
                var typingActivity = turnContext.Activity.CreateReply();
                typingActivity.Type = ActivityTypes.Typing;
                await turnContext.SendActivityAsync(typingActivity);
            }
            catch (Exception ex)
            {
                // Do not fail on errors sending the typing indicator
                this.logger.LogWarning(ex, "Failed to send a typing indicator");
            }
        }

        /// <summary>
        /// Send the given attachment to the specified team.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cardToSend">The card to send.</param>
        /// <param name="teamId">Team id to which the message is being sent.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns><see cref="Task"/>That resolves to a <see cref="ConversationResourceResponse"/>Send a attachemnt.</returns>
        private async Task<ConversationResourceResponse> SendCardToTeamAsync(
            ITurnContext turnContext,
            Attachment cardToSend,
            string teamId,
            CancellationToken cancellationToken)
        {
            var conversationParameters = new ConversationParameters
            {
                Activity = (Activity)MessageFactory.Attachment(cardToSend),
                ChannelData = new TeamsChannelData { Channel = new ChannelInfo(teamId) },
            };

            var taskCompletionSource = new TaskCompletionSource<ConversationResourceResponse>();
            await ((BotFrameworkAdapter)turnContext.Adapter).CreateConversationAsync(
                null,       // If we set channel = "msteams", there is an error as preinstalled middleware expects ChannelData to be present.
                turnContext.Activity.ServiceUrl,
                this.microsoftAppCredentials,
                conversationParameters,
                (newTurnContext, newCancellationToken) =>
                {
                    var activity = newTurnContext.Activity;
                    taskCompletionSource.SetResult(new ConversationResourceResponse
                    {
                        Id = activity.Conversation.Id,
                        ActivityId = activity.Id,
                        ServiceUrl = activity.ServiceUrl,
                    });
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);

            return await taskCompletionSource.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Get the reply to a question asked by end user.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="message">Text message.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task GetQuestionAnswerReplyAsync(
            ITurnContext<IMessageActivity> turnContext,
            IMessageActivity message)
        {
            string text = message.Text?.ToLower()?.Trim() ?? string.Empty;

            try
            {
                var queryResult = new QnASearchResultList();

                ResponseCardPayload payload = new ResponseCardPayload();

                if (!string.IsNullOrEmpty(message.ReplyToId) && (message.Value != null))
                {
                    payload = ((JObject)message.Value).ToObject<ResponseCardPayload>();
                }

                queryResult = await this.qnaServiceProvider.GenerateAnswerAsync(question: text, isTestKnowledgeBase: false, payload.PreviousQuestions?.First().Id.ToString(), payload.PreviousQuestions?.First().Questions.First()).ConfigureAwait(false);

                if (queryResult.Answers.First().Id != -1)
                {
                    var answerData = queryResult.Answers.First();
                    AnswerModel answerModel = new AnswerModel();

                    if (Validators.IsValidJSON(answerData.Answer))
                    {
                        answerModel = JsonConvert.DeserializeObject<AnswerModel>(answerData.Answer);
                    }

                    if (!string.IsNullOrEmpty(answerModel?.Title) || !string.IsNullOrEmpty(answerModel?.Subtitle) || !string.IsNullOrEmpty(answerModel?.ImageUrl) || !string.IsNullOrEmpty(answerModel?.RedirectionUrl))
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Attachment(MessagingExtensionQnaCard.GetEndUserRichCard(text, answerData))).ConfigureAwait(false);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Attachment(ResponseCard.GetCard(answerData, text, this.appBaseUri, payload))).ConfigureAwait(false);
                    }
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(UnrecognizedInputCard.GetCard(text))).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Check if knowledge base is empty and has not published yet when end user is asking a question to bot.
                if (((ErrorResponseException)ex).Response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var knowledgeBaseId = await this.configurationProvider.GetSavedEntityDetailAsync(Constants.KnowledgeBaseEntityId).ConfigureAwait(false);
                    var hasPublished = await this.qnaServiceProvider.GetInitialPublishedStatusAsync(knowledgeBaseId).ConfigureAwait(false);

                    // Check if knowledge base has not published yet.
                    if (!hasPublished)
                    {
                        this.logger.LogError(ex, "Error while fetching the qna pair: knowledge base may be empty or it has not published yet.");
                        await turnContext.SendActivityAsync(MessageFactory.Attachment(UnrecognizedInputCard.GetCard(text))).ConfigureAwait(false);
                        return;
                    }
                }

                // Throw the error at calling place, if there is any generic exception which is not caught.
                throw;
            }
        }
    }
}