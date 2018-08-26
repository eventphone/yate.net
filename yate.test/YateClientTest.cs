using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using yate;

namespace eventphone.yate.test
{
    public class YateClientTest : IDisposable
    {
        private readonly YateClient _client;
        private readonly YateClient _testClient;
        private readonly TestServer _server;

        public YateClientTest()
        {
            _client = new YateClient("localhost", 5039);
            _testClient = new YateClient("localhost", 55039);
            _server = new TestServer(55039);
        }

        [Fact]
        public async Task CanConnect()
        {
            await _client.ConnectAsync(RoleType.Global, CancellationToken.None);
        }

        [Fact]
        public async Task CanLog()
        {
            await CanConnect();
            await _client.LogAsync("Foo", CancellationToken.None);
        }

        [Fact]
        public async Task CanGetLocal()
        {
            await CanConnect();
            var response = await _client.GetLocalAsync("engine.cfgsuffix", CancellationToken.None);
            Assert.Equal(".conf", response);
        }

        [Fact]
        public async Task CanSendMessage()
        {
            await CanConnect();
            var response = await _client.SendMessageAsync("zwerg.on", "ok", CancellationToken.None);
            Assert.Equal("ok", response.Result);
            Assert.True(response.Handled);
        }

        [Fact]
        public async Task CanAckUnhandledMessage()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            _server.SendMessage("%%>message:123:321:test:false:name=value");
            _server.AckMessage("%%<message:123:false:test:false");
        }

        [Fact]
        public async Task CanAckDespiteException()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            _testClient.MessageReceived += (s, e) => throw new NotImplementedException("foo");
            _server.SendMessage("%%>message:123:321:test:false:name=value");
            _server.AckMessage("%%>output:foo");
            _server.AckMessage("%%<message:123:false:test:false");
        }

        public void Dispose()
        {
            _client.Dispose();
            _testClient.Dispose();
        }
    }

        public class TestServer:IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task<TcpClient> _clientTask;

        public TestServer(ushort port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _clientTask = _listener.AcceptTcpClientAsync();
        }

        public void ReplyToMessage(string expected, string response)
        {
            AckMessage(expected);
            SendMessage(response);
        }

        public void SendMessage(string message)
        {
            var client = _clientTask.Result;
            var stream = client.GetStream();
            stream.Write(Encoding.ASCII.GetBytes(message));
            stream.Write(new byte[] { 10 });
            stream.Flush();
        }

        public void AckMessage(string expected)
        {
            var client = _clientTask.Result;
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII);
            var message = reader.ReadLine();
            Assert.Equal(expected, message);
        }

        public void AckConnect()
        {
            AckMessage("%%>connect:global");
        }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}
