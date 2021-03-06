﻿using System;
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
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var tasks = new List<Task> {cancelTask};
            using (var reader = new StreamReader(InputStream, Encoding.UTF8))
            while (!_isclosed)
            {
                try
                {
                    var responseTask = reader.ReadLineAsync();
                    tasks.Add(responseTask);
                    var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
                    while (completed != responseTask)
                    {
                        if (completed.Status != TaskStatus.RanToCompletion)
                        {
                            if (completed == cancelTask)
                            {
                                await completed;
                            }
                            else
                            {
                                try
                                {
                                    await completed;
                                }
                                catch (Exception ex)
                                {
                                    tasks.Add(LogAsync(ex.Message, cancellationToken));
                                }
                            }
                        }
                        tasks.Remove(completed);
                        completed = await Task.WhenAny(tasks).ConfigureAwait(false);
                    }
                    var response = await responseTask;
                    if (response == null) break;
                    var parts = response.Split(':');
                    parts[0] = _serializer.Decode(parts[0]);
                    switch (parts[0])
                    {
                        case YateConstants.RSetLocal:
                            parts[1] = _serializer.Decode(parts[1]);
                            ProcessResponse(YateConstants.RSetLocal, parts[1], parts);
                            break;
                        case YateConstants.RError:
                            OnError(parts[1]);
                            break;
                        case YateConstants.RMessage:
                            parts[1] = _serializer.Decode(parts[1]);
                            if (!ProcessResponse(YateConstants.RMessage, parts[1], parts))
                            {
                                var message = GetYateRMessageEventArgs(parts);
                                OnWatch(message);
                            }
                            break;
                        case YateConstants.SMessage:
                            var arg = GetYateSMessageEventArgs(parts);
                            tasks.Add(OnMessageReceived(arg, cancellationToken));
                            break;
                        case YateConstants.RInstall:
                            parts[2] = _serializer.Decode(parts[2]);
                            ProcessResponse(YateConstants.RInstall, parts[2], parts);
                            break;
                        case YateConstants.RUninstall:
                            parts[2] = _serializer.Decode(parts[2]);
                            ProcessResponse(YateConstants.RUninstall, parts[2], parts);
                            break;
                        case YateConstants.RWatch:
                            parts[1] = _serializer.Decode(parts[1]);
                            ProcessResponse(YateConstants.RWatch, parts[1], parts);
                            break;
                        case YateConstants.RUnwatch:
                            parts[1] = _serializer.Decode(parts[1]);
                            ProcessResponse(YateConstants.RUnwatch, parts[1], parts);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                catch (IOException ex)
                {
                    var socketExept = ex.InnerException as SocketException;
                    if (socketExept == null || (socketExept.ErrorCode != 10004 && socketExept.ErrorCode != 995))
                    {
                        OnDisconnected();
                        throw;
                    }
                }
            }
            OnDisconnected();
        }

        private YateMessageEventArgs GetYateRMessageEventArgs(string[] parts)
        {
            var resultParams = GetMessageParameter(parts);
            //%%<message:<id>:<processed>:[<name>]:<retvalue>[:<key>=<value>...]
            return new YateMessageEventArgs(_serializer.Decode(parts[1]), YateConstants.True.Equals(_serializer.Decode(parts[2]), StringComparison.Ordinal), _serializer.Decode(parts[3]), _serializer.Decode(parts[4]), resultParams);
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

        protected virtual async Task OnMessageReceived(YateMessageEventArgs message, CancellationToken cancellationToken)
        {
            var handler = MessageReceivedAsync;
            if (handler != null)
            try
            {
                await handler(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogAsync(ex.Message, cancellationToken).ConfigureAwait(false);
            }
            var commands = new List<string>
            {
                YateConstants.RMessage,
                message.Id,
                message.Handled ? YateConstants.True : YateConstants.False,
                message.Name,
                message.Result
            };
            if (handler != null)
            {
                commands.AddRange(message.NewParameter.Select(x => _serializer.Encode(x)));
            }
            var response = Command(commands.ToArray());
            await SendAsync(response, cancellationToken).ConfigureAwait(false);
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

        public Func<YateMessageEventArgs, Task<bool>> MessageReceivedAsync;
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