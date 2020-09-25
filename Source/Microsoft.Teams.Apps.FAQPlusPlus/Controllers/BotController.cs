// <copyright file="BotController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Helpers;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Teams.Apps.FAQPlusPlus.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Integration.AspNet.Core;

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
        private readonly BotSettings settings;
        private readonly IBotFrameworkHttpAdapter adapter;
        private readonly IBot userBot;
        private readonly IBot smeBot;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotController"/> class.
        /// </summary>
        /// <param name="settings">Application bot settings bag.</param>
        /// <param name="adapter">Bot adapter.</param>
        /// <param name="userBot"> User Bot Interface.</param>
        /// <param name="smeBot"> SME Bot Interface.</param>
        public BotController(BotSettings settings, IBotFrameworkHttpAdapter adapter, IBot userBot, IBot smeBot)
        {
            this.settings = settings;
            this.adapter = adapter;
            this.userBot = userBot;
            this.smeBot = smeBot;
        }

        /// <summary>
        /// Executing the Post Async method.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [HttpPost]
        public async Task PostAsync()
        {
            this.Request.EnableBuffering();
            using var reader = new StreamReader(this.Request.Body, Encoding.UTF8, false, 1000, true);

            // TODO Do we need to handle malformed HTTP payload? or exceptions will be logged in telemetry anyway
            var body = JsonConvert.DeserializeObject<JObject>(await reader.ReadToEndAsync());

            // Reset the request body stream position so next middleware (activity handlers) can read it
            this.Request.Body.Position = 0;

            // Fetch recipient id from body
            var botId = ((JValue)body.SelectToken("recipient.id")).Value;
            if (Utility.GetSanitizedId(botId.ToString()) == this.settings.SmeAppId)
            {
                await this.adapter.ProcessAsync(this.Request, this.Response, this.smeBot).ConfigureAwait(false);
            }
            else
            {
                await this.adapter.ProcessAsync(this.Request, this.Response, this.userBot).ConfigureAwait(false);
            }
        }
    }
}
