using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eventphone.yate
{
    public partial class YateClient:IDisposable
    {
        private readonly YateSerializer _serializer;
        private readonly TcpClient _client;
        private readonly string _host;
        private readonly ushort _port;
        private Thread _reader;
        private bool _isclosed = false;
        private ConcurrentDictionary<string, YateResponse> _eventQueue = new ConcurrentDictionary<string, YateResponse>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, ConcurrentBag<Action<YateMessageEventArgs>>> _watchCallbacks;

        private static class Commands
        {
            public static readonly string SConnect = "%>connect";
            public static readonly string SOutput = "%>output";
            public static readonly string SSetLocal = "%>setlocal";
            public const string RSetLocal = "%<setlocal";
            public const string RError = "Error in";
            public const string SMessage = "%>message";
            public const string RMessage = "%<message";
            public static readonly string SInstall = "%>install";
            public const string RInstall = "%<install";
            public static readonly string SUninstall = "%>uninstall";
            public const string RUninstall = "%<uninstall";
            public static readonly string SWatch = "%>watch";
            public const string RWatch = "%<watch";
            public static readonly string SUnwatch = "%>unwatch";
            public const string RUnwatch = "%<unwatch";
        }

        public YateClient(string host, ushort port)
        {
            _serializer = new YateSerializer();
            _client = new TcpClient();
            _host = host;
            _port = port;
            _watchCallbacks = new ConcurrentDictionary<string, ConcurrentBag<Action<YateMessageEventArgs>>>();
            Watched += InvokeWatchCallbacks;
        }

        private string GetKey(string command, string key)
        {
            return command + ':' + key;
        }

        private string Command(params string[] parameter)
        {
            return String.Join(":", parameter.Where(x => x != null).Select(x => _serializer.Encode(x)));
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _isclosed = true;
                    _client.Dispose();
                    _reader?.Join();
                }
                disposedValue = true;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    public enum RoleType
    {
        Global,
        Channel,
        Play,
        Record,
        PlayRec
    }
}
