using System;
using CCache;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace bench
{
    [MemoryDiagnoser]
    public class CCacheBench
    {
        private static Random random = new Random();
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private BenchObject[] items;
        private const int _numObjects = 6000;
        private class BenchObject
        {
            public string V1 { get; set; }
            public int V2 { get; set; }
            public bool V3 { get; set; }
            public double V4 { get; set; }
        }


        [GlobalSetup]
        public void Setup()
        {
            items = new BenchObject[_numObjects];
            for (int i = 0; i < _numObjects; i++)
            {
                items[i] = new BenchObject
                {
                    V1 = RandomString(15),
                    V2 = random.Next(100),
                    V3 = random.Next(1) == 0,
                    V4 = random.NextDouble()
                };
            }
        }

        [Benchmark]
        public void InsertBenchmark()
        {
            var cache = new Cache();
            foreach (var item in items)
            {
                cache.Set(item.V1, item, TimeSpan.FromMinutes(2)).Wait();
            }
        }

        [Benchmark]
        public void GCBenchmark()
        {
            var cache = new Cache();
            foreach (var item in items)
            {
                cache.Set(item.V1, item, TimeSpan.FromMinutes(1)).Wait();
            }

            cache.Stop().Wait();
            cache.GC();
            cache.Restart();
        }

        private static string RandomString(int length)
            => new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}