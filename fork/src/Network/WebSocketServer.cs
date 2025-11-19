using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Fleck;

namespace TarkovClient
{
    static class Server
    {
        const string WS_URL = "ws://0.0.0.0:5123";

        static volatile bool isClosing = false;
        static WebSocketServer _server = null;
        static readonly ConcurrentDictionary<IWebSocketConnection, bool> _sockets =
            new ConcurrentDictionary<IWebSocketConnection, bool>();

        static Server()
        {
            Application.ApplicationExit += (object sender, EventArgs e) => Stop();
        }

        public static bool CanSend
        {
            get { return _sockets.Count > 0; }
        }

        public static void Stop()
        {
            isClosing = true;

            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
        }

        public static void Start()
        {
            isClosing = false;

            // Server start
            StartServer();

#if DEBUG
            //var posTask = Task.Run(() => SendRandomPosition());
            //await posTask;
#endif
        }

        static void StartServer()
        {
            FleckLog.Level = LogLevel.Debug;
            _server = new WebSocketServer(WS_URL);

            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    _sockets.TryAdd(socket, true);

                    SendConfiguration();
                };
                socket.OnClose = () =>
                {
                    _sockets.TryRemove(socket, out _);
                };
                socket.OnMessage = (msg) =>
                {
                    ProcessMessage(msg);
                };
            });
        }

        static void SendRandomPosition()
        {
            while (!isClosing)
            {
                Random rnd = new Random();

                var fields = typeof(MapName)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(string))
                    .Select(f => (string)f.GetValue(null))
                    .ToArray();
                var map = fields[rnd.Next(fields.Length)];

                // waiting, to be sure messages order
                SendMap(map);
                Thread.Sleep(2000);

                // lab 0,0 position fix
                if (map == MapName.The_Lab)
                {
                    SendPosition(
                        new Position(
                            rnd.Next(10) * 10 - 340,
                            rnd.Next(10) * 10 - 200,
                            rnd.Next(10) * 10
                        )
                    );
                }
                else
                {
                    SendPosition(
                        new Position(
                            rnd.Next(10) * 10 - 0.3f,
                            rnd.Next(10) * 10 + 0.3f,
                            rnd.Next(10) * 10
                        )
                    );
                }

                Thread.Sleep(5000);
            }
        }

        static void SendData(Object data)
        {
            try
            {
                if (_sockets.Count == 0)
                    return;

                var json = JsonSerializer.Serialize(data);

                // Send message to all connected clients
                foreach (var socket in _sockets.Keys.ToList().AsReadOnly())
                {
                    socket.Send(json);
                }
            }
            catch (Exception) { }
        }


        public static void SendMap(string map)
        {
            MapChangeData data = new MapChangeData()
            {
                messageType = WsMessageType.MAP_CHANGE,
                map = map,
            };

            // Show in logs parsed map name on Website
            SendData(data);
        }

        public static void SendPosition(Position pos)
        {
            UpdatePositionData posData = new UpdatePositionData()
            {
                messageType = WsMessageType.POSITION_UPDATE,
                x = pos.X,
                y = pos.Y,
                z = pos.Z,
            };
            SendData(posData);
        }

        public static void SendFilename(string filename)
        {
            SendFilenameData data = new SendFilenameData()
            {
                messageType = WsMessageType.SEND_FILENAME,
                filename = filename,
            };

            SendData(data);
        }

        public static void SendConfiguration()
        {
            ConfigurationData data = new ConfigurationData()
            {
                messageType = WsMessageType.CONFIGURATION,
                version = Env.Version,
                gameFolder = Env.GameFolder,
                screenshotsFolder = Env.ScreenshotsFolder,
            };

            SendData(data);
        }

        public static void SendQuestUpdate(string questId, string status)
        {
            QuestUpdateData data = new QuestUpdateData()
            {
                messageType = WsMessageType.QUEST_UPDATE,
                questId = questId,
                status = status,
            };

            SendData(data);
        }

        static T ParseJson<T>(string json)
        {
            try
            {
                // Deserilize to object
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception) { }
            ; // ignore
            return default(T);
        }

        static void ProcessMessage(string json)
        {
            WsMessage msg = ParseJson<WsMessage>(json);

            if (msg != null && msg.messageType == WsMessageType.SETTINGS_UPDATE)
            {
                var settings = ParseJson<UpdateSettingsData>(json);

                Env.SetSettings(settings, true);
                Settings.Save();
                SendConfiguration();

                Watcher.Restart();
                //Env.RestartApp();
            }
            else if (msg != null && msg.messageType == WsMessageType.SETTINGS_RESET)
            {
                Settings.Delete();
                Env.ResetSettings();
                SendConfiguration();

                Watcher.Restart();
                //Env.RestartApp();
            }
        }
    }
}
