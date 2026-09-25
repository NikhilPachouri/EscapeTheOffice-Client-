using System;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Net
{
    // Plain WebSocket + JSON envelope. With no code it asks the server to create a room and learns
    // the code from `assigned`. Reconnects with the room code and token for as long as the server
    // holds the slot (2 minutes), then gives up.
    public class GameConnection : MonoBehaviour, IServerLink
    {
        const float RetryInterval = 2f;
        const float GracePeriod = 120f;

        public event Action<string, JToken> MessageReceived;
        public event Action<string> StatusChanged;

        public string Code { get; private set; }
        public string Token { get; private set; }
        public bool IsOnline => ws != null && ws.State == WebSocketState.Open;
        // Increments on every successful (re)connect, so a `world` on a new session can be told
        // apart from a hot reload on the same one.
        public int Session { get; private set; }
        // Set once the server has closed the game for good (game_complete) so we stop reconnecting.
        public bool Finished { get; set; }

        WebSocket ws;
        string url;
        bool wantConnected;
        bool connecting;
        float retryAt = -1f;
        float lostAt = -1f;

        // code == null: create a new room. Otherwise join (or, with a token, rejoin) that room.
        public void Connect(string serverUrl, string code, string token = null)
        {
            url = serverUrl;
            Code = string.IsNullOrEmpty(code) ? null : code;
            Token = string.IsNullOrEmpty(token) ? null : token;
            wantConnected = true;
            Finished = false;
            lostAt = -1f;
            Open();
        }

        async void Open()
        {
            if (connecting) return;
            connecting = true;
            retryAt = -1f;
            StatusChanged?.Invoke(lostAt < 0 ? "Connecting…" : "Reconnecting…");

            var socket = new WebSocket(url);
            ws = socket;
            socket.OnOpen += () =>
            {
                connecting = false;
                lostAt = -1f;
                Session++;
                StatusChanged?.Invoke("Connected");
                if (Code == null) Send(MsgType.Create, null);
                else Send(MsgType.Join, new JoinData { Code = Code, Token = Token });
            };
            socket.OnMessage += bytes => Receive(Encoding.UTF8.GetString(bytes));
            socket.OnError += err => Debug.LogWarning($"[net] socket error: {err}");
            socket.OnClose += closeCode =>
            {
                if (ws != socket) return;
                connecting = false;
                ConnectionLost($"Disconnected ({closeCode})");
            };

            try
            {
                // NativeWebSocket's Connect() only returns once the socket closes.
                await socket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[net] connect failed: {e.Message}");
                if (ws == socket)
                {
                    connecting = false;
                    ConnectionLost("Connection failed");
                }
            }
        }

        void ConnectionLost(string reason)
        {
            if (!wantConnected || Finished) return;
            if (lostAt < 0) lostAt = Time.unscaledTime;
            if (Time.unscaledTime - lostAt > GracePeriod)
            {
                wantConnected = false;
                StatusChanged?.Invoke("Lost connection to the server.");
                return;
            }
            StatusChanged?.Invoke($"{reason}. Retrying…");
            retryAt = Time.unscaledTime + RetryInterval;
        }

        void Receive(string json)
        {
            Envelope env;
            try { env = JsonConvert.DeserializeObject<Envelope>(json); }
            catch (Exception e)
            {
                Debug.LogWarning($"[net] bad message: {e.Message}\n{json}");
                return;
            }
            if (env?.Type == null) return;

            if (env.Type == MsgType.Assigned)
            {
                var a = env.Data?.ToObject<AssignedData>();
                if (!string.IsNullOrEmpty(a?.Code)) Code = a.Code;
                if (!string.IsNullOrEmpty(a?.Token)) Token = a.Token;
            }
            else if (env.Type == MsgType.GameComplete)
            {
                Finished = true;
            }
            MessageReceived?.Invoke(env.Type, env.Data ?? new JObject());
        }

        public void Send(string type, object data)
        {
            if (!IsOnline) return;
            var json = JsonConvert.SerializeObject(new { type, data = data ?? new object() });
            _ = ws.SendText(json);
        }

        // Leave for good: tell the server (which ends the game for the partner and frees the room),
        // then close. Returns false when there was no live game to leave.
        public bool LeaveRoom()
        {
            wantConnected = false;
            retryAt = -1f;
            var socket = ws;
            ws = null; // detach so OnClose / OnDestroy leave this socket alone while the send flushes
            if (socket == null || socket.State != WebSocketState.Open) return false;
            if (Finished) { _ = socket.Close(); return false; }
            Finished = true;
            _ = SendThenClose(socket, JsonConvert.SerializeObject(new { type = MsgType.Leave, data = new object() }));
            return true;
        }

        static async Task SendThenClose(WebSocket socket, string json)
        {
            try { await socket.SendText(json); } catch (Exception e) { Debug.LogWarning($"[net] leave send failed: {e.Message}"); }
            try { await socket.Close(); } catch { /* already closed */ }
        }

        public void Disconnect()
        {
            wantConnected = false;
            retryAt = -1f;
            var socket = ws;
            ws = null;
            if (socket != null && socket.State == WebSocketState.Open) _ = socket.Close();
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Without this, handlers never fire.
            ws?.DispatchMessageQueue();
#endif
            if (wantConnected && retryAt > 0 && Time.unscaledTime >= retryAt) Open();
        }

        void OnApplicationQuit() => Disconnect();
        void OnDestroy() => Disconnect();
    }
}
