using System;
using System.Collections.Generic;

namespace eventphone.yate
{
    public interface IYateMessageResponse<T>
    {
        T ParseResponse(YateMessageResponse response, YateSerializer serializer);

        string Name { get; }

        string Result { get; }

        IEnumerable<Tuple<string,string>> Parameters { get; }
    }

    public class YateMessageResponse
    {
        /// <summary>
        /// same message ID string received trough %%>message
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// boolean ("true" or "false") indication if the message has been declared processed by one of the handlers
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// name of the message, possibly changed
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// textual return value of the message
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// key-value pairs of parameters to the message.
        /// </summary>
        public IEnumerable<Tuple<string, string>> Parameter {get;set;}
    }
}
