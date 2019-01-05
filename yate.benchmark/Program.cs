using System;
using System.Collections.Generic;
using BenchmarkDotNet.Running;

namespace yate.benchmark
{
    class Program
    {
        private static readonly Dictionary<string,Type> _benchmarks = new Dictionary<string, Type>();

        static Program()
        {
            AddBenchmark<DecodeBenchmark>();
        }

        private static void AddBenchmark<T>()
        {
            var type = typeof(T);
            _benchmarks.Add(type.Name, type);
        }

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: dotnet yate.benchmark.dll <benchmark>");
                foreach (var name in _benchmarks.Keys)
                {
                    Console.WriteLine($"\t{name}");
                }
                return;
            }
            var benchmark = _benchmarks[args[0]];
            Activator.CreateInstance(benchmark);
            BenchmarkRunner.Run(benchmark);
        }
    }
}
