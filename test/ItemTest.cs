using System;
using Xunit;
using CCache;

namespace test
{
    public class ItemTest
    {
        [Theory]
        [InlineData(4, true)]
        [InlineData(5, false)]
        public void TestPromotability(int promote, bool shouldPromote)
        {
            var item = new Item("dummy", new object { }, 1000);
            item.Promotions = promote;
            Assert.Equal(shouldPromote, item.ShouldPromote(5));
        }


        [Fact]
        public void TestExpired()
        {
            var now = DateTimeHelper.UnixTickNow;
            var item1 = new Item("dummy", new object { }, now + 10000);
            var item2 = new Item("dummy", new object { }, now - 10000);

            Assert.False(item1.Expired);
            Assert.True(item2.Expired);
        }

        [Fact]
        public void TestTTL()
        {
            var now = DateTimeHelper.UnixTickNow;
            var item1 = new Item("dummy", new object { }, now + TimeSpan.FromSeconds(2).Ticks);
            var item2 = new Item("dummy", new object { }, now - TimeSpan.FromSeconds(2).Ticks);
            Assert.Equal(2, (int) Math.Ceiling(item1.TTL.TotalSeconds));
            Assert.Equal(-2, (int) Math.Ceiling(item2.TTL.TotalSeconds));
        }

        [Fact]
        public void TestExpires()
        {
            var now = DateTimeHelper.UnixTickNow;
            var item = new Item("dummy", new object { }, now + 1000);
            var expires = DateTimeHelper.Unix0.AddTicks(now + 1000);
            Assert.Equal(expires, item.Expires);
        }

    }
}
