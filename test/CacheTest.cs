using System;
using Xunit;
using CCache;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace test
{
    public class CacheTest
    {
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
        public async Task TestDeleteAPrefix()
        {
            var cache = new Cache();
            Assert.Equal(0, cache.ItemCount);
            await cache.Set("sekiro::wolf", "sabimaru", TimeSpan.FromSeconds(1000));
            await cache.Set("sekiro::genichiro", "bmb", TimeSpan.FromSeconds(1000));
            await cache.Set("darksoul::sif", "wolf", TimeSpan.FromSeconds(1000));
            await cache.Set("darksoul::artorias", "bsm", TimeSpan.FromSeconds(1000));
            Assert.Equal(4, cache.ItemCount);

            var count = await cache.DeletePrefix("sekiro");
            Assert.Equal(2, count);

            await Task.Delay(2000);

            Assert.Equal(2, cache.ItemCount);
            Assert.Null(await cache.GetOrDefault("sekiro::wolf"));
            Assert.Null(await cache.GetOrDefault("sekiro::genichiro"));

            var i1 = await cache.GetOrDefault("darksoul::sif");
            Assert.Equal("wolf", i1.Value<string>());

            var i2 = await cache.GetOrDefault("darksoul::artorias");
            Assert.Equal("bsm", i2.Value<string>());
        }

        [Fact]
        public async Task TestOnDeleteCallbackCalled()
        {
            var onDeleteFnCalled = false;
            Action<Item> onDeleteFn = (Item item) =>
            {
                if (item.Key == "genichiro") onDeleteFnCalled = true;
            };

            var cache = new Cache(new Configuration()
            {
                ItemsToPrune = 1,
                MaxSize = 10,
                OnDelete = onDeleteFn
            });

            await cache.Set("sekiro", "one arm wolf", TimeSpan.FromMinutes(1));
            await cache.Set("genichiro", "bmb", TimeSpan.FromMinutes(1));
            Assert.Equal(2, cache.ItemCount);

            await Task.Delay(1000);
            Assert.True(await cache.Delete("genichiro"));
            await Task.Delay(1000);

            Assert.Null(await cache.GetOrDefault("genichiro"));
            var item = await cache.GetOrDefault("sekiro");
            Assert.Equal("one arm wolf", item.Value<string>());
            Assert.True(onDeleteFnCalled);
        }

        [Fact]
        public async Task TestFetchExpiredItem()
        {
            var cache = new Cache();

            await cache.Set("sekiro", "one arm wolf", TimeSpan.FromMinutes(-1)); // Expired item
            await cache.Set("emma", "the physician", TimeSpan.FromHours(1)); // Not expired item
            Assert.Equal(2, cache.ItemCount);

            Func<string> fetchFN = () => "mortal blade";
            await Task.Delay(2000);

            var sekiro = await cache.Fetch("sekiro", TimeSpan.FromSeconds(1), fetchFN);
            Assert.Equal("mortal blade", sekiro.Value<string>());

            var emma = await cache.Fetch("emma", TimeSpan.FromSeconds(1), fetchFN);
            Assert.Equal("the physician", emma.Value<string>());
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
            for (int i = 0; i < 15; i++)
            {
                await cache.Set(i.ToString(), i.ToString(), TimeSpan.FromMinutes(1));
            }
            Assert.Equal(15, cache.ItemCount);
            Assert.Equal("0", (await cache.GetOrDefault("0")).Value<string>());
            Assert.Equal("9", (await cache.GetOrDefault("9")).Value<string>());
            Assert.True(await cache.Delete("13"));

            await Task.Delay(2000);
            await cache.Stop();
            cache.GC();
            cache.Restart();
            Assert.Null(await cache.GetOrDefault("1"));
            Assert.Null(await cache.GetOrDefault("10"));
            Assert.Null(await cache.GetOrDefault("11"));
            Assert.Equal("0", (await cache.GetOrDefault("0")).Value<string>());
            Assert.Equal("9", (await cache.GetOrDefault("9")).Value<string>());
            Assert.Equal("14", (await cache.GetOrDefault("14")).Value<string>());
            Assert.Equal(4, cache.ItemCount);
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

        [Fact]
        public async Task TestTrackerDoesNotCleanupHeldInstance()
        {
            var cache = new Cache(new Configuration()
            {
                ItemsToPrune = 10
            });

            for (int i = 0; i < 10; i++)
            {
                await cache.Set(i.ToString(), i.ToString(), TimeSpan.FromMinutes(1));
            }

            var item = await cache.TrackingGet("0");

            await Task.Delay(2000);

            await cache.Stop();
            cache.GC();
            cache.Restart();

            Assert.Equal(1, cache.ItemCount);
            var i0 = await cache.GetOrDefault("0");
            Assert.Equal("0", i0.Value<string>());
            Assert.Null(await cache.GetOrDefault("1"));
            Assert.Null(await cache.GetOrDefault("9"));

            item.Release();
            await cache.Stop();
            cache.GC();
            cache.Restart();
            Assert.Null(await cache.GetOrDefault("0"));
        }
    }
}