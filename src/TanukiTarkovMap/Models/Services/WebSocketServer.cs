using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Fleck;
using TanukiTarkovMap.Models.Data;

namespace TanukiTarkovMap.Models.Services
{
    static class Server
    {
        const string WS_URL = "ws://0.0.0.0:5123";

        static volatile bool isClosing = false;
        static WebSocketServer _server = null;
        static readonly ConcurrentDictionary<IWebSocketConnection, bool> _sockets = new();

        static Server()
        {
            if (Application.Current != null)
            {
                Application.Current.Exit += (object sender, ExitEventArgs e) => Stop();
            }
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
                // WebSocketServer doesn't have Dispose, just nullify
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
                MssageType = WsMessageType.MAP_CHANGE,
                Map = map,
            };

            // Show in logs parsed map name on Website
            SendData(data);
        }

        public static void SendPosition(Position pos)
        {
            UpdatePositionData posData = new UpdatePositionData()
            {
                MssageType = WsMessageType.POSITION_UPDATE,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
            };
            SendData(posData);
        }

        public static void SendFilename(string filename)
        {
            SendFilenameData data = new SendFilenameData()
            {
                MssageType = WsMessageType.SEND_FILENAME,
                Filename = filename,
            };

            SendData(data);
        }

        public static void SendConfiguration()
        {
            ConfigurationData data = new ConfigurationData()
            {
                MssageType = WsMessageType.CONFIGURATION,
                Version = Env.Version,
                GameFolder = Env.GameFolder,
                ScreenshotsFolder = Env.ScreenshotsFolder,
            };

            SendData(data);
        }

        public static void SendQuestUpdate(string questId, string status)
        {
            QuestUpdateData data = new QuestUpdateData()
            {
                MssageType = WsMessageType.QUEST_UPDATE,
                QuestId = questId,
                Status = status,
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

            if (msg != null && msg.MssageType == WsMessageType.SETTINGS_UPDATE)
            {
                var settings = ParseJson<UpdateSettingsData>(json);

                Env.SetSettings(settings, true);
                Settings.Save();
                SendConfiguration();

                Watcher.Restart();
                //Env.RestartApp();
            }
            else if (msg != null && msg.MssageType == WsMessageType.SETTINGS_RESET)
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
