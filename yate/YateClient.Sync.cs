using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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
        public void Connect()
        {
            Connect(RoleType.Global, null, null);
        }

        /// <summary>
        /// As the conection is initiated from the external module the engine must be informed on the role of the connection.
        /// This must be the first request sent over a newly established socket connection.
        /// </summary>
        /// <remarks>
        /// The role and direction of the connection is established and then this keyword cannot be used again on the same connection.
        /// There is no answer to this request - if it fails the engine will slam the connection shut.
        /// </remarks>
        public void Connect(RoleType role, string channelId = null, string channelType = null)
        {
            _client.Connect(_host, _port);
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
            Send(Command(Commands.SConnect, roleType, channelId, channelType));
        }

        /// <summary>
        /// used to relay arbitrary output messages to engine's logging output
        /// </summary>
        public void Log(string message)
        {
            Send(Command(Commands.SOutput) + ':' + message);
        }

        /// <summary>
        /// query local parameter
        /// </summary>
        public string GetLocal(string parameter)
        {
            return SetLocal(parameter, String.Empty);
        }

        /// <summary>
        /// requests the change of a local parameter
        /// </summary>
        public string SetLocal(string parameter, string value)
        {
            var result = Send(Commands.RSetLocal, parameter, Commands.SSetLocal, parameter, value);
            return _serializer.Decode(result[2]);
        }

        /// <summary>
        /// sent by the application to the engine to ask it to process a message.
        /// </summary>
        /// <param name="name">name of the message</param>
        /// <param name="result">default textual return value of the message</param>
        /// <param name="parameter">enumeration of the key-value pairs of the message</param>
        /// <returns></returns>
        public YateMessageResponse SendMessage(string name, string result, params Tuple<string, string>[] parameter)
        {
            string id = Guid.NewGuid().ToString();
            var time = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var stringParams =
                new[] {Commands.SMessage, id, time, name, result}.Concat(parameter.Select(x => _serializer.Encode(x)));
            var response = Send(Commands.RMessage, id, stringParams.ToArray());
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

        public T SendMessage<T>(IYateMessageResponse<T> message)
        {
            var result = SendMessage(message.Name, message.Result, message.Parameters.ToArray());
            return message.ParseResponse(result, _serializer);
        }

        public InstallResult Install(string name, string filterName)
        {
            return Install(null, name, filterName, null);
        }

        public InstallResult Install(string name)
        {
            return Install(null, name, null, null);
        }

        public InstallResult Install(string name, string filterName, string filterValue)
        {
            return Install(null, name, filterName, filterValue);
        }

        public InstallResult Install(int priority, string name, string filterName, string filterValue)
        {
            return Install((int?) priority, name, filterName, filterValue);
        }

        public InstallResult Uninstall(string name)
        {
            var result = Send(Commands.RUninstall, name, Commands.SUninstall, name);
            return new InstallResult(_serializer.Decode(result[1]), _serializer.Decode(result[3]));
        }

        public bool Watch(string name)
        {
            var result = Send(Commands.RWatch, name, Commands.SWatch, name);
            return "true".Equals(_serializer.Decode(result[2]), StringComparison.OrdinalIgnoreCase);
        }

        public bool Watch(string name, Action<YateMessageEventArgs> callback)
        {
            var bag = _watchCallbacks.GetOrAdd(name, new ConcurrentBag<Action<YateMessageEventArgs>>());
            bag.Add(callback);
            var response = Send(Commands.RWatch, name, Commands.SWatch, name);
            return "true".Equals(_serializer.Decode(response[2]), StringComparison.OrdinalIgnoreCase);
        }

        public bool Unwatch(string name)
        {
            var result = Send(Commands.RUnwatch, name, Commands.SUnwatch, name);
            return "true".Equals(_serializer.Decode(result[2]), StringComparison.OrdinalIgnoreCase);
        }

        private InstallResult Install(int? priority, string name, string filterName, string filterValue)
        {
            var result = Send(Commands.RInstall, name, Commands.SInstall, priority?.ToString() ?? String.Empty, name,
                filterName, filterValue);
            return new InstallResult(_serializer.Decode(result[1]), _serializer.Decode(result[3]));
        }

        private string[] Send(string responseCommand, string key, params string[] parameter)
        {
            var response = new YateResponse();
            var eventKey = GetKey(responseCommand, key);
            if (!_eventQueue.TryAdd(eventKey, response))
                throw new ArgumentException("this command is currently pending");
            Send(Command(parameter));
            return response.GetResponse();
        }

        private void Send(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message + '\n');
            _writeLock.Wait();
            try
            {
                var stream = _client.GetStream();
                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}