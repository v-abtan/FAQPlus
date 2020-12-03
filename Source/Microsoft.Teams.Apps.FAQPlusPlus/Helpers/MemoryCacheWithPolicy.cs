// <copyright file="MemoryCacheWithPolicy.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Threading.Tasks;

namespace Microsoft.Teams.Apps.FAQPlusPlus.Helpers
{
    using System;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Cache data in memory with timeout eviction policy.
    /// </summary>
    /// <typeparam name="T">Item data type</typeparam>
    public class MemoryCacheWithPolicy<T>
    {
        private readonly MemoryCache cache = new MemoryCache(
            new MemoryCacheOptions());

        /// <summary>
        /// Get item from cache or invoke a delegate to get ite from source.
        /// </summary>
        /// <param name="key">Item name</param>
        /// <param name="createItem">Delegate to get item if missing from cache.</param>
        /// <returns>Item value.</returns>
        public async Task<T> GetOrCreate(object key, Func<Task<T>> createItem)
        {
            T cacheEntry;
            if (!this.cache.TryGetValue(key, out cacheEntry))
            {
                // Key not in cache, so get data.
                cacheEntry = await createItem();

                var cacheEntryOptions = new MemoryCacheEntryOptions()

                    // Remove from cache after this time, regardless of sliding expiration
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                // Save data in cache.
                this.cache.Set(key, cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;
        }
    }
}
