// <copyright file="CommonTeamsActivityHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Bots
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Teams;
    using Microsoft.Bot.Connector.Authentication;
    using Microsoft.Bot.Schema;
    using Microsoft.Extensions.Logging;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Providers;

    /// <summary>
    /// Class that shares common functions for multiple bots.
    /// </summary>
    public class CommonTeamsActivityHandler : TeamsActivityHandler
    {
        /// <summary>
        /// Represents a set of key/value application configuration properties for FaqPlusPlus bot.
        /// </summary>
        protected readonly BotSettings options;

        /// <summary>
        /// logger instance of specific ActivityHandler type.
        /// </summary>
        protected readonly ILogger logger;

        /// <summary>
        /// Configuration Provider.
        /// </summary>
        protected readonly IConfigurationDataProvider configurationProvider;

        /// <summary>
        /// Microsoft app credentials to use.
        /// </summary>
        protected readonly MicrosoftAppCredentials microsoftAppCredentials;

        /// <summary>
        /// Tickets Provider.
        /// </summary>
        protected readonly ITicketsProvider ticketsProvider;

        /// <summary>
        /// Base URL for app.
        /// </summary>
        protected readonly string appBaseUri;

        /// <summary>
        /// Question and answer maker service provider.
        /// </summary>
        protected readonly IQnaServiceProvider qnaServiceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommonTeamsActivityHandler"/> class.
        /// </summary>
        /// <param name="configurationProvider">Configuration Provider.</param>
        /// <param name="microsoftAppCredentials">Microsoft app credentials to use.</param>
        /// <param name="ticketsProvider">Tickets Provider.</param>
        /// <param name="qnaServiceProvider">Question and answer maker service provider.</param>
        /// <param name="options">A set of key/value application configuration properties for FaqPlusPlus bot.</param>
        /// <param name="logger">Instance to send logs to the Application Insights service.</param>
        public CommonTeamsActivityHandler(
            IConfigurationDataProvider configurationProvider,
            MicrosoftAppCredentials microsoftAppCredentials,
            ITicketsProvider ticketsProvider,
            IQnaServiceProvider qnaServiceProvider,
            BotSettings options,
            ILogger logger)
        {
            this.logger = logger;
            this.options = options;
            this.configurationProvider = configurationProvider;
            this.microsoftAppCredentials = microsoftAppCredentials;
            this.ticketsProvider = ticketsProvider;
            this.qnaServiceProvider = qnaServiceProvider;
            this.appBaseUri = this.options.AppBaseUri;
        }

        /// <summary>
        /// Handles an incoming activity.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// Reference link: https://docs.microsoft.com/en-us/dotnet/api/microsoft.bot.builder.activityhandler.onturnasync?view=botbuilder-dotnet-stable.
        /// </remarks>
        public override Task OnTurnAsync(
            ITurnContext turnContext,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (turnContext != null & !this.IsActivityFromExpectedTenant(turnContext))
                {
                    this.logger.LogWarning($"Unexpected tenant id {turnContext?.Activity.Conversation.TenantId}");
                    return Task.CompletedTask;
                }

                // Get the current culture info to use in resource files
                string locale = turnContext?.Activity.Entities?.FirstOrDefault(entity => entity.Type == "clientInfo")?.Properties["locale"]?.ToString();

                if (!string.IsNullOrEmpty(locale))
                {
                    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
                }

                return base.OnTurnAsync(turnContext, cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error at OnTurnAsync()");
                return base.OnTurnAsync(turnContext, cancellationToken);
            }
        }

        /// <summary>
        /// Verify if the tenant Id in the message is the same tenant Id used when application was configured.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <returns>Boolean value where true represent tenant is valid while false represent tenant in not valid.</returns>
        private bool IsActivityFromExpectedTenant(ITurnContext turnContext)
        {
            return turnContext.Activity.Conversation.TenantId == this.options.TenantId;
        }

        /// <summary>
        /// Send typing indicator to the user.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected async Task SendTypingIndicatorAsync(ITurnContext turnContext)
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
    }
}