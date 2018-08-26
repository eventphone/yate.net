using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace yate
{
    public class YateClient:IDisposable
    {
        private readonly YateSerializer _serializer;
        private readonly TcpClient _client;
        private readonly string _host;
        private readonly ushort _port;
        private Thread _reader;
        private bool _isclosed = false;
        private ConcurrentDictionary<string, YateResponse> _eventQueue = new ConcurrentDictionary<string, YateResponse>();

        private static class Commands
        {
            public static readonly string SConnect = "%>connect";
            public static readonly string SOutput = "%>output";
            public static readonly string SSetLocal = "%>setlocal";
            public const string RSetLocal = "%<setlocal";
            public const string RError = "Error in";
            public const string SMessage = "%>message";
            public const string RMessage = "%<message";
        }

        public YateClient(string host, ushort port)
        {
            _serializer = new YateSerializer();
            _client = new TcpClient();
            _host = host;
            _port = port;
        }

        /// <summary>
        /// As the conection is initiated from the external module the engine must be informed on the role of the connection.
        /// This must be the first request sent over a newly established socket connection.
        /// </summary>
        /// <remarks>
        /// The role and direction of the connection is established and then this keyword cannot be used again on the same connection.
        /// There is no answer to this request - if it fails the engine will slam the connection shut.
        /// </remarks>
        public Task ConnectAsync(RoleType role, CancellationToken cancellationToken)
        {
            return ConnectAsync(role, null, null, cancellationToken);
        }

        /// <summary>
        /// As the conection is initiated from the external module the engine must be informed on the role of the connection.
        /// This must be the first request sent over a newly established socket connection.
        /// </summary>
        /// <remarks>
        /// The role and direction of the connection is established and then this keyword cannot be used again on the same connection.
        /// There is no answer to this request - if it fails the engine will slam the connection shut.
        /// </remarks>
        public Task ConnectAsync(RoleType role, string channelId, CancellationToken cancellationToken)
        {
            return ConnectAsync(role, channelId, null, cancellationToken);
        }

        /// <summary>
        /// As the conection is initiated from the external module the engine must be informed on the role of the connection.
        /// This must be the first request sent over a newly established socket connection.
        /// </summary>
        /// <remarks>
        /// The role and direction of the connection is established and then this keyword cannot be used again on the same connection.
        /// There is no answer to this request - if it fails the engine will slam the connection shut.
        /// </remarks>
        public async Task ConnectAsync(RoleType role, string channelId, string channelType, CancellationToken cancellationToken)
        {
            await _client.ConnectAsync(_host, _port);
            string roleType;
            switch (role)
            {
                case RoleType.Global:
                    roleType = "global";
                    break;
                case RoleType.Channel:
                    roleType = "channel";
                    break;
                case RoleType.Play:
                    roleType = "play";
                    break;
                case RoleType.Record:
                    roleType = "record";
                    break;
                case RoleType.PlayRec:
                    roleType = "playrec";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role));
            }
            _reader = new Thread(Read) { IsBackground = true, Name = "YateClientReader" };
            _reader.Start();
            await SendAsync(Command(Commands.SConnect, roleType, channelId, channelType), cancellationToken);
        }

        /// <summary>
        /// used to relay arbitrary output messages to engine's logging output
        /// </summary>
        public async Task LogAsync(string message, CancellationToken cancellationToken)
        {
            await SendAsync(Command(Commands.SOutput) + ':' + message, cancellationToken);
        }

        public async Task<string> GetLocalAsync(string parameter, CancellationToken cancellationToken)
        {
            var result = await SendAsync(Commands.RSetLocal, parameter, cancellationToken, Commands.SSetLocal, parameter, String.Empty);
            return result[2];
        }

        /// <summary>
        /// sent by the application to the engine to ask it to process a message.
        /// </summary>
        /// <param name="name">name of the message</param>
        /// <param name="result">default textual return value of the message</param>
        /// <param name="parameter">enumeration of the key-value pairs of the message</param>
        /// <returns></returns>
        public async Task<YateMessage> SendMessageAsync(string name, string result, CancellationToken cancellationToken, params Tuple<string, string>[] parameter)
        {
            string id = Guid.NewGuid().ToString();
            var time = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var stringParams = new[] { Commands.SMessage, id, time, name, result }.Concat(parameter.Select(x => _serializer.Encode(x)));
            var response = await SendAsync(Commands.RMessage, id, cancellationToken, stringParams.ToArray());
            var resultParams = new List<Tuple<string, string>>();
            for (int i = 5; i < response.Length; i++)
            {
                resultParams.Add(_serializer.DecodeParameter(response[i]));
            }
            return new YateMessage
            {
                Id = response[1],
                Handled = "true".Equals(response[2], StringComparison.OrdinalIgnoreCase),
                Name = response[3],
                Result = response[4],
            };
        }

        private async Task<string[]> SendAsync(string responseCommand, string key, CancellationToken cancellationToken, params string[] parameter)
        {
            var response = new YateResponse();
            var eventKey = GetKey(responseCommand, key);
            if (!_eventQueue.TryAdd(eventKey, response))
                throw new ArgumentException("this command is currently pending");
            await SendAsync(Command(parameter), cancellationToken);
            return await response.GetResponseAsync(cancellationToken);
        }

        private string GetKey(string command, string key)
        {
            return command + ':' + key;
        }

        private async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            var buffer = Encoding.ASCII.GetBytes(message + '\n');
            var stream = _client.GetStream();
            await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private string Command(params string[] parameter)
        {
            return String.Join(":", parameter.Where(x => x != null).Select(x => _serializer.Encode(x)));
        }

        private void Read()
        {
            using(var reader = new StreamReader(_client.GetStream(), Encoding.ASCII))
            while (!_isclosed)
            {
                try
                {
                        var response = reader.ReadLine();
                        if (response == null) continue;
                        var parts = response.Split(':');
                        parts[0] = _serializer.Decode(parts[0]);
                        switch (parts[0])
                        {
                            case Commands.RSetLocal:
                                parts[1] = _serializer.Decode(parts[1]);
                                ProcessResponse(Commands.RSetLocal, parts[1], parts);
                                break;
                            case Commands.RError:
                                OnError(parts[1]);
                                break;
                            case Commands.RMessage:
                                parts[1] = _serializer.Decode(parts[1]);
                                ProcessResponse(Commands.RMessage, parts[1], parts);
                                break;
                            case Commands.SMessage:
                                OnMessageReceived(parts);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                }
                catch (IOException ex)
                {
                    var socketExept = ex.InnerException as SocketException;
                    if (socketExept == null || socketExept.ErrorCode != 10004)
                        throw;
                }
            }
        }

        protected virtual void OnMessageReceived(string[] parts)
        {
            var resultParams = new List<Tuple<string, string>>();
            for (int i = 5; i < parts.Length; i++)
            {
                resultParams.Add(_serializer.DecodeParameter(parts[i]));
            }
            var message = new YateMessageEventArgs(_serializer.Decode(parts[1]), _serializer.Decode(parts[2]), _serializer.Decode(parts[3]), _serializer.Decode(parts[4]), resultParams);
            var handler = MessageReceived;
            try
            {
                handler?.Invoke(this, message);
            }
            catch(Exception ex)
            {
                LogAsync(ex.Message, CancellationToken.None).GetAwaiter().GetResult();
            }
            var commands = new List<string> { Commands.RMessage, message.Id, message.Handled?"true":"false", message.Name, message.Result };
            commands.AddRange(message.NewParameter.Select(x => _serializer.Encode(x)));
            var response = Command(commands.ToArray());
            SendAsync(response, CancellationToken.None).GetAwaiter().GetResult();
        }

        public event EventHandler<YateMessageEventArgs> MessageReceived;

        private void ProcessResponse(string command, string key, string[] values)
        {
            if (_eventQueue.TryRemove(GetKey(command, key), out var response))
            {
                response.Values = values;
            }
        }

        private void OnError(string error)
        {
            foreach(var value in _eventQueue.Values)
            {
                value.Error = error;
            }
        }

        private class YateResponse:IDisposable
        {
            private readonly SemaphoreSlim _resetEvent;
            private string[] _values;
            private string _error;

            public YateResponse()
            {
                _resetEvent = new SemaphoreSlim(0, 1);
            }

            public string[] Values
            {
                get
                {
                    return _values;
                }
                set
                {
                    _values = value;
                    _resetEvent.Release();
                }
            }

            public string Error
            {
                get
                {
                    return _error;
                }
                set
                {
                    _error = value;
                    _resetEvent.Release();
                }
            }

            public async Task<string[]> GetResponseAsync(CancellationToken cancellationToken)
            {
                await _resetEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (_error != null)
                    throw new YateException(Error);
                return Values;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            protected void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _resetEvent?.Dispose();
                }
            }
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
