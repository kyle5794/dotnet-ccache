using System;
using System.Threading;
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