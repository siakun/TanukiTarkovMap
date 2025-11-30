using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TanukiTarkovMap.Models.Data;

/**
Server - WebSocket 서버 (ASP.NET Core Kestrel 기반)

Purpose: tarkov-market.com 웹사이트와 실시간 양방향 통신을 담당

Core Functionality:
- 서버 시작/종료: Start(), Stop() - 포트 5123에서 WebSocket 수신
- 메시지 브로드캐스트: SendMap, SendPosition, SendFilename 등
- 클라이언트 관리: ConcurrentDictionary로 다중 연결 관리
- 설정 동기화: 웹에서 설정 변경 시 ProcessMessage()로 수신 처리

Message Types (WsMessageType):
- CONFIGURATION: 앱 버전, 게임/스크린샷 폴더 정보
- MAP_CHANGE: 현재 맵 정보 전송
- POSITION_UPDATE: 플레이어 위치 좌표
- SEND_FILENAME: 스크린샷 파일명
- SETTINGS_UPDATE/RESET: 웹에서 설정 변경 요청

Endpoint: ws://0.0.0.0:5123/ws 또는 ws://0.0.0.0:5123/
*/
namespace TanukiTarkovMap.Models.Services
{
    static class Server
    {
        const string WS_URL = "http://0.0.0.0:5123";  // Kestrel will handle WebSocket upgrade

        static volatile bool isClosing = false;
        static IHost? _host = null;
        static readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

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

            // 모든 WebSocket 연결 즉시 종료 (graceful close 생략)
            foreach (var socket in _sockets.Values)
            {
                try
                {
                    socket.Abort();
                    socket.Dispose();
                }
                catch { }
            }
            _sockets.Clear();

            // ASP.NET Core 호스트 빠른 종료
            _host?.StopAsync(TimeSpan.FromMilliseconds(500)).Wait(500);
            _host?.Dispose();
            _host = null;
        }

        public static void Start()
        {
            isClosing = false;

            // ASP.NET Core 서버 시작
            StartServer();

#if DEBUG
            //var posTask = Task.Run(() => SendRandomPosition());
            //await posTask;
#endif
        }

        static void StartServer()
        {
            var builder = WebApplication.CreateBuilder();

            // Kestrel 서버 설정
            builder.WebHost.UseUrls(WS_URL);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AllowSynchronousIO = true;
            });

            // 로깅 설정 (필요시)
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            // WebSocket 서비스 추가
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // CORS 활성화
            app.UseCors();

            // WebSocket 미들웨어 활성화
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120)
            });

            // WebSocket 엔드포인트
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws" || context.Request.Path == "/")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        var socketId = Guid.NewGuid().ToString();

                        _sockets.TryAdd(socketId, webSocket);

                        // 연결 시 설정 전송
                        SendConfigurationToSocket(webSocket);

                        // 메시지 수신 처리
                        await HandleWebSocketAsync(socketId, webSocket);

                        _sockets.TryRemove(socketId, out _);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });

            // 서버 시작
            _host = app;
            Task.Run(() => _host.Run());
        }

        static async Task HandleWebSocketAsync(string socketId, WebSocket webSocket)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);

            while (webSocket.State == WebSocketState.Open && !isClosing)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        ProcessMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            string.Empty, CancellationToken.None);
                        break;
                    }
                }
                catch (WebSocketException)
                {
                    // 연결이 끊어진 경우
                    break;
                }
                catch (Exception)
                {
                    // 기타 오류
                    break;
                }
            }
        }

        static async void SendConfigurationToSocket(WebSocket socket)
        {
            ConfigurationData data = new ConfigurationData()
            {
                MessageType = WsMessageType.CONFIGURATION,
                Version = App.Version,
                GameFolder = App.GameFolder,
                ScreenshotsFolder = App.ScreenshotsFolder,
            };

            await SendDataToSocket(socket, data);
        }

        static async Task SendDataToSocket(WebSocket socket, object data)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    var json = JsonSerializer.Serialize(data);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    var buffer = new ArraySegment<byte>(bytes);

                    await socket.SendAsync(buffer, WebSocketMessageType.Text,
                        endOfMessage: true, CancellationToken.None);
                }
            }
            catch { }
        }

        static void SendData(object data)
        {
            try
            {
                if (_sockets.Count == 0)
                {
                    Utils.Logger.SimpleLog("[WS] No connected clients - skipping send");
                    return;
                }

                var json = JsonSerializer.Serialize(data);
                var bytes = Encoding.UTF8.GetBytes(json);
                var buffer = new ArraySegment<byte>(bytes);

                // 모든 연결된 클라이언트에게 메시지 전송
                var tasks = new List<Task>();
                int openSocketCount = 0;
                foreach (var socket in _sockets.Values.ToList())
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        openSocketCount++;
                        tasks.Add(socket.SendAsync(buffer, WebSocketMessageType.Text,
                            endOfMessage: true, CancellationToken.None));
                    }
                }

                Task.WaitAll(tasks.ToArray(), 1000);

                Utils.Logger.SimpleLog($"[WS] Sent to {openSocketCount} client(s) | Data: {json}");
            }
            catch (Exception ex)
            {
                Utils.Logger.SimpleLog($"[WS Error] SendData: {ex.Message}");
            }
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

        public static void SendMap(string map)
        {
            MapChangeData data = new MapChangeData()
            {
                MessageType = WsMessageType.MAP_CHANGE,
                Map = map,
            };

            // Show in logs parsed map name on Website
            SendData(data);
        }

        public static void SendPosition(Position pos)
        {
            UpdatePositionData posData = new UpdatePositionData()
            {
                MessageType = WsMessageType.POSITION_UPDATE,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
            };
            SendData(posData);
        }

        public static void SendFilename(string filename)
        {
            Utils.Logger.SimpleLog($"[WS] SendFilename: {filename} | Clients: {_sockets.Count}");

            SendFilenameData data = new SendFilenameData()
            {
                MessageType = WsMessageType.SEND_FILENAME,
                Filename = filename,
            };

            SendData(data);
        }

        public static void SendConfiguration()
        {
            ConfigurationData data = new ConfigurationData()
            {
                MessageType = WsMessageType.CONFIGURATION,
                Version = App.Version,
                GameFolder = App.GameFolder,
                ScreenshotsFolder = App.ScreenshotsFolder,
            };

            SendData(data);
        }

        public static void SendQuestUpdate(string questId, string status)
        {
            QuestUpdateData data = new()
            {
                MessageType = WsMessageType.QUEST_UPDATE,
                QuestId = questId,
                Status = status,
            };

            SendData(data);
        }

        static T ParseJson<T>(string json)
        {
            // Deserilize to object
            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception) 
            { 
                return default;
            }
        }

        static void ProcessMessage(string json)
        {
            WsMessage msg = ParseJson<WsMessage>(json);

            if (msg != null && msg.MessageType == WsMessageType.SETTINGS_UPDATE)
            {
                var settings = ParseJson<UpdateSettingsData>(json);

                App.SetSettings(settings, true);
                Settings.Save();
                SendConfiguration();

                Watcher.Restart();
                //App.RestartApp();
            }
            else if (msg != null && msg.MessageType == WsMessageType.SETTINGS_RESET)
            {
                Settings.Delete();
                App.ResetSettings();
                SendConfiguration();

                Watcher.Restart();
            }
        }
    }
}
