// <copyright file="BotController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Integration.AspNet.Core;
    using Microsoft.Teams.Apps.FAQPlusPlus.Bots;

    // This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
    // implementation at runtime. Multiple different IBot implementations running at different endpoints can be
    // achieved by specifying a more specific type for the bot constructor argument.

    /// <summary>
    /// This is a Bot controller class includes all API's related to this Bot.
    /// </summary>
    [Route("api/messages")]
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter adapter;
        private readonly IBot userBot;
        private readonly IBot smeBot;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotController"/> class.
        /// </summary>
        /// <param name="adapter">Bot adapter.</param>
        /// <param name="userBot"> User Bot Interface.</param>
        /// <param name="smeBot"> SME Bot Interface.</param>
        public BotController(IBotFrameworkHttpAdapter adapter, UserActivityHandler userBot, SmeActivityHandler smeBot)
        {
            this.adapter = adapter;
            this.userBot = userBot;
            this.smeBot = smeBot;
        }

        /// <summary>
        /// Executing the Post Async method.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Route("user")]
        [HttpPost]
        public async Task PostUserAsync()
        {
            await this.adapter.ProcessAsync(this.Request, this.Response, this.userBot).ConfigureAwait(false);
        }

        /// <summary>
        /// Executing the Post Async method.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Route("sme")]
        [HttpPost]
        public async Task PostSmeAsync()
        {
            await this.adapter.ProcessAsync(this.Request, this.Response, this.smeBot).ConfigureAwait(false);
        }
    }
}
