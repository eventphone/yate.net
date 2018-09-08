/*
 * Yate Status Monitor
 * - display active channels and important messages in real-time
 *   with a curses-like interface -
 *
 * Author: Ben Fuhrmannek <bef@eventphone.de>
 * Date: 2013-12-03
 *
 * Copyright (c) 2013-2014, Ben Fuhrmannek
 * All rights reserved.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eventphone.yate;
using eventphone.yate.Messages;
using McMaster.Extensions.CommandLineUtils;
using TrueColorConsole;

namespace eventphone.ystatus
{
    [VersionOptionFromMember(MemberName = nameof(Version))]
    class Program
    {
        public string Version
        {
            get { return "0.0.1"; }
        }

        [Option(ShortName = "H", Description = "connect to host")]
        public string Host { get; set; } = "localhost";

        [Option(ShortName = "P", Description = "connect to port")]
        public ushort Port { get; set; } = 5039;
        
        [Option(ShortName = "cd", Description = "channel remove delay")]
        public uint ChannelDelay { get; set; } = 6000;

        [Option(ShortName = "fd", Description = "flashmessage delay")]
        public uint FlashDelay { get; set; } = 8000;
        
        static void Main(string[] args)
        {
            CommandLineApplication.Execute<Program>(args);
        }

        public Program()
        {
            ChannelData = new ConcurrentDictionary<string, IDictionary<string, string>>();
            FlashMessages = new ConcurrentDictionary<Guid, Tuple<string, string>>();
        }

        private void OnExecute()
        {
            VTConsole.Enable();
            VTConsole.SetWindowTitle("Yate Status Monitor");
            UpdateDisplay();
            using (var resetEvent = new ManualResetEventSlim())
            using (var client = new YateClient(Host, Port))
            {
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    resetEvent.Set();
                };
                client.Connect(RoleType.Global);
                client.Log($"start for {Environment.UserName}");
                client.Watch("chan.startup", ChanUpdate);
                client.Watch("chan.hangup", ChanHangup);
                client.Watch("call.ringing", ChanUpdate);
                client.Watch("call.answered", ChanUpdate);
                client.Watch("chan.disconnected", ChanDisconnected);
                client.Watch("call.update", ChanUpdate);
                client.Watch("user.auth", UserAuth);
                client.Watch("user.register", UserRegister);
                client.Watch("user.unregister", UserUnregister);
                try
                {
                    UpdateFromStatus(client);
                }
                catch (Exception ex)
                {
                    FlashMessage("error", $"engine.status failed: {ex.Message}");
                }
                resetEvent.Wait();
                Cleanup();
            }
        }

        private void UpdateFromStatus(YateClient client)
        {
            var status = client.SendMessage(new EngineStatusSip());
            if (status.Name != "sip") return;
            foreach (var detail in status.Details)
            {
                var data = new Dictionary<string, string>
                {
                    {"status", GetValueOrDefault(detail, "Status")},
                    {"address", GetValueOrDefault(detail, "Address")},
                    {"peerid", GetValueOrDefault(detail, "Peer")}
                };
                UpdateChan(GetValueOrDefault(detail, "id"), data);
            }
            UpdateDisplay();
        }

        private void UserUnregister(YateMessageEventArgs arg)
        {
            if (arg.GetParameter("username") != null)
            {
                FlashMessage("info", $"unregistered user {arg.GetParameter("username")} {arg.GetParameter("data", "?")} / {arg.GetParameter("device", "?")}");
            }
        }

        private void UserRegister(YateMessageEventArgs arg)
        {
            FlashMessage("info", $"registered user {arg.GetParameter("username", "?")} {arg.GetParameter("data", "?")} / {arg.GetParameter("device", "?")}");
        }

        private void UserAuth(YateMessageEventArgs arg)
        {
            if (!arg.Handled && arg.GetParameter("response") != null)
            {
                FlashMessage("warning", $"auth failed: {arg.GetParameter("username", "?")}@{arg.GetParameter("realm", "?")} / {arg.GetParameter("address", "?")} / {arg.GetParameter("device", "?")}");
            }
        }

        private void ChanDisconnected(YateMessageEventArgs arg)
        {
            arg.Parameter.Add("ysm_status", "disconnected");
            var id = arg.GetParameter("id");
            UpdateChan(id, arg.Parameter);
            RemoveChan(id);
        }

        private void ChanHangup(YateMessageEventArgs arg)
        {
            arg.Parameter.Add("ysm_status", "hungup");
            var id = arg.GetParameter("id");
            UpdateChan(id, arg.Parameter);
            RemoveChan(id);
        }

        private void RemoveChan(string id)
        {
            Task.Delay(TimeSpan.FromMilliseconds(ChannelDelay))
                .ContinueWith(_ =>
                {
                    ChannelData.TryRemove(id, out var _);
                    UpdateDisplay();
                });
        }

        private void ChanUpdate(YateMessageEventArgs arg)
        {
            arg.Parameter.Add("ysm_status", arg.GetParameter("status", String.Empty));
            UpdateChan(arg.GetParameter("id"), arg.Parameter);
        }

        private void UpdateChan(string id, IDictionary<string, string> values)
        {
            ChannelData.AddOrUpdate(id, values, (_, e) => Merge(e, values));
            UpdateDisplay();
        }

        private void FlashMessage(string level, string message)
        {
            var id = Guid.NewGuid();
            FlashMessages.AddOrUpdate(id, new Tuple<string, string>(level, message), (_, e) => new Tuple<string, string>(level, message));
            Task.Delay(TimeSpan.FromMilliseconds(FlashDelay)).ContinueWith(_ =>
            {
                FlashMessages.TryRemove(id, out var _);
                UpdateDisplay();
            });
            UpdateDisplay();
        }

        private string GetValueOrDefault(IDictionary<string, string> dict, string key, string defaultValue = "?")
        {
            if (dict.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        private IDictionary<TKey, TValue> Merge<TKey, TValue>(IDictionary<TKey, TValue> target, IDictionary<TKey, TValue> add)
        {
            foreach (var value in add)
            {
                if (target.ContainsKey(value.Key))
                    target[value.Key] = value.Value;
                else
                    target.Add(value.Key, value.Value);
            }
            return target;
        }

        private ConcurrentDictionary<string, IDictionary<string,string>> ChannelData { get; }
        private ConcurrentDictionary<Guid, Tuple<string, string>> FlashMessages { get; }

        private void UpdateDisplay()
        {
            WriteHeader();
            if (ChannelData.Count == 0)
            {
                WriteEmptyNotice();
            }
            foreach (var entry in ChannelData.OrderBy(x=>x.Key))
            {
                var id = entry.Key;
                var kv = entry.Value;
                string inout;
                switch (GetValueOrDefault(kv, "direction"))
                {
                    case "incoming":
                        inout = "-[in]->";
                        break;
                    case "outgoing":
                        inout = "<-[out]";
                        break;
                    default:
                        inout = "<-[?]->";
                        break;
                }
                var ystatus = GetValueOrDefault(kv, "ysm_status");
                if (ystatus == "hungup" && GetValueOrDefault(kv, "status") != "answered")
                    ystatus = GetValueOrDefault(kv, "status");
                switch (ystatus)
                {
                    case "rejected":
                        VTConsole.SetColorForeground(Color.Yellow);
                        VTConsole.SetColorBackground(Color.Red);
                        break;
                    case "ringing":
                        VTConsole.SetColorForeground(Color.Yellow);
                        VTConsole.SetColorBackground(Color.Black);
                        break;
                    case "answered":
                        VTConsole.SetColorForeground(Color.Green);
                        VTConsole.SetColorBackground(Color.Black);
                        break;
                    case "hungup":
                        VTConsole.SetColorForeground(Color.White);
                        VTConsole.SetColorBackground(Color.Blue);
                        break;
                    default:
                        VTConsole.SetColorForeground(Color.White);
                        VTConsole.SetColorBackground(Color.Black);
                        break;
                }
                var status = ystatus;
                if (kv.ContainsKey("reason"))
                    status += ": " + kv["reason"];
                if (kv.ContainsKey("cause_sip"))
                    status += $" SIP {kv["cause_sip"]}/{GetValueOrDefault(kv, "reason_sip")}";
                Console.WriteLine($"{id,-10} {inout} {GetValueOrDefault(kv, "caller"),10} -> {GetValueOrDefault(kv, "called"),-10} | {GetValueOrDefault(kv, "address")} {status}");
                VTConsole.EraseInLine();
            }
            foreach (var value in FlashMessages.Values)
            {
                var level = value.Item1;
                var msg = value.Item2;
                switch (level)
                {
                    case "info":
                        VTConsole.SetColorForeground(Color.Yellow);
                        VTConsole.SetColorBackground(Color.Black);
                        break;
                    case "notice":
                        VTConsole.SetColorForeground(Color.Black);
                        VTConsole.SetColorBackground(Color.Cyan);
                        break;
                    case "warning":
                        VTConsole.SetColorForeground(Color.Black);
                        VTConsole.SetColorBackground(Color.Red);
                        break;
                    case "error":
                        VTConsole.SetColorForeground(Color.Red);
                        VTConsole.SetColorBackground(Color.Black);
                        break;
                    default:
                        VTConsole.SetColorForeground(Color.Cyan);
                        VTConsole.SetColorBackground(Color.Black);
                        break;
                }
                Console.WriteLine($"[{level}] {msg}");
            }
        }

        private void WriteHeader()
        {
            VTConsole.SetColorBackground(Color.Black);
            VTConsole.EraseInDisplay(VTEraseMode.Entirely);
            VTConsole.WriteConcat("\x1b", "[1m");
            VTConsole.WriteLine($"Yate Status Monitor {Version} - (c) zivillian <eventphone@zivillian.de>", Color.Yellow, Color.Blue);
            VTConsole.EraseInLine();
            VTConsole.Write("\x1b[22m",Color.White, Color.Black);
        }

        private void WriteEmptyNotice()
        {
            VTConsole.WriteLine("no channel active", Color.Cyan);
            VTConsole.SetColorForeground(Color.White);
        }

        private void Cleanup()
        {
            VTConsole.EraseInDisplay(VTEraseMode.Entirely);
            VTConsole.SoftReset();
            Console.WriteLine("bye.");
        }
    }
}