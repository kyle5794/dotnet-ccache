using BenchmarkDotNet.Running;

namespace bench
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<CCacheBench>();
        }
    }
}
