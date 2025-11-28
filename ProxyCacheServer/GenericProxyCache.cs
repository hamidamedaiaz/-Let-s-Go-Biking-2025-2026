using System;
using System.Runtime.Caching;

namespace ProxyCacheService
{
    /// <summary>
    /// Generic caching service for proxy objects.
    /// Provides in-memory caching with configurable expiration policies.
    /// </summary>
    /// <typeparam name="T">Type of objects to cache</typeparam>
    public class GenericProxyCache<T> where T : class, new()
    {
        private readonly MemoryCache _cache;
        public DateTimeOffset dt_default { get; set; }

        public GenericProxyCache()
        {
            _cache = MemoryCache.Default;
            dt_default = ObjectCache.InfiniteAbsoluteExpiration;
            Console.WriteLine($"[GenericProxyCache] Cache initialized for type: {typeof(T).Name}");
        }

        /// <summary>
        /// Retrieves an item from cache or creates a new one with infinite expiration.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <returns>Cached or newly created object</returns>
        public T Get(string cacheItemName)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Cache item name cannot be empty", nameof(cacheItemName));

            var cachedItem = _cache.Get(cacheItemName) as T;

            if (cachedItem == null)
            {
                var newObj = new T();
                CacheItemPolicy policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = dt_default
                };
                _cache.Set(cacheItemName, newObj, policy);
                return newObj;
            }
            else
            {
                Console.WriteLine($"[GenericProxyCache] Cache hit for '{cacheItemName}'");
            }
            return cachedItem;
        }

        /// <summary>
        /// Retrieves an item from cache or creates a new one with specified expiration in seconds.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <param name="dt_seconds">Expiration time in seconds</param>
        /// <returns>Cached or newly created object</returns>
        public T Get(string cacheItemName, double dt_seconds)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Cache item name cannot be empty", nameof(cacheItemName));

            if (dt_seconds <= 0)
                throw new ArgumentException("Expiration time must be positive", nameof(dt_seconds));

            var cachedItem = _cache.Get(cacheItemName) as T;

            if (cachedItem == null)
            {
                var newObj = new T();
                CacheItemPolicy policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(dt_seconds)
                };
                _cache.Set(cacheItemName, newObj, policy);
                return newObj;
            }
            else
            {
                Console.WriteLine($"[GenericProxyCache] Cache hit for '{cacheItemName}'");
            }
            return cachedItem;
        }

        /// <summary>
        /// Retrieves an item from cache or creates a new one with specified absolute expiration.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <param name="dt">Absolute expiration date/time</param>
        /// <returns>Cached or newly created object</returns>
        public T Get(string cacheItemName, DateTimeOffset dt)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Cache item name cannot be empty", nameof(cacheItemName));

            if (dt <= DateTimeOffset.UtcNow)
                throw new ArgumentException("Expiration date must be in the future", nameof(dt));

            var cachedItem = _cache.Get(cacheItemName) as T;

            if (cachedItem == null)
            {
                var newObj = new T();
                CacheItemPolicy policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = dt
                };
                _cache.Set(cacheItemName, newObj, policy);
                return newObj;
            }
            else
            {
                Console.WriteLine($"[GenericProxyCache] Cache hit for '{cacheItemName}' (expiration: {dt})");
            }

            return cachedItem;
        }

        /// <summary>
        /// Stores an item in cache with infinite expiration.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <param name="value">Value to cache</param>
        public void Set(string cacheItemName, T value)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Cache item name cannot be empty", nameof(cacheItemName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = dt_default
            };
            _cache.Set(cacheItemName, value, policy);
            Console.WriteLine($"[GenericProxyCache] Item '{cacheItemName}' stored in cache");
        }

        /// <summary>
        /// Stores an item in cache with specified expiration in seconds.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="dt_seconds">Expiration time in seconds</param>
        public void Set(string cacheItemName, T value, double dt_seconds)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Cache item name cannot be empty", nameof(cacheItemName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (dt_seconds <= 0)
                throw new ArgumentException("Expiration time must be positive", nameof(dt_seconds));

            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(dt_seconds)
            };
            _cache.Set(cacheItemName, value, policy);
            Console.WriteLine($"[GenericProxyCache] Item '{cacheItemName}' stored with {dt_seconds}s expiration");
        }

        /// <summary>
        /// Stores an item in cache with specified absolute expiration.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="dt">Absolute expiration date/time</param>
        public void Set(string cacheItemName, T value, DateTimeOffset dt)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Cache item name cannot be empty", nameof(cacheItemName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (dt <= DateTimeOffset.UtcNow)
                throw new ArgumentException("Expiration date must be in the future", nameof(dt));

            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = dt
            };
            _cache.Set(cacheItemName, value, policy);
            Console.WriteLine($"[GenericProxyCache] Item '{cacheItemName}' stored (expiration: {dt})");
        }

        /// <summary>
        /// Retrieves an item from cache or creates it using the factory function.
        /// </summary>
        /// <param name="cacheItemName">Unique cache key</param>
        /// <param name="dt_seconds">Expiration time in seconds</param>
        /// <param name="factory">Factory function to create the object if not in cache</param>
        /// <returns>Cached or newly created object</returns>
        public T GetOrAdd(string cacheItemName, double dt_seconds, Func<T> factory)
        {
            var cachedItem = _cache.Get(cacheItemName) as T;
            if (cachedItem == null)
            {
                var newObj = factory();
                CacheItemPolicy policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(dt_seconds)
                };
                _cache.Set(cacheItemName, newObj, policy);
                return newObj;
            }
            return cachedItem;
        }
    }
}
