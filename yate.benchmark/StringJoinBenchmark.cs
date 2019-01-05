using System;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace yate.benchmark
{
    public class StringJoinBenchmark
    {
        public StringJoinBenchmark()
        {
            if (StringJoin() != StringBuilderJoin())
                throw new InvalidOperationException("results differ");
            if (StringJoin() != SpanJoin())
                throw new InvalidOperationException("results differ");
        }

        public string[] Input { get; set; } = {"%%<message", "<id>", "<processed>", "[<name>]", "<retvalue>", null, "<key>=<value>"};
        private static readonly char[] SpecialChars = Enumerable.Range(0, 35).Select(x=>(char)x).ToArray();

        private static string Encode(string message)
        {
            int i;
            int index = 0;
            if ((i = message.IndexOfAny(SpecialChars, index)) < 0)
            {
                return message;
            }
            var sb = new StringBuilder(message.Length);
            do
            {
                if (index < i)
                {
                    sb.Append(message, index, i - index);
                }
                sb.Append('%');
                if (message[i] == '%')
                {
                    sb.Append('%');
                }
                else
                {
                    sb.Append((char) (message[i] + 64));
                }
                index = i + 1;
            } while ((i = message.IndexOfAny(SpecialChars, index)) >= 0);
            sb.Append(message, index, message.Length - index);
            return sb.ToString();
        }

        [Benchmark(Baseline = true)]
        public string StringJoin()
        {
            var parameter = Input;
            return String.Join(":", parameter.Where(x => x != null).Select(x => Encode(x)));
        }

        [Benchmark]
        public string StringBuilderJoin()
        {
            var parameter = Input;
            var sb = new StringBuilder();
            for (int i = 0; i < parameter.Length; i++)
            {
                if (parameter[i] == null) continue;
                sb.Append(Encode(parameter[i]));
                sb.Append(':');
            }
            return sb.ToString(0, sb.Length - 1);
        }

        [Benchmark]
        public string SpanJoin()
        {
            var parameter = Input;
            var length = 0;
            for (int i = 0; i < parameter.Length; i++)
            {
                if (parameter[i] == null) continue;
                parameter[i] = Encode(parameter[i]);
                length += parameter[i].Length + 1;
            }
            if (length == 0) return String.Empty;
            Span<char> result = stackalloc char[length];
            var target = result;
            for (int i = 0; i < parameter.Length; i++)
            {
                if (parameter[i] == null) continue;
                parameter[i].AsSpan().CopyTo(target);
                target[parameter[i].Length] = ':';
                target = target.Slice(parameter[i].Length+1);
            }
            return result.Slice(0, result.Length - 1).ToString();
        }
    }
}