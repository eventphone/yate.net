using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eventphone.yate
{
    public partial class YateClient
    {
        private void Read()
        {
            using (var reader = new StreamReader(_client.GetStream(), Encoding.UTF8))
            while (!_isclosed)
            {
                try
                {
                    var response = reader.ReadLine();
                    if (response == null) break;
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
                            if (!ProcessResponse(Commands.RMessage, parts[1], parts))
                            {
                                var message = GetYateRMessageEventArgs(parts);
                                OnWatch(message);
                            }
                            break;
                        case Commands.SMessage:
                            var arg = GetYateSMessageEventArgs(parts);
                            OnMessageReceived(arg);
                            break;
                        case Commands.RInstall:
                            parts[2] = _serializer.Decode(parts[2]);
                            ProcessResponse(Commands.RInstall, parts[2], parts);
                            break;
                        case Commands.RUninstall:
                            parts[2] = _serializer.Decode(parts[2]);
                            ProcessResponse(Commands.RUninstall, parts[2], parts);
                            break;
                        case Commands.RWatch:
                            parts[1] = _serializer.Decode(parts[1]);
                            ProcessResponse(Commands.RWatch, parts[1], parts);
                            break;
                        case Commands.RUnwatch:
                            parts[1] = _serializer.Decode(parts[1]);
                            ProcessResponse(Commands.RUnwatch, parts[1], parts);
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
            OnDisconnected();
        }

        private YateMessageEventArgs GetYateRMessageEventArgs(string[] parts)
        {
            var resultParams = GetMessageParameter(parts);
            //%%<message:<id>:<processed>:[<name>]:<retvalue>[:<key>=<value>...]
            return new YateMessageEventArgs(_serializer.Decode(parts[1]), _serializer.Decode(parts[2])=="true", _serializer.Decode(parts[3]), _serializer.Decode(parts[4]), resultParams);
        }

        private YateMessageEventArgs GetYateSMessageEventArgs(string[] parts)
        {
            var resultParams = GetMessageParameter(parts);
            //%%>message:<id>:<time>:<name>:<retvalue>[:<key>=<value>...]
            return new YateMessageEventArgs(_serializer.Decode(parts[1]), _serializer.Decode(parts[2]), _serializer.Decode(parts[3]), _serializer.Decode(parts[4]), resultParams);
        }

        private Dictionary<string, string> GetMessageParameter(string[] parts)
        {
            var resultParams = new Dictionary<string, string>();
            for (int i = 5; i < parts.Length; i++)
            {
                var parameter = _serializer.DecodeParameter(parts[i]);
                resultParams.Add(parameter.Item1, parameter.Item2);
            }
            return resultParams;
        }

        protected virtual void OnMessageReceived(YateMessageEventArgs message)
        {
            var handler = MessageReceived;
            try
            {
                handler?.Invoke(this, message);
            }
            catch (Exception ex)
            {
                LogAsync(ex.Message, CancellationToken.None).GetAwaiter().GetResult();
            }
            var commands = new List<string>
            {
                Commands.RMessage,
                message.Id,
                message.Handled ? "true" : "false",
                message.Name,
                message.Result
            };
            commands.AddRange(message.NewParameter.Select(x => _serializer.Encode(x)));
            var response = Command(commands.ToArray());
            SendAsync(response, CancellationToken.None).GetAwaiter().GetResult();
        }

        protected virtual void OnWatch(YateMessageEventArgs message)
        {
            var handler = Watched;
            try
            {
                handler?.Invoke(this, message);
            }
            catch (Exception ex)
            {
                LogAsync(ex.Message, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private void InvokeWatchCallbacks(object sender, YateMessageEventArgs e)
        {
            if (_watchCallbacks.TryGetValue(e.Name, out var bag))
            {
                foreach (var callback in bag)
                {
                    callback(e);
                }
            }
        }

        public event EventHandler<YateMessageEventArgs> MessageReceived;
        public event EventHandler<YateMessageEventArgs> Watched;
        public event EventHandler Disconnected;

        private bool ProcessResponse(string command, string key, string[] values)
        {
            if (_eventQueue.TryRemove(GetKey(command, key), out var response))
            {
                response.Values = values;
                return true;
            }
            return false;
        }

        private void OnError(string error)
        {
            foreach (var value in _eventQueue.Values)
            {
                value.Error = error;
            }
        }

        private class YateResponse : IDisposable
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
                get { return _values; }
                set
                {
                    _values = value;
                    _resetEvent.Release();
                }
            }

            public string Error
            {
                get { return _error; }
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

            public string[] GetResponse()
            {
                _resetEvent.Wait();
                if (_error != null)
                    throw new YateException(Error);
                return Values;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _resetEvent?.Dispose();
                }
            }
        }
    }
}