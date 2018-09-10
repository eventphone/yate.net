using System;
using System.Collections.Generic;
using System.Linq;

namespace eventphone.yate
{
    public class YateMessageEventArgs:EventArgs
    {
        public YateMessageEventArgs(string id, bool processed, string name, string result, IDictionary<string, string> parameter)
            :this(id, name, result, parameter)
        {
            Handled = processed;
        }

        public YateMessageEventArgs(string id, string time, string name, string result, IDictionary<string, string> parameter)
            :this(id, name, result, parameter)
        {
            if (Int64.TryParse(time, out var timestamp))
            {
                Time = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
        }

        private YateMessageEventArgs(string id, string name, string result, IDictionary<string, string> parameter)
        {
            Id = id;
            Name = name;
            Result = result;
            Parameter = parameter;
            NewParameter = new List<Tuple<string, string>>();
        }

        public string Id { get; }

        public string Name { get; set; }

        public string Result { get; set; }

        public DateTimeOffset Time { get; }

        public IDictionary<string, string> Parameter { get; }

        public List<Tuple<string, string>> NewParameter { get; }

        public bool Handled { get; set; }
        
        public string GetParameter(string key, string fallback = null)
        {
            if (Parameter.TryGetValue(key, out var value))
                return value;
            return fallback;
        }
    }
}
