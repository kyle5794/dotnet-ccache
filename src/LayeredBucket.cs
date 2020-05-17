using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CCache.Test")]

namespace CCache
{
    public class LayeredBucket
    {
        private static ReaderWriterLock _rwl = new ReaderWriterLock();
        private const int _timeOut = 10000;
        private Dictionary<string, Bucket> _buckets = new Dictionary<string, Bucket>();

        internal int Count
        {
            get
            {
                try
                {
                    _rwl.AcquireReaderLock(_timeOut);
                    return _buckets.Aggregate(0, (acc, bucket) => acc + bucket.Value.Count);
                }
                finally
                {
                    _rwl.ReleaseReaderLock();
                }
            }
        }

        internal Item Get(string primarKey, string secondaryKey) => GetSecondaryBucket(primarKey).Get(secondaryKey);

        internal Item GetOrDefault(string primarKey, string secondaryKey)
        {
            try
            {
                return Get(primarKey, secondaryKey);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        internal Bucket GetSecondaryBucket(string primarKey)
        {
            try
            {
                _rwl.AcquireReaderLock(_timeOut);
                return _buckets[primarKey];
            }
            finally
            {
                _rwl.ReleaseReaderLock();
            }
        }


        internal (Item, Item) Set<T>(string primaryKey, string secondaryKey, T value, TimeSpan duration)
        {
            var expires = DateTimeHelper.UnixTickNow + duration.Ticks;
            var bucket = new Bucket();
            _rwl.AcquireWriterLock(_timeOut);
            if (!_buckets.ContainsKey(primaryKey))
            {
                _buckets.Add(primaryKey, new Bucket());
            }
            else
            {
                bucket = _buckets[primaryKey];
            }
            _rwl.ReleaseWriterLock();
            var (item, existing) = bucket.Set(secondaryKey, value, duration);
            item.Group = primaryKey;
            return (item, existing);

        }

        internal Item Delete(string primaryKey, string secondaryKey)
        {

            Bucket b = null;
            _rwl.AcquireReaderLock(_timeOut);

            if (_buckets.ContainsKey(primaryKey))
            {
                b = _buckets[primaryKey];
            }
            _rwl.ReleaseReaderLock();

            if (b == null)
            {
                return null;
            }

            return b.Delete(secondaryKey);
        }

        internal async Task<bool> DeleteAll(string primaryKey, ChannelWriter<Item> deleteChannel)
        {

            Bucket b = null;
            _rwl.AcquireReaderLock(_timeOut);

            if (_buckets.ContainsKey(primaryKey))
            {
                b = _buckets[primaryKey];
            }
            _rwl.ReleaseReaderLock();

            if (b == null)
            {
                return false;
            }

            return await b.DeleteAll(deleteChannel);
        }


        internal void Clear()
        {
            try
            {
                _rwl.AcquireWriterLock(_timeOut);
                foreach (var item in _buckets)
                {
                    item.Value.Clear();
                }
                _buckets = new Dictionary<string, Bucket>();
            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }
    }
}