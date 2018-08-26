using System;
using System.Runtime.Serialization;

namespace yate
{
    [Serializable]
    public class MessageParseException : Exception
    {
        public MessageParseException():base()
        {
        }

        public MessageParseException(string message):base(message)
        {
        }

        public MessageParseException(string message, Exception innerException) :base(message, innerException)
        {
        }

        public MessageParseException(SerializationInfo info, StreamingContext context):base(info, context)
        {

        }
    }
}
