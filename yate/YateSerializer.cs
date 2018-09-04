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
            var sb = new StringBuilder(message.Length);
            int i;
            int index = 0;
            while((i = message.IndexOf('%', index)) >= 0)
            {
                if (message.Length == i + 1)
                    throw new MessageParseException(message);
                if (message[i + 1] != '%' && (int)message[i + 1] <= 64)
                    throw new MessageParseException(message);
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
