using System;
using System.Linq;
using System.Text;

namespace eventphone.yate
{
    public class YateSerializer
    {
        private static readonly char[] SpecialChars = Enumerable.Range(0, 35).Select(x=>(char)x).ToArray();
        static YateSerializer()
        {
            SpecialChars[32] = '%';
            SpecialChars[33] = '=';
            SpecialChars[34] = ':';
        }

        public string Encode(string message)
        {
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

        public string Decode(string message)
        {
            var input = message.AsSpan();
            int i;
            if((i = input.IndexOf('%')) >= 0)
            {
                Span<char> buffer = stackalloc char[input.Length];
                var target = buffer.Slice(0);
                do
                {
                    if (input.Length == i + 1)
                    {
                        throw new YateException(message);
                    }
                    if (input[i + 1] != '%' && (int) input[i + 1] <= 64)
                    {
                        throw new YateException(message);
                    }
                    if (i > 0)
                    {
                        input.Slice(0, i).CopyTo(target);
                        target = target.Slice(i);
                    }
                    if (input[i + 1] == '%')
                    {
                        target[0] = '%';
                        target = target.Slice(1);
                    }
                    else
                    {
                        target[0] = (char) (input[i + 1] - 64);
                        target = target.Slice(1);
                    }
                    input = input.Slice(i + 2);
                } while ((i = input.IndexOf('%')) >= 0);
                
                input.CopyTo(target);
                return buffer.Slice(0, buffer.Length - target.Length + input.Length).ToString();
            }
            return message;
        }

        public string Encode(Tuple<string,string> parameter)
        {
            var left = Encode(parameter.Item1);
            var right = Encode(parameter.Item2);
            return left.Replace("=", "%}") + '=' + right.Replace("=", "%}");
        }

        public Tuple<string,string> DecodeParameter(string parameter)
        {
            var parts = parameter.Split('=');
            var left = Decode(parts[0]);
            var right = Decode(parts[1]);
            return new Tuple<string, string>(left, right);
        }
    }
}
