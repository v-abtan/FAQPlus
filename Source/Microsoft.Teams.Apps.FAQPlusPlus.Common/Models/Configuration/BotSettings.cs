// <copyright file="BotSettings.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration
{
    /// <summary>
    /// Provides app settings related to FaqPlusPlus bot.
    /// </summary>
    public class BotSettings
    {
        /// <summary>
        /// Gets or sets access cache expiry in days.
        /// </summary>
        public int AccessCacheExpiryInDays { get; set; }

        /// <summary>
        /// Gets or sets application base uri.
        /// </summary>
        public string AppBaseUri { get; set; }

        /// <summary>
        /// Gets or sets sme app id.
        /// </summary>
        public string SmeAppId { get; set; }

        /// <summary>
        /// Gets or sets user app id.
        /// </summary>
        public string UserAppId { get; set; }

        /// <summary>
        /// Gets or sets access tenant id string.
        /// </summary>
        public string TenantId { get; set; }
    }
}