using System;

namespace CCache
{
    public class Configuration
    {
        public Int64 MaxSize { get; set; }
        public int Buckets { get; set; }
        public int ItemsToPrune { get; set; }
        public int DeleteBuffer { get; set; }
        public int PromoteBuffer { get; set; }
        public int GetsPerPromote { get; set; }
        public bool Tracking { get; set; }
        public Action<Item> OnDelete;

        public Configuration()
        {
            MaxSize = 5000;
            Buckets = 16;
            ItemsToPrune = 500;
            DeleteBuffer = 1024;
            GetsPerPromote = 3;
            PromoteBuffer = 1024;
            Tracking = false;
        }

        // public static Configuration Configure()
        // {
        //     return new Configuration();
        // }
    }
}