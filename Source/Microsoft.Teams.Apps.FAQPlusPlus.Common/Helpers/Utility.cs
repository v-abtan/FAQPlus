// <copyright file="Utility.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Common.Helpers
{
    using System;

    /// <summary>
    /// Helper class for bot operations.
    /// </summary>
    public class Utility
    {
        /// <summary>
        /// Extract bot Id from turn recipient id.
        /// </summary>
        /// <param name="id">recipient id.</param>
        /// <returns>bot id.</returns>
        public static string GetSanitizedId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id), "Invalid value for parameter id.");
            }

            string recipientId;
            try
            {
                var recipientIdParts = id.Split(':');
                recipientId = recipientIdParts.Length > 0 ? recipientIdParts[recipientIdParts.Length - 1] : null;
            }
            catch
            {
                throw;
            }

            return recipientId;
        }
    }
}
