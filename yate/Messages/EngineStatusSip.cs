using System;
using System.Collections.Generic;
using System.Text;
using yate;

namespace eventphone.yate.Messages
{
    public class EngineStatusSip : IYateMessageResponse<SipStatusResponse>
    {
        public string Name => "engine.status";

        public string Result => String.Empty;

        public IEnumerable<Tuple<string, string>> Parameters
        {
            get
            {
                yield return new Tuple<string, string>("module", "sip");
            }
        }

        public SipStatusResponse ParseResponse(YateMessageResponse response, YateSerializer serializer)
        {
            return new SipStatusResponse(response.Result.Trim(), serializer);
        }
    }

    public class SipStatusResponse
    {
        public SipStatusResponse(string response, YateSerializer serializer)
        {
            var parts = response.Split(';');
            ParseInfo(parts[0], serializer);
            Stats = new SipStatistics(parts[1], serializer);
        }

        private void ParseInfo(string info, YateSerializer serializer)
        {
            var parts = info.Split(',');
            foreach(var part in parts)
            {
                var tuple = serializer.DecodeParameter(part);
                switch (tuple.Item1.ToLower())
                {
                    case "name":
                        Name = tuple.Item2;
                        break;
                    case "format":
                        Format = tuple.Item2;
                        break;
                }
            }
        }

        public string Name { get; private set; }

        public string Format { get; private set; }

        public SipStatistics Stats { get; private set; }
    }

    public class SipStatistics
    {
        public SipStatistics(string stats, YateSerializer serializer)
        {
            var parts = stats.Split(',');
            foreach(var part in parts)
            {
                var tuple = serializer.DecodeParameter(part);
                if (!Int64.TryParse(tuple.Item2, out var value))
                    continue;
                switch (tuple.Item1.ToLower())
                {
                    case "routed":
                        Routed = value;
                        break;
                    case "routing":
                        Routing= value;
                        break;
                    case "total":
                        Total = value;
                        break;
                    case "chans":
                        Chans = value;
                        break;
                    case "transactions":
                        Transactions = value;
                        break;
                }
            }
        }

        public long Routed { get; internal set; }

        public long Routing { get; internal set; }

        public long Total { get; internal set; }

        public long Chans { get; internal set; }

        public long Transactions { get; internal set; }
    }
}
