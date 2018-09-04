using System;
using System.Runtime.Serialization;

namespace eventphone.yate
{
    [Serializable]
    public class YateException : Exception
    {
        public YateException() : base()
        {
        }

        public YateException(string message) : base(message)
        {
        }

        public YateException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public YateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
