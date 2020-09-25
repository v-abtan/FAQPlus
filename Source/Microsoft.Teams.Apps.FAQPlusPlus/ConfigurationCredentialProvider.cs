// <copyright file="ConfigurationCredentialProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Connector.Authentication;

    /// <summary>
    /// Implementation of <see cref="ICredentialProvider"/> that supports multiple bots communications.
    /// </summary>
    public class ConfigurationCredentialProvider : ICredentialProvider
    {
        private readonly Dictionary<string, string> credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCredentialProvider"/> class.
        /// A constructor that accepts a map of bot id list and credentials.
        /// </summary>
        /// <param name="credentials">A map of bot id list and credentials.</param>
        public ConfigurationCredentialProvider(Dictionary<string, string> credentials)
        {
            this.credentials = credentials;
        }

        /// <summary>
        /// Validates an app ID.
        /// </summary>
        /// <param name="appId">The app ID to validate.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result is true if <paramref name="appId"/>
        /// is valid for the controller; otherwise, false.</remarks>
        public Task<bool> IsValidAppIdAsync(string appId)
        {
            return Task.FromResult(this.credentials.ContainsKey(appId));
        }

        /// <summary>
        /// Gets the app password for a given bot app ID.
        /// </summary>
        /// <param name="appId">The ID of the app to get the password for.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful and the app ID is valid, the result
        /// contains the password; otherwise, null.
        /// </remarks>
        public Task<string> GetAppPasswordAsync(string appId)
        {
            return Task.FromResult(this.credentials.ContainsKey(appId) ? this.credentials[appId] : null);
        }

        /// <summary>
        /// Checks whether bot authentication is disabled.
        /// </summary>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful and bot authentication is disabled, the result
        /// is true; otherwise, false.
        /// </remarks>
        public Task<bool> IsAuthenticationDisabledAsync()
        {
            return Task.FromResult(!this.credentials.Any());
        }
    }

}