using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CCache.Test")]

namespace CCache
{
    internal class Bucket
    {
        private static ReaderWriterLock _rwl = new ReaderWriterLock();
        private const int _timeOut = 10000;
        private Dictionary<string, Item> _lookup = new Dictionary<string, Item>();

        internal int Count
        {
            get
            {
                try
                {
                    _rwl.AcquireReaderLock(_timeOut);
                    return _lookup.Count;
                }
                finally
                {
                    _rwl.ReleaseReaderLock();
                }
            }
        }

        internal Item Get(string key)
        {
            try
            {
                _rwl.AcquireReaderLock(_timeOut);
                return _lookup[key];
            }
            finally
            {
                _rwl.ReleaseReaderLock();
            }
        }

        internal Item GetOrDefault(string key)
        {
            try
            {
                _rwl.AcquireReaderLock(_timeOut);
                if (_lookup.ContainsKey(key))
                {
                    return _lookup[key];
                }

                return null;
            }
            finally
            {
                _rwl.ReleaseReaderLock();
            }
        }

        internal (Item, Item) Set<T>(string key, T value, TimeSpan duration)
        {
            var expires = DateTimeHelper.UnixTickNow + duration.Ticks;
            var item = new Item(key, value, expires);
            try
            {
                Item existing = null;
                _rwl.AcquireWriterLock(_timeOut);
                if (_lookup.ContainsKey(key))
                {
                    existing = _lookup[key];
                    _lookup.Remove(key);
                }

                _lookup.Add(key, item);

                return (item, existing);
            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }

        internal Item Delete(string key)
        {
            try
            {
                _rwl.AcquireWriterLock(_timeOut);
                Item old = null;
                if (_lookup.ContainsKey(key))
                {
                    old = _lookup[key];
                }

                _lookup.Remove(key);

                return old;
            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }

        internal async Task<bool> DeleteAll(ChannelWriter<Item> deleteChannel)
        {
            try
            {
                _rwl.AcquireWriterLock(_timeOut);
                if (!_lookup.Any())
                {
                    return false;
                }

                foreach (var item in _lookup)
                {
                    _lookup.Remove(item.Key);
                    if (!deleteChannel.TryWrite(item.Value))
                    {
                        await deleteChannel.WriteAsync(item.Value);
                    }
                }

                return true;
            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }

        // Original note from Golang code by @karlseguin:
        // This is an expensive operation, so we do what we can to optimize it and limit
        // the impact it has on concurrent operations. Specifically, we:
        // 1 - Do an initial iteration to collect matches. This allows us to do the
        //     "expensive" prefix check (on all values) using only a read-lock
        // 2 - Do a second iteration, under write lock, for the matched results to do
        //     the actual deletion
        // Also, this is the only place where the Bucket is aware of cache detail: the
        // deletables channel. Passing it here lets us avoid iterating over matched items
        // again in the cache. Further, we pass item to deletables BEFORE actually removing
        // the item from the map. I'm pretty sure this is 100% fine, but it is unique.
        // (We do this so that the write to the channel is under the read lock and not the
        // write lock)
        internal async Task<int> DeletePrefix(string prefix, ChannelWriter<Item> deleteChannel)
        {
            var keys = new List<String>(_lookup.Count / 10);

            _rwl.AcquireReaderLock(_timeOut);
            foreach (var item in _lookup)
            {
                if (item.Key.StartsWith(prefix))
                {
                    if (!deleteChannel.TryWrite(item.Value))
                    {
                        await deleteChannel.WriteAsync(item.Value);
                        keys.Add(item.Key);
                    }
                }
            }
            _rwl.ReleaseReaderLock();

            if (!keys.Any())
            {
                return 0;
            }

            _rwl.AcquireWriterLock(_timeOut);

            keys.Select(k => _lookup.Remove(k));
            _rwl.ReleaseWriterLock();

            return keys.Count;
        }

        internal void Clear()
        {
            try
            {
                _rwl.AcquireWriterLock(_timeOut);
                _lookup = new Dictionary<string, Item>();
            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }
    }
}