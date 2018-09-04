using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using eventphone.yate.Messages;

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

        [Fact]
        public async Task CanInstall()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var install = _testClient.InstallAsync("test", CancellationToken.None);
            _server.ReplyToMessage("%%>install::test", "%%<install:100:test:true");
            var result = await install;
            Assert.Equal(100, result.Priority);
            Assert.True(result.Success);
            install = _testClient.InstallAsync("test", CancellationToken.None);
            _server.ReplyToMessage("%%>install::test", "%%<install:100:test:false");
            result = await install;
            Assert.False(result.Success);
        }

        [Fact]
        public async Task CanUninstall()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var install = _testClient.UninstallAsync("test", CancellationToken.None);
            _server.ReplyToMessage("%%>uninstall:test", "%%<uninstall:100:test:true");
            var result = await install;
            Assert.Equal(100, result.Priority);
            Assert.True(result.Success);
            install = _testClient.UninstallAsync("test", CancellationToken.None);
            _server.ReplyToMessage("%%>uninstall:test", "%%<uninstall:100:test:false");
            result = await install;
            Assert.False(result.Success);
        }

        [Fact]
        public async Task CanGetLocal()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var getlocal = _testClient.GetLocalAsync("engine.cfgsuffix", CancellationToken.None);
            _server.ReplyToMessage("%%>setlocal:engine.cfgsuffix:", "%%<setlocal:engine.cfgsuffix:.conf:true");
            var result = await getlocal;
            Assert.Equal(".conf", result);
        }

        [Fact]
        public async Task CanSetLocal()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var setlocal = _testClient.SetLocalAsync("id", "test", CancellationToken.None);
            _server.ReplyToMessage("%%>setlocal:id:test", "%%<setlocal:id:test:true");
            var result = await setlocal;
            Assert.Equal("test", result);
        }

        [Fact]
        public async Task CanWatch()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var watch = _testClient.WatchAsync("test", CancellationToken.None);
            _server.ReplyToMessage("%%>watch:test", "%%<watch:test:true");
            var result = await watch;
            Assert.True(result);
        }

        [Fact]
        public async Task CanWatchWithCallback()
        {
            var invoked = false;
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var resetEvent = new AutoResetEvent(false);
            _testClient.Watch += (s, e) => { resetEvent.Set(); };
            var watch = _testClient.WatchAsync("test", Callback, CancellationToken.None);
            _server.ReplyToMessage("%%>watch:test", "%%<watch:test:true");
            var result = await watch;
            Assert.True(result);
            _server.SendMessage("%%<message:123:321:foo:false");
            resetEvent.WaitOne();
            Assert.False(invoked);
            _server.SendMessage("%%<message:123:321:test:false");
            resetEvent.WaitOne();
            Assert.True(invoked);
            
            void Callback(YateMessageEventArgs e)
            {
                Assert.Equal("test", e.Name);
                invoked = true;
            }
        }

        [Fact]
        public async Task CanUnwatch()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var getlocal = _testClient.UnwatchAsync("test", CancellationToken.None);
            _server.ReplyToMessage("%%>unwatch:test", "%%<unwatch:test:true");
            var result = await getlocal;
            Assert.True(result);
        }

        [Fact]
        public async Task WatchMessageRaisesEvent()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var resetEvent = new ManualResetEventSlim(false);
            _testClient.Watch += (s, e) => { resetEvent.Set(); };
            _server.SendMessage("%%<message::true:call.execute::driver=dumb");
            resetEvent.Wait();
        }

        [Fact]
        public async Task CanGetSipEngineStatus()
        {
            await _testClient.ConnectAsync(RoleType.Global, CancellationToken.None);
            _server.AckConnect();
            var result = _testClient.SendMessageAsync(new EngineStatusSip(), CancellationToken.None);
            string id = null;
            bool Validate(string message)
            {
                Assert.StartsWith("%%>message:", message);
                id = message.Substring(11, message.IndexOf(':', 11) - 11);
                Assert.Contains(":engine.status:", message);
                return true;
            }
            _server.ReplyToMessage(Validate, ()=>$"%%<message:{id}:false:engine.status" +
            ":name=sip,type=varchans,format=Status|Address|Peer;routed=386,routing=0,total=386,chans=3,transactions=0;sip/384=answered|172.24.24.4%z5060|ExtModule,sip/385=answered|172.24.24.3%z5060|ExtModule,sip/386=answered|172.24.24.6%z5060|ExtModule%M%J" +
            ":module=sip" +
            ":handlers=engine%z90,cdrbuild%z100,moh%z100,callgen%z100,isaccodec%z100,cdrcombine%z100,mysqldb%z110,openssl%z110,mux%z110,wave%z110,callfork%z110,tonedetect%z110,stun%z110,tone%z110,dumb%z110,yrtp%z110,conf%z110,extmodule%z110,regexroute%z110,sip%z110,pbx%z110,queues%z110,register%z110,monitoring%z110,park%z110,queuesnotify%z110,snmpagent%z110");
            var response = await result;
            Assert.Equal(3, response.Details.Count);
            var last = response.Details.Last();
            Assert.Equal("sip/386=answered", last["Status"]);
            Assert.Equal("172.24.24.6:5060", last["Address"]);
            Assert.Equal("ExtModule", last["Peer"]);
        }

        public void Dispose()
        {
            _client.Dispose();
            _testClient.Dispose();
            _server.Dispose();
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

        public void ReplyToMessage(Func<string,bool> expected, Func<string> response)
        {
            AckMessage(expected);
            SendMessage(response);
        }

        public void SendMessage(string message)
        {
            SendMessage(() => message);
        }
        public void SendMessage(Func<string> message)
        {
            var client = _clientTask.Result;
            var stream = client.GetStream();
            stream.Write(Encoding.UTF8.GetBytes(message()));
            stream.Write(new byte[] { 10 });
            stream.Flush();
        }

        public void AckMessage(string expected)
        {
            AckMessage(x => expected == x);
        }

        public void AckMessage(Func<string,bool> expected)
        {
            var client = _clientTask.Result;
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var message = reader.ReadLine();
            Assert.True(expected(message));
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
