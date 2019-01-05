using System;
using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace yate.benchmark
{
    [MemoryDiagnoser]
    public class DecodeBenchmark
    {
        [Params("a%}a%zb%}%}bb", "a%%a%%%}a%Jb", "test")]
        public string Encoded { get; set; }

        public DecodeBenchmark()
        {
            Encoded = "a%}a%zb%}%}bb";
            if (DecodeStringbuilder() != DecodeSpan())
                throw new InvalidOperationException("results differ");
            if (DecodeStringbuilder() != DecodeSpanBuffer())
                throw new InvalidOperationException("results differ");
        }

        [Benchmark(Baseline = true)]
        public string DecodeStringbuilder()
        {
            var message = Encoded;
            var sb = new StringBuilder(message.Length);
            int i;
            int index = 0;
            while((i = message.IndexOf('%', index)) >= 0)
            {
                if (message.Length == i + 1)
                    throw new Exception(message);
                if (message[i + 1] != '%' && (int)message[i + 1] <= 64)
                    throw new Exception(message);
                if (index < i)
                    sb.Append(message, index, i - index);
                if (message[i + 1] == '%')
                {
                    sb.Append('%');
                }
                else
                {
                    sb.Append((char)(message[i + 1] - 64));
                }
                index = i + 2;
            }
            sb.Append(message, index, message.Length - index);
            return sb.ToString();
        }

        [Benchmark]
        public string DecodeSpan()
        {
            var message = Encoded.AsSpan();
            var sb = new StringBuilder(message.Length);
            int i;
            while((i = message.IndexOf('%')) >= 0)
            {
                if (message.Length == i + 1)
                    throw new Exception(new string(message));
                if (message[i + 1] != '%' && (int)message[i + 1] <= 64)
                    throw new Exception(new string(message));
                if (i > 0)
                    sb.Append(message.Slice(0, i));
                if (message[i + 1] == '%')
                {
                    sb.Append('%');
                }
                else
                {
                    sb.Append((char)(message[i + 1] - 64));
                }
                message = message.Slice(i + 2);
            }
            sb.Append(message);
            return sb.ToString();
        }

        [Benchmark]
        public string DecodeSpanBuffer()
        {
            var message = Encoded.AsSpan();
            int i;
            if((i = message.IndexOf('%')) >= 0)
            {
                Span<char> buffer = stackalloc char[message.Length];
                var target = buffer.Slice(0);
                do
                {
                    if (message.Length == i + 1)
                    {
                        throw new Exception(new string(message));
                    }
                    if (message[i + 1] != '%' && (int) message[i + 1] <= 64)
                    {
                        throw new Exception(new string(message));
                    }
                    if (i > 0)
                    {
                        message.Slice(0, i).CopyTo(target);
                        target = target.Slice(i);
                    }
                    if (message[i + 1] == '%')
                    {
                        target[0] = '%';
                        target = target.Slice(1);
                    }
                    else
                    {
                        target[0] = (char) (message[i + 1] - 64);
                        target = target.Slice(1);
                    }
                    message = message.Slice(i + 2);
                } while ((i = message.IndexOf('%')) >= 0);
                
                message.CopyTo(target);
                return buffer.Slice(0, buffer.Length - target.Length + message.Length).ToString();
            }
            return Encoded;
        }
    }
}