using System;
using System.Collections.Generic;
using System.Linq;

namespace yate
{
    public class YateMessageEventArgs:EventArgs
    {
        public YateMessageEventArgs(string id, string time, string name, string result, IEnumerable<Tuple<string, string>> parameter)
        {
            Id = id;
            Name = name;
            Result = result;
            if (Int64.TryParse(time, out var timestamp))
            {
                Time = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
            Parameter = parameter.ToArray();
            NewParameter = new List<Tuple<string, string>>();
        }

        public string Id { get; }

        public string Name { get; set; }

        public string Result { get; set; }

        public DateTimeOffset Time { get; }

        public IReadOnlyList<Tuple<string, string>> Parameter { get; }

        public List<Tuple<string, string>> NewParameter { get; }

        public bool Handled { get; set; }
    }
}
