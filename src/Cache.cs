using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Data.HashFunction.FNV;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CCache.Test")]
[assembly: InternalsVisibleTo("CCache.Bench")]

namespace CCache
{
    public class Cache
    {
        private readonly Configuration _config = new Configuration();
        private LinkedList<Item> _list = new LinkedList<Item>();
        private Channel<Item> _deleteChannel;
        private Channel<Item> _promoteChannel;
        private Channel<bool> _doneChannel;
        private Int64 _size;
        private Bucket[] _buckets;
        private UInt32 _bucketMask;
        private int _dropped;
        private IFNV1a _fnv = FNV1aFactory.Instance.Create();
        public Cache(Configuration config)
        {
            _config = config;
            _buckets = new Bucket[config.Buckets];
            _bucketMask = (UInt32)config.Buckets - 1;
            for (int i = 0; i < config.Buckets; i++)
            {
                _buckets[i] = new Bucket();
            }

            Restart();
        }
        public Cache() : this(new Configuration()) { }

        public int ItemCount
        {
            get => _buckets.Aggregate(0, (acc, bucket) => acc + bucket.Count);
        }

        public async Task<Item> Fetch<T>(string key, TimeSpan duration, Func<T> fetchFn)
        {
            var item = Bucket(key).GetOrDefault(key);
            if (item != null && !item.Expired)
            {
                return item;
            }

            var value = fetchFn();
            var newItem = await DoSet(key, value, duration);
            return newItem;
        }

        /// <summary>GetOrDefault returns null if key is not found.
        /// This can return an expired item. Use item.Expired to see if the item
        /// is expired and item.TTL to see how long until the item expires (which
        /// will be negative for an already expired item).</summary>
        public async Task<Item> GetOrDefault(string key)
        {
            var item = Bucket(key).GetOrDefault(key);
            if (item == null)
            {
                return null;
            }

            if (!item.Expired && !_promoteChannel.Writer.TryWrite(item))
            {
                await _promoteChannel.Writer.WriteAsync(item);
            }

            return item;
        }

        /// <summary>Get throws KeyNotFoundException if key is not found
        /// This can return an expired item. Use item.Expired to see if the item
        /// is expired and item.TTL to see how long until the item expires (which
        /// will be negative for an already expired item).</summary>
        public async Task<Item> Get(string key)
        {
            var item = Bucket(key).Get(key);
            if (!item.Expired && !_promoteChannel.Writer.TryWrite(item))
            {
                await _promoteChannel.Writer.WriteAsync(item);
            }

            return item;
        }

        public async Task<Item> TrackingGet(string key)
        {
            var item = Bucket(key).GetOrDefault(key);
            if (item == null)
            {
                return null;
            }

            if (!item.Expired && !_promoteChannel.Writer.TryWrite(item))
            {
                await _promoteChannel.Writer.WriteAsync(item);
            }

            item.Track();
            return item;
        }

        public async Task<bool> Replace(string key, object value)
        {
            var item = Bucket(key).GetOrDefault(key);
            if (item == null)
            {
                return false;
            }

            await Set(key, value, item.TTL);
            return true;
        }

        public Task Set<T>(string key, T value, TimeSpan duration) => DoSet(key, value, duration);

        public async Task<bool> Delete(string key)
        {
            var item = Bucket(key).Delete(key);
            if (item == null)
            {
                return false;
            }

            if (!_deleteChannel.Writer.TryWrite(item))
            {
                await _deleteChannel.Writer.WriteAsync(item);
            }

            return true;
        }

        public async Task<int> DeletePrefix(string prefix)
        {
            var count = 0;
            foreach (var bucket in _buckets)
            {
                count += await bucket.DeletePrefix(prefix, _deleteChannel.Writer);
            }

            return count;
        }

        public int Dropped
        {
            get
            {
                var x = _dropped; // read and writes to 32-bit value types are atomic
                Interlocked.Exchange(ref _dropped, 0); // Reset dropped count
                return x;
            }
        }

        public void Clear()
        {
            foreach (var bucket in _buckets)
            {
                bucket.Clear();
            }

            _size = 0;
            _list.Clear();
        }

        public async Task Stop()
        {
            _promoteChannel.Writer.Complete();
            while (await _doneChannel.Reader.WaitToReadAsync())
            {
                while (_doneChannel.Reader.TryRead(out var done))
                {
                    if (done)
                    {
                        await _doneChannel.Reader.Completion;
                        await _deleteChannel.Reader.Completion;
                        await _promoteChannel.Reader.Completion;
                        return;
                    }
                }
            }
        }

        internal void Restart()
        {
            _deleteChannel = Channel.CreateBounded<Item>(_config.DeleteBuffer);
            _promoteChannel = Channel.CreateBounded<Item>(_config.DeleteBuffer);
            _doneChannel = Channel.CreateBounded<bool>(1);
            StartWorker();
        }

        internal Bucket Bucket(string key)
        {
            var hash = _fnv.ComputeHash(Encoding.ASCII.GetBytes(key));
            var sum = BitConverter.ToUInt32(hash.Hash, 0);
            return _buckets[sum & _bucketMask];
        }

        private async Task<Item> DoSet<T>(string key, T value, TimeSpan duration)
        {
            var (item, existing) = Bucket(key).Set(key, value, duration);
            if (existing != null && !_deleteChannel.Writer.TryWrite(existing))
            {
                await _deleteChannel.Writer.WriteAsync(existing);
            }

            if (!_promoteChannel.Writer.TryWrite(item))
            {
                await _promoteChannel.Writer.WriteAsync(item);
            }

            return item;
        }

        private void StartWorker()
        {
            Task.Run(PromoteWorker);
            Task.Run(DeleteWorker);
        }

        private async Task PromoteWorker()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        var promote = await _promoteChannel.Reader.ReadAsync();
                        if (DoPromote(promote) && _size > _config.MaxSize)
                        {
                            var dropped = GC();
                            Interlocked.Add(ref _dropped, dropped);
                        }
                    }
                    catch (ChannelClosedException)
                    {
                        _deleteChannel.Writer.Complete();
                        return;
                    }
                }
            }
            finally
            {
                await _doneChannel.Writer.WriteAsync(true);
                _doneChannel.Writer.Complete();
            }
        }

        private async Task DeleteWorker()
        {
            while (await _deleteChannel.Reader.WaitToReadAsync())
            {
                while (_deleteChannel.Reader.TryRead(out var item))
                {
                    DoDelete(item);
                }
            }
        }

        private void DoDelete(Item item)
        {
            if (item.Node != null)
            {
                item.Promotions = -2;
            }

            _size -= item.Size;
            if (_config.OnDelete != null)
            {
                _config.OnDelete(item);
            }
            _list.Remove(item);
        }

        private bool DoPromote(Item item)
        {
            if (item.Promotions == -2)
            {
                return false;
            }

            if (item.Node != null)
            {
                if (item.ShouldPromote(_config.GetsPerPromote))
                {
                    _list.MoveToFront(item.Node);
                    item.Promotions = 0;
                }

                return false;
            }

            _size += item.Size;
            item.Node = _list.AddFirst(item);
            return true;
        }

        internal int GC()
        {
            var dropped = 0;
            var last = _list.Last;
            for (int i = 0; i < _config.ItemsToPrune; i++)
            {
                if (last == null)
                {
                    return dropped;
                }

                var prev = last.Previous;
                var item = last.Value;
                if (_config.Tracking || item.RefCount == 0)
                {
                    Bucket(item.Key).Delete(item.Key);
                    _size -= item.Size;
                    _list.Remove(item.Node);
                    if (_config.OnDelete != null)
                    {
                        _config.OnDelete(item);
                    }

                    dropped++;
                    item.Promotions = -2;
                }

                last = prev;
            }

            return dropped;
        }
    }
}