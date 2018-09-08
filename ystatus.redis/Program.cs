﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using eventphone.yate;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace ystatus.redis
{
    class Program:IDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly YateClient _yate;
        private static string RedisPrefix = "yate:ystatus:";

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            var configuration = builder.Build();
            RedisPrefix += Environment.MachineName + ':';
            using (var program = new Program(configuration))
            {
                program.WaitForExit();
            }
        }

        private Program(IConfigurationRoot configuration)
        {
            var redis = configuration.GetConnectionString("redis");
            _redis = ConnectionMultiplexer.Connect(redis, Console.Out);
            Clear();

            var yateConfig = configuration.GetSection("Yate");
            _yate = new YateClient(yateConfig["host"], yateConfig.GetValue<ushort>("port"));
            _yate.Connect();

            _yate.Watch("chan.startup", ChanUpdate);
            _yate.Watch("chan.hangup", ChanHangup);
            _yate.Watch("call.ringing", ChanUpdate);
            _yate.Watch("call.answered", ChanUpdate);
            _yate.Watch("chan.disconnected", ChanDisconnected);
            _yate.Watch("call.update", ChanUpdate);
            _yate.Watch("user.auth", UserAuth);
            _yate.Watch("user.register", UserRegister);
            _yate.Watch("user.unregister", UserUnregister);
        }

        private void ChanUpdate(YateMessageEventArgs arg)
        {
            var id = arg.GetParameter("id");
            var values = GetHash(arg);
            values.Add(new HashEntry("ysm_status", arg.GetParameter("status", String.Empty)));
            UpdateRedis(RedisPrefix + id, values, TimeSpan.FromHours(1));
        }

        private void ChanHangup(YateMessageEventArgs arg)
        {
            var id = arg.GetParameter("id");
            var values = GetHash(arg);
            values.Add(new HashEntry("ysm_status", "hungup"));
            UpdateRedis(RedisPrefix + id, values, TimeSpan.FromSeconds(6));
        }

        private void ChanDisconnected(YateMessageEventArgs arg)
        {
            var id = arg.GetParameter("id");
            var values = GetHash(arg);
            values.Add(new HashEntry("ysm_status", "disconnected"));
            UpdateRedis(RedisPrefix + id, values, TimeSpan.FromSeconds(6));
        }

        private void UserAuth(YateMessageEventArgs arg)
        {
            if (!arg.Handled && arg.GetParameter("response") != null)
            {
                var message = $"auth failed: {arg.GetParameter("username", "?")}@{arg.GetParameter("realm", "?")} / {arg.GetParameter("address", "?")} / {arg.GetParameter("device", "?")}";
                FlashMessage("warning", message);
            }
        }

        private void UserRegister(YateMessageEventArgs arg)
        {
            var message = $"registered user {arg.GetParameter("username", "?")} {arg.GetParameter("data", "?")} / {arg.GetParameter("device", "?")}";
            FlashMessage("info", message);
        }

        private void UserUnregister(YateMessageEventArgs arg)
        {
            var message = $"unregistered user {arg.GetParameter("username")} {arg.GetParameter("data", "?")} / {arg.GetParameter("device", "?")}";
            FlashMessage("info", message);
        }

        private void FlashMessage(string level, string message)
        {
            var key = RedisPrefix + level + ":" + Guid.NewGuid();
            var redis = _redis.GetDatabase();
            redis.StringSet(key, message, TimeSpan.FromSeconds(8), flags: CommandFlags.FireAndForget);
        }

        private void UpdateRedis(RedisKey key, IEnumerable<HashEntry> values, TimeSpan expire)
        {
            var redis = _redis.GetDatabase();
            redis.HashSet(key, values.Where(x=>!x.Value.IsNull).ToArray(), CommandFlags.FireAndForget);
            redis.KeyExpire(key, expire, CommandFlags.FireAndForget);
        }

        private void Clear()
        {
            var endpoints = _redis.GetEndPoints();
            foreach (var endPoint in endpoints)
            {
                var server = _redis.GetServer(endPoint);
                var keys = server.Keys(pattern: RedisPrefix + '*');
                var redis = _redis.GetDatabase();
                foreach (var key in keys)
                {
                    redis.KeyDelete(key, CommandFlags.FireAndForget);
                }
            }
        }

        private List<HashEntry> GetHash(YateMessageEventArgs arg)
        {
            var values = new List<HashEntry>
            {
                new HashEntry("direction", arg.GetParameter("direction")),
                new HashEntry("status", arg.GetParameter("status")),
                new HashEntry("reason_sip", arg.GetParameter("reason_sip")),
                new HashEntry("caller", arg.GetParameter("caller")),
                new HashEntry("called", arg.GetParameter("called")),
                new HashEntry("address", arg.GetParameter("address")),
                new HashEntry("reason", arg.GetParameter("reason")),
                new HashEntry("cause_sip", arg.GetParameter("cause_sip")),
            };
            return values;
        }

        private void WaitForExit()
        {
            var resetEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                resetEvent.Set();
            };
            resetEvent.WaitOne();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _redis.Dispose();
                _yate.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}