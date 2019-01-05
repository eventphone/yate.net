using System;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace yate.benchmark
{
    [MemoryDiagnoser]
    public class EncodeBenchmark
    {
        private static readonly char[] SpecialChars = Enumerable.Range(0, 35).Select(x=>(char)x).ToArray();
        static EncodeBenchmark()
        {
            SpecialChars[32] = '%';
            SpecialChars[33] = '=';
            SpecialChars[34] = ':';
        }

        [Params("aaaa=bb", "test", "a%a%=a\nb")]
        public string Input { get; set; } = "a%a%=a\nb";

        public EncodeBenchmark()
        {
            if (EncodeStringBuilder() != EncodeSpan())
                throw new InvalidOperationException("results differ");
            if (EncodeStringBuilder() != EncodeStringBuilderShort())
                throw new InvalidOperationException("results differ");
            if (EncodeStringBuilder() != EncodeSpanBuffer())
                throw new InvalidOperationException("results differ");
            if (EncodeStringBuilder() != EncodeRecursive())
                throw new InvalidOperationException("results differ");
        }

        [Benchmark(Baseline = true)]
        public string EncodeStringBuilder()
        {
            var message = Input;
            var sb = new StringBuilder(message.Length);
            int i;
            int index = 0;
            while((i = message.IndexOfAny(SpecialChars, index)) >= 0)
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
                    sb.Append((char)(message[i] + 64));
                }
                index = i + 1;
            }
            sb.Append(message, index, message.Length - index);
            return sb.ToString();
        }

        [Benchmark]
        public string EncodeStringBuilderShort()
        {
            var message = Input;
            int i;
            int index = 0;
            if ((i = message.IndexOfAny(SpecialChars, index)) >= 0)
            {
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
            return message;
        }

        //[Benchmark]
        public string EncodeSpan()
        {
            var message = Input.AsSpan();
            var sb = new StringBuilder(message.Length);
            int i;
            while((i = message.IndexOfAny(SpecialChars)) >= 0)
            {
                if (i > 0)
                {
                    sb.Append(message.Slice(0, i));
                }
                sb.Append('%');
                if (message[i] == '%')
                {
                    sb.Append('%');
                }
                else
                {
                    sb.Append((char)(message[i] + 64));
                }
                message = message.Slice(i+1);
            }
            sb.Append(message);
            return sb.ToString();
        }

        //[Benchmark]
        public string EncodeSpanBuffer()
        {
            var message = Input.AsSpan();
            int count = 0;
            int i;
            while ((i = message.IndexOfAny(SpecialChars)) >= 0)
            {
                count++;
                message = message.Slice(i + 1);
            }
            message = Input.AsSpan();
            Span<char> buffer = stackalloc char[message.Length + count];
            var target = buffer.Slice(0);
            while((i = message.IndexOfAny(SpecialChars)) >= 0)
            {
                if (i > 0)
                {
                    message.Slice(0, i).CopyTo(target);
                    target = target.Slice(i);
                }
                target[0] = '%';
                if (message[i] == '%')
                {
                    target[1] = '%';
                }
                else
                {
                    target[1] = (char)(message[i] + 64);
                }
                message = message.Slice(i + 1);
                target = target.Slice(2);
            }
            message.CopyTo(target);
            return buffer.ToString();
        }

        //[Benchmark]
        public string EncodeRecursive()
        {
            var encoded = EncodeRecursive(0, Input.AsSpan());
            if (encoded == null) return Input;
            return encoded.ToString();
        }

        private static Span<char> EncodeRecursive(int encodedLength, ReadOnlySpan<char> toEncode)
        {
            int i;
            if ((i = toEncode.IndexOfAny(SpecialChars)) >= 0)
            {
                var result = EncodeRecursive(encodedLength + i + 2, toEncode.Slice(i+1));
                var target = result.Slice(encodedLength);
                toEncode.Slice(0, i).CopyTo(target);
                target[i] = '%';
                if (toEncode[i] == '%')
                {
                    target[i+1] = '%';
                }
                else
                {
                    target[i+1] = (char)(toEncode[i] + 64);
                }
                return result;
            }
            else if (encodedLength == 0)
            {
                return null;
            }
            else{
                Span<char> result = new char[encodedLength + toEncode.Length];
                toEncode.CopyTo(result.Slice(encodedLength));
                return result;
            }
        }
    }
}