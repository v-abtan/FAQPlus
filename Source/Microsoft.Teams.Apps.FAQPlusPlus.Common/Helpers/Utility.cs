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
            var invalidParameterValueMessage = "Invalid value for parameter id.";
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id), invalidParameterValueMessage);
            }

            const string idPrefix = "28:";
            var recipientIdParts = id.Split(new[] { idPrefix }, 2, StringSplitOptions.None);
            if (recipientIdParts.Length != 2)
            {
                throw new ArgumentNullException(nameof(id), invalidParameterValueMessage);
            }

            return recipientIdParts[1];
        }
    }
}
