// <copyright file="Utility.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Common.Helpers
{
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
            var res = string.Empty;
            try
            {
                res = id.Split(':')[1];
            }
            catch
            {
                throw;
            }

            return res;
        }
    }
}
