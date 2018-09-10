using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eventphone.yate
{
    public partial class YateClient
    {
        /// <summary>
        /// As the conection is initiated from the external module the engine must be informed on the role of the connection.
        /// This must be the first request sent over a newly established socket connection.
        /// </summary>
        /// <remarks>
        /// The role and direction of the connection is established and then this keyword cannot be used again on the same connection.
        /// There is no answer to this request - if it fails the engine will slam the connection shut.
        /// </remarks>
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return ConnectAsync(RoleType.Global, null, null, cancellationToken);
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
            await _client.ConnectAsync(_host, _port).ConfigureAwait(false);
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
            _reader = new Thread(Read) {IsBackground = true, Name = "YateClientReader"};
            _reader.Start();
            await SendAsync(Command(Commands.SConnect, roleType, channelId, channelType), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// used to relay arbitrary output messages to engine's logging output
        /// </summary>
        public Task LogAsync(string message, CancellationToken cancellationToken)
        {
            return SendAsync(Command(Commands.SOutput) + ':' + message, cancellationToken);
        }

        /// <summary>
        /// query local parameter
        /// </summary>
        public Task<string> GetLocalAsync(string parameter, CancellationToken cancellationToken)
        {
            return SetLocalAsync(parameter, String.Empty, cancellationToken);
        }

        /// <summary>
        /// requests the change of a local parameter
        /// </summary>
        public async Task<string> SetLocalAsync(string parameter, string value, CancellationToken cancellationToken)
        {
            var result = await SendAsync(Commands.RSetLocal, parameter, cancellationToken, Commands.SSetLocal, parameter, value).ConfigureAwait(false);
            return _serializer.Decode(result[2]);
        }

        /// <summary>
        /// sent by the application to the engine to ask it to process a message.
        /// </summary>
        /// <param name="name">name of the message</param>
        /// <param name="result">default textual return value of the message</param>
        /// <param name="parameter">enumeration of the key-value pairs of the message</param>
        /// <returns></returns>
        public async Task<YateMessageResponse> SendMessageAsync(string name, string result, CancellationToken cancellationToken, params Tuple<string, string>[] parameter)
        {
            string id = Guid.NewGuid().ToString();
            var time = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var response = await SendAsync(Commands.RMessage, id, cancellationToken, parameter, Commands.SMessage, id, time, name, result).ConfigureAwait(false);
            var resultParams = new List<Tuple<string, string>>();
            for (int i = 5; i < response.Length; i++)
            {
                resultParams.Add(_serializer.DecodeParameter(response[i]));
            }
            return new YateMessageResponse
            {
                Id = response[1],
                Handled = "true".Equals(_serializer.Decode(response[2]), StringComparison.OrdinalIgnoreCase),
                Name = _serializer.Decode(response[3]),
                Result = _serializer.Decode(response[4]),
                Parameter = resultParams
            };
        }

        public async Task<T> SendMessageAsync<T>(IYateMessageResponse<T> message, CancellationToken cancellationToken)
        {
            var result = await SendMessageAsync(message.Name, message.Result, cancellationToken, message.Parameters.ToArray()).ConfigureAwait(false);
            return message.ParseResponse(result, _serializer);
        }

        public Task<InstallResult> InstallAsync(string name, string filterName, CancellationToken cancellationToken)
        {
            return InstallAsync(null, name, filterName, null, cancellationToken);
        }

        public Task<InstallResult> InstallAsync(string name, CancellationToken cancellationToken)
        {
            return InstallAsync(null, name, null, null, cancellationToken);
        }

        public Task<InstallResult> InstallAsync(string name, string filterName, string filterValue, CancellationToken cancellationToken)
        {
            return InstallAsync(null, name, filterName, filterValue, cancellationToken);
        }

        public Task<InstallResult> InstallAsync(int priority, string name, string filterName, string filterValue, CancellationToken cancellationToken)
        {
            return InstallAsync((int?) priority, name, filterName, filterValue, cancellationToken);
        }

        public async Task<InstallResult> UninstallAsync(string name, CancellationToken cancellationToken)
        {
            var result = await SendAsync(Commands.RUninstall, name, cancellationToken, Commands.SUninstall, name).ConfigureAwait(false);
            return new InstallResult(_serializer.Decode(result[1]), _serializer.Decode(result[3]));
        }

        public async Task<bool> WatchAsync(string name, CancellationToken cancellationToken)
        {
            var result = await SendAsync(Commands.RWatch, name, cancellationToken, Commands.SWatch, name).ConfigureAwait(false);
            return "true".Equals(_serializer.Decode(result[2]), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> WatchAsync(string name, Action<YateMessageEventArgs> callback, CancellationToken cancellationToken)
        {
            var bag = _watchCallbacks.GetOrAdd(name, new ConcurrentBag<Action<YateMessageEventArgs>>());
            bag.Add(callback);
            var response = await SendAsync(Commands.RWatch, name, cancellationToken, Commands.SWatch, name).ConfigureAwait(false);
            return "true".Equals(_serializer.Decode(response[2]), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> UnwatchAsync(string name, CancellationToken cancellationToken)
        {
            var result = await SendAsync(Commands.RUnwatch, name, cancellationToken, Commands.SUnwatch, name).ConfigureAwait(false);
            return "true".Equals(_serializer.Decode(result[2]), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<InstallResult> InstallAsync(int? priority, string name, string filterName, string filterValue, CancellationToken cancellationToken)
        {
            var result = await SendAsync(Commands.RInstall, name, cancellationToken, Commands.SInstall, priority?.ToString() ?? String.Empty, name, filterName, filterValue).ConfigureAwait(false);
            return new InstallResult(_serializer.Decode(result[1]), _serializer.Decode(result[3]));
        }

        private Task<string[]> SendAsync(string responseCommand, string key, CancellationToken cancellationToken, params string[] parameter)
        {
            return SendAsync(responseCommand, key, cancellationToken, null, parameter);
        }

        private async Task<string[]> SendAsync(string responseCommand, string key, CancellationToken cancellationToken, Tuple<string,string>[] parameter, params string[] message)
        {
            var response = new YateResponse();
            var eventKey = GetKey(responseCommand, key);
            if (!_eventQueue.TryAdd(eventKey, response))
                throw new ArgumentException("this command is currently pending");
            var command = Command(message);
            if (parameter != null)
            {
                command += ':' + String.Join(":", parameter.Select(x => _serializer.Encode(x)));
            }
            await SendAsync(command, cancellationToken).ConfigureAwait(false);
            return await response.GetResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            var buffer = Encoding.UTF8.GetBytes(message + '\n');
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var stream = _client.GetStream();
                await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}