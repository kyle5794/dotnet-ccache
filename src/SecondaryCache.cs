using System;
using System.Threading.Tasks;

namespace CCache
{
    public class SecondaryCache
    {
        private Bucket _bucket;
        private LayeredCache _pCache;

        public SecondaryCache(LayeredCache pCache)
        {
            _bucket = new Bucket();
            _pCache = pCache;
        }

        public Item GetOrDefault(string secondaryKey) => _bucket.GetOrDefault(secondaryKey);
        public Item Get(string secondaryKey) => _bucket.Get(secondaryKey);

        public async Task<Item> Set<T>(string secondaryKey, T value, TimeSpan duration)
        {
            var (item, existing) = _bucket.Set(secondaryKey, value, duration);
            if (existing == null)
            {
                if (!_pCache.DeleteWriter.TryWrite(existing))
                {
                    await _pCache.DeleteWriter.WriteAsync(existing);
                }
            }

            if (!_pCache.PromoteWriter.TryWrite(item))
            {
                await _pCache.PromoteWriter.WriteAsync(item);
            }

            return item;
        }

        public async Task<Item> Fetch<T>(string secondaryKey, TimeSpan duration, Func<T> fetchFn)
        {
            var item = _bucket.GetOrDefault(secondaryKey);
            if (item != null)
            {
                return item;
            }

            return await Set(secondaryKey, fetchFn(), duration); ;
        }

        public async Task<bool> Delete(string secondaryKey)
        {
            var item = _bucket.Delete(secondaryKey);
            if (item != null)
            {
                if (!_pCache.DeleteWriter.TryWrite(item))
                {
                    await _pCache.DeleteWriter.WriteAsync(item);
                }

                return true;
            }

            return false;
        }

        public async Task<bool> Replace<T>(string secondaryKey, T value)
        {
            var item = _bucket.GetOrDefault(secondaryKey);
            if (item == null)
            {
                return false;
            }

            await Set(secondaryKey, value, item.TTL);
            return true;
        }

        public Item TrackingGet(string secondaryKey)
        {
            var item = _bucket.GetOrDefault(secondaryKey);
            if (item == null)
            {
                return null;
            }

            item.Track();
            return item;
        }
    }
}