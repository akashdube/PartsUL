using System;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace PartsUnlimited.Cache
{
    public class TransientRedisCacheWrapper : IPartsUnlimitedCache
    {
        private readonly PartsUnlimitedRedisCache _cache;
        private readonly RetryPolicy<RedisTransientErrorDetectionStrategy> _retryPolicy;

        public TransientRedisCacheWrapper(PartsUnlimitedRedisCache cache)
        {
            _cache = cache;
            var retryStrategy = new FixedInterval(3, TimeSpan.FromSeconds(1));
            _retryPolicy = new RetryPolicy<RedisTransientErrorDetectionStrategy>(retryStrategy);
        }

        public Task Set<T>(string key, T value, PartsUnlimitedMemoryCacheEntryOptions options)
        {
            return _retryPolicy.ExecuteAction(() => _cache.Set(key, value, options));
        }

        public Task<CacheResult<T>> TryGetValue<T>(string key)
        {
            return _retryPolicy.ExecuteAction(() => _cache.TryGetValue<T>(key));
        }

        public Task Remove(string key)
        {
            return _retryPolicy.ExecuteAction(() => _cache.Remove(key));
        }
    }
}