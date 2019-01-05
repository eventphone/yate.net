using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace yate.benchmark
{
    public class StringSplitBenchmark
    {
        public StringSplitBenchmark()
        {
            var split = StringSplit();
            var span = SpanSplit();
            if (split.Count != span.Count)
                throw new InvalidOperationException("results differ");
            for (int i = 0; i < split.Count; i++)
            {
                if (split[i] != span[i])
                    throw new InvalidOperationException("results differ");
            }
        }
        public string Input { get; set; } = "%%<message:<id>:<processed>:[<name>]:<retvalue>:<key>=<value>";

        [Benchmark(Baseline = true)]
        public IList<string> StringSplit()
        {
            var message = Input;
            return message.Split(':');
        }

        [Benchmark]
        public IList<string> SpanSplit()
        {
            var message = Input.AsSpan();
            var result = new List<string>(16);
            int i;
            while ((i = message.IndexOf(':')) >= 0)
            {
                result.Add(message.Slice(0, i).ToString());
                message = message.Slice(i + 1);
            }
            result.Add(message.ToString());
            return result;
        }
    }
}