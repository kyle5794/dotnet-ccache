using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CCache.Test")]

namespace CCache
{
    internal interface ITrackedItem
    {
        void Release();
        bool Expired { get; }
        TimeSpan TTL { get; }
        DateTime Expires { get; }
        void Extend(TimeSpan duration);
    }

    public class Item : ITrackedItem
    {
        internal string Key { get; }
        internal Int32 Promotions { get; set; }
        internal Int32 RefCount;
        private Int64 _expires; // Ticks
        internal Int64 Size { get; set; } = 1;
        internal object Value { get; }
        internal LinkedListNode<Item> Node { get; set; }

        public Item(string key, object value, Int64 expires)
        {
            Key = key;
            Value = value;
            Promotions = 0;
            _expires = expires;
            Size = 1;
        }

        internal bool ShouldPromote(Int32 getsPerPromote)
        {
            Promotions++;
            return Promotions == getsPerPromote;
        }

        internal void Track() => Interlocked.Increment(ref RefCount);
        public void Release() => Interlocked.Decrement(ref RefCount);

        public bool Expired
        {
            get => Interlocked.Read(ref _expires) < DateTimeHelper.UnixTickNow;
        }

        public DateTime Expires
        {
            get => DateTimeHelper.Unix0.AddTicks(Interlocked.Read(ref _expires));
        }

        public TimeSpan TTL
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref _expires) - DateTimeHelper.UnixTickNow);
        }

        public void Extend(TimeSpan duration) => Interlocked.Exchange(ref _expires, DateTimeHelper.UnixTickNow + duration.Ticks);
    }

}
