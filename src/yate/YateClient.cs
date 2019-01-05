using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace eventphone.yate
{
    public partial class YateClient:IDisposable
    {
        private readonly YateSerializer _serializer;
        private readonly TcpClient _client;
        protected Stream InputStream;
        protected Stream OutputStream;
        private readonly string _host;
        private readonly ushort _port;
        private Thread _reader;
        private bool _isclosed;
        private readonly ConcurrentDictionary<string, YateResponse> _eventQueue = new ConcurrentDictionary<string, YateResponse>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, ConcurrentBag<Action<YateMessageEventArgs>>> _watchCallbacks;
        
        public YateClient()
        {
            _serializer = new YateSerializer();
            _watchCallbacks = new ConcurrentDictionary<string, ConcurrentBag<Action<YateMessageEventArgs>>>();
            Watched += InvokeWatchCallbacks;
            InputStream = Console.OpenStandardInput();
            OutputStream = Console.OpenStandardOutput();
        }

        public YateClient(string host, ushort port)
            : this()
        {
            _client = new TcpClient();
            _host = host;
            _port = port;
        }

        private void StartReader()
        {
            _reader = new Thread(Read) {IsBackground = true, Name = "YateClientReader"};
            _reader.Start();
        }

        private string GetKey(string command, string key)
        {
            return command + ':' + key;
        }

        private string Command(params string[] parameter)
        {
            var length = 0;
            for (int i = 0; i < parameter.Length; i++)
            {
                if (parameter[i] == null) continue;
                parameter[i] = _serializer.Encode(parameter[i]);
                length += parameter[i].Length + 1;
            }
            if (length == 0) return String.Empty;
            Span<char> result = stackalloc char[length];
            var target = result;
            for (int i = 0; i < parameter.Length; i++)
            {
                if (parameter[i] == null) continue;
                parameter[i].AsSpan().CopyTo(target);
                target[parameter[i].Length] = ':';
                target = target.Slice(parameter[i].Length+1);
            }
            return result.Slice(0, result.Length - 1).ToString();
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
                    _client?.Dispose();
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
