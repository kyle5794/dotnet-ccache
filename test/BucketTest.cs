using System;
using Xunit;
using CCache;
using System.Collections.Generic;

namespace test
{
    public class BucketTest
    {
        [Fact]
        public void TestGetMissFromBucket()
        {
            var bucket = new Bucket();
            var item = bucket.GetOrDefault("omegalulz");
            Assert.Null(item);
        }

        [Fact]
        public void TestGetMissFromBucketThrowException()
        {
            var bucket = new Bucket();
            Assert.Throws<KeyNotFoundException>(() => bucket.Get("omegalulz"));
        }

        [Fact]
        public void TestSetNewBucketItem()
        {
            var bucket = new Bucket();
            var (item, existing) = bucket.Set("k", "lulz", TimeSpan.FromSeconds(1000));
            var value = (string)item.Value;
            Assert.Equal("lulz", value);
            Assert.Null(existing);
        }

        [Fact]
        public void TestSetExistingBucketItem()
        {
            var bucket = new Bucket();
            var (item1, existing1) = bucket.Set("k", "lulz", TimeSpan.FromSeconds(1000));
            var value1 = (string)item1.Value;
            Assert.Equal("lulz", value1);
            Assert.Null(existing1);

            var (item2, existing2) = bucket.Set("k", "omegalulzl", TimeSpan.FromSeconds(1000));
            var value2 = (string)item2.Value;
            Assert.Equal("omegalulzl", value2);
            var value3 = (string)existing2.Value;
            Assert.Equal("lulz", value3);
        }

        [Fact]
        public void TestGetHitFromBucket()
        {
            var bucket = new Bucket();
            bucket.Set("k", "lulz", TimeSpan.FromSeconds(1000));
            var item = bucket.GetOrDefault("k");
            var value = (string)item.Value;
            Assert.Equal("lulz", value);
        }

        [Fact]
        public void TestDeleteItemFromBucket()
        {
            var bucket = new Bucket();
            bucket.Set("k", "lulz", TimeSpan.FromSeconds(1000));
            var item = bucket.GetOrDefault("k");
            var value = (string)item.Value;
            Assert.Equal("lulz", value);
            bucket.Delete("k");
            Assert.Null(bucket.GetOrDefault("k"));
        }

    }
}
