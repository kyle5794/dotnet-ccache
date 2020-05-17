using System;
using Xunit;
using CCache;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit.Abstractions;
namespace test
{
    public class CacheTest
    {
        private ITestOutputHelper helper;
        public CacheTest(ITestOutputHelper helper)
        {
            this.helper = helper;
        }
        [Fact]
        public void TestGetMissFromCache()
        {
            var cache = new Cache();
            Assert.ThrowsAsync<KeyNotFoundException>(() => cache.Get("omegalulz"));
        }

        [Fact]
        public async Task TestDeleteAValue()
        {
            var cache = new Cache();
            Assert.Equal(0, cache.ItemCount);
            await cache.Set("sekiro", "sabimaru", TimeSpan.FromSeconds(1000));
            await cache.Set("genichiro", "bmb", TimeSpan.FromSeconds(1000));
            Assert.Equal(2, cache.ItemCount);

            var delete = await cache.Delete("genichiro");
            Assert.True(delete);

            Assert.Null(await cache.GetOrDefault("genichiro"));
            Assert.Equal(1, cache.ItemCount);
            var item = await cache.GetOrDefault("sekiro");
            Assert.Equal("sabimaru", item.Value<string>());
        }

        [Fact]
        public async Task TestGCTheOldestItem()
        {
            var cache = new Cache();

            for (int i = 0; i < 1000; i++)
            {
                await cache.Set(i.ToString(), i.ToString(), TimeSpan.FromMinutes(1));
            }
            Assert.Equal(1000, cache.ItemCount);

            await Task.Delay(10000);
            await cache.Stop();
            cache.GC();
            cache.Restart();

            Assert.Null(await cache.GetOrDefault("0"));
            Assert.Null(await cache.GetOrDefault("499"));
            Assert.Equal("500", (await cache.GetOrDefault("500")).Value<string>());
            Assert.Equal("999", (await cache.GetOrDefault("999")).Value<string>());
            Assert.Equal(500, cache.ItemCount);
        }

        [Fact]
        public async Task TestPromotedItemsDontGetPruned()
        {
            var cache = new Cache(new Configuration()
            {
                GetsPerPromote = 1,
                ItemsToPrune = 10,
            });
            helper.WriteLine("commence testing");
            for (int i = 0; i < 15; i++)
            {
                await cache.Set(i.ToString(), i.ToString(), TimeSpan.FromMinutes(1));
            }
            Assert.Equal(15, cache.ItemCount);
            Assert.Equal("0", (await cache.GetOrDefault("0")).Value<string>());
            Assert.Equal("9", (await cache.GetOrDefault("9")).Value<string>());
            Assert.True(await cache.Delete("13"));
            helper.WriteLine("finish insert");

            await Task.Delay(2000);
            await cache.Stop();
            cache.GC();
            cache.Restart();
            helper.WriteLine("finish restart");
            Assert.Null(await cache.GetOrDefault("1"));
            Assert.Null(await cache.GetOrDefault("10"));
            Assert.Null(await cache.GetOrDefault("11"));
            Assert.Equal("0", (await cache.GetOrDefault("0")).Value<string>());
            Assert.Equal("9", (await cache.GetOrDefault("9")).Value<string>());
            Assert.Equal("14", (await cache.GetOrDefault("14")).Value<string>());
            Assert.Equal(4, cache.ItemCount);
            Assert.True(false);
        }

        [Fact]
        public async Task TestRemoveOldestItemWhenFull()
        {
            var onDeleteFnCalled = false;
            Action<Item> onDeleteFn = (Item item) =>
            {
                if (item.Key == "0") onDeleteFnCalled = true;
            };

            var cache = new Cache(new Configuration()
            {
                ItemsToPrune = 1,
                MaxSize = 10,
                OnDelete = onDeleteFn
            });

            for (int i = 0; i < 12; i++)
            {
                await cache.Set(i.ToString(), i.ToString(), TimeSpan.FromMinutes(1));
            }

            await Task.Delay(2000);

            Assert.Null(await cache.GetOrDefault("0"));
            Assert.Null(await cache.GetOrDefault("1"));
            Assert.Equal("2", (await cache.GetOrDefault("2")).Value<string>());
            Assert.Equal("3", (await cache.GetOrDefault("3")).Value<string>());
            Assert.Equal("11", (await cache.GetOrDefault("11")).Value<string>());
            Assert.Equal(10, cache.ItemCount);
            Assert.True(onDeleteFnCalled);
        }
    }
}