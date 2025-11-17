using System;
using System.Runtime.Caching;

namespace ProxyCacheService
{
    public class GenericProxyCache<T> where T : class, new()
    {
        private readonly MemoryCache _cache;
        public DateTimeOffset dt_default { get; set; }
        public GenericProxyCache()
        {
            _cache = MemoryCache.Default;
            dt_default = ObjectCache.InfiniteAbsoluteExpiration;
            Console.WriteLine($"[INFO] GenericProxyCache<{typeof(T).Name}> initialisé");
        }
        public T Get(string cacheItemName)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Le nom de l'élément cache ne peut pas être vide", nameof(cacheItemName));

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
                Console.WriteLine($"[CACHE] Hit pour '{cacheItemName}'");
            }
            return cachedItem;
        }


        public T Get(string cacheItemName, double dt_seconds)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Le nom de l'élément cache ne peut pas être vide", nameof(cacheItemName));

            if (dt_seconds <= 0)
                throw new ArgumentException("La durée d'expiration doit être positive", nameof(dt_seconds));

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
                Console.WriteLine($"[CACHE] Hit pour '{cacheItemName}'");
            }
            return cachedItem;
        }


        public T Get(string cacheItemName, DateTimeOffset dt)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Le nom de l'élément cache ne peut pas être vide", nameof(cacheItemName));

            if (dt <= DateTimeOffset.UtcNow)
                throw new ArgumentException("La date d'expiration doit être dans le futur", nameof(dt));

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
                Console.WriteLine($"[CACHE] Hit pour '{cacheItemName}' avec expiration: {dt}");
            }

            return cachedItem;
        }

        public void Set(string cacheItemName, T value)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Le nom de l'élément cache ne peut pas être vide", nameof(cacheItemName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = dt_default
            };
            _cache.Set(cacheItemName, value, policy);
            Console.WriteLine($"[CACHE] Item '{cacheItemName}' stocké dans le cache");
        }

        public void Set(string cacheItemName, T value, double dt_seconds)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Le nom de l'élément cache ne peut pas être vide", nameof(cacheItemName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (dt_seconds <= 0)
                throw new ArgumentException("La durée d'expiration doit être positive", nameof(dt_seconds));

            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(dt_seconds)
            };
            _cache.Set(cacheItemName, value, policy);
            Console.WriteLine($"[CACHE] Item '{cacheItemName}' stocké dans le cache avec expiration de {dt_seconds} secondes");
        }

        public void Set(string cacheItemName, T value, DateTimeOffset dt)
        {
            if (string.IsNullOrWhiteSpace(cacheItemName))
                throw new ArgumentException("Le nom de l'élément cache ne peut pas être vide", nameof(cacheItemName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (dt <= DateTimeOffset.UtcNow)
                throw new ArgumentException("La date d'expiration doit être dans le futur", nameof(dt));

            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = dt
            };
            _cache.Set(cacheItemName, value, policy);
            Console.WriteLine($"[CACHE] Item '{cacheItemName}' stocké dans le cache avec expiration: {dt}");
        }
    }
}
