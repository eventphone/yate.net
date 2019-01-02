using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace eventphone.yate
{
    public partial class YateClient:IDisposable
    {
        private readonly YateSerializer _serializer;
        private readonly TcpClient _client;
        private readonly string _host;
        private readonly ushort _port;
        private Thread _reader;
        private bool _isclosed;
        private readonly ConcurrentDictionary<string, YateResponse> _eventQueue = new ConcurrentDictionary<string, YateResponse>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, ConcurrentBag<Action<YateMessageEventArgs>>> _watchCallbacks;

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
        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _isclosed = true;
                    _client.Dispose();
                    _reader?.Join();
                }
                _disposed = true;
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
