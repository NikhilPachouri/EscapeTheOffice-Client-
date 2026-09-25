using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Concentus.Enums;
using Concentus.Structs;
using UnityEngine;

namespace EscapeOffice.Net
{
    // Push-to-talk voice over the server's /voice relay. The server only forwards frames, so
    // capture, Opus encode/decode, jitter buffering and playback all live here.
    //
    // Frame (one binary message): [0] version = 1, [1..2] seq uint16 big-endian, [3..] Opus packet.
    // The voice socket is independent of the game socket; each reconnects on its own.
    public class VoiceChat : MonoBehaviour
    {
        const int Rate = 48000;
        const int Frame = 960;                  // 20 ms mono
        const int MaxPayload = 480;
        const int StartSamples = Frame * 3;     // start playback with 60 ms buffered
        const int MaxSamples = Rate / 5;        // past 200 ms, drop the oldest
        const int MaxConcealed = 5;             // PLC frames for one gap, at most
        static readonly long SpeakingWindow = TimeSpan.FromMilliseconds(200).Ticks;

        public bool Active => cts != null;
        public bool Connected => ws != null && ws.State == WebSocketState.Open;
        public bool Talking { get; private set; }
        public bool MuteIncoming { get; set; }
        public bool PartnerSpeaking => DateTime.UtcNow.Ticks - Interlocked.Read(ref lastReceived) < SpeakingWindow;
        public string Status { get; private set; } = "";

        // Hold to talk: the touch button, or V on a keyboard.
        public static bool TalkHeld;

        [Tooltip("Testing only: transmit continuously without holding TALK (e.g. with the server's -voice-loop).")]
        public bool openMicForTesting;

        // Diagnostics for the debug overlay and tests.
        public int FramesSent, FramesReceived;

        string code, token;
        Uri uri;
        CancellationTokenSource cts;
        ClientWebSocket ws;
        readonly ConcurrentQueue<byte[]> outbox = new ConcurrentQueue<byte[]>();
        OpusEncoder encoder;
        long lastReceived;
        volatile bool announce;

        // Capture
        AudioClip mic;
        int micPos;
        float[] micRead = new float[0];
        readonly List<float> pending = new List<float>();
        readonly short[] pcmOut = new short[Frame];
        readonly byte[] packet = new byte[MaxPayload];
        ushort seq;

        // Playback: decoded samples, written by the receive task, read by the audio thread.
        readonly float[] ring = new float[Rate];
        int ringStart, ringCount;
        bool playing;
        readonly object ringLock = new object();
        AudioSource source;

        public void Begin(string gameUrl, string roomCode, string roomToken)
        {
            if (Active && roomCode == code && roomToken == token) return; // same slot, already running
            Stop();
            if (string.IsNullOrEmpty(roomToken)) return;
            code = roomCode;
            token = roomToken;

            // wss://host/ws -> wss://host/voice?code=..&token=..
            var game = new Uri(gameUrl);
            uri = new UriBuilder(game.Scheme, game.Host, game.IsDefaultPort ? -1 : game.Port, "/voice")
            {
                Query = $"code={Uri.EscapeDataString(code)}&token={Uri.EscapeDataString(token)}"
            }.Uri;

            encoder = new OpusEncoder(Rate, 1, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = 24000,
                Complexity = 5,
                SignalType = OpusSignal.OPUS_SIGNAL_VOICE,
            };
            StartPlayback();
            RequestMic();
            cts = new CancellationTokenSource();
            _ = Run(cts.Token);
        }

        public void Stop()
        {
            if (cts == null) return;
            cts.Cancel();
            cts = null;
            var socket = ws;
            ws = null;
            try { if (socket != null && socket.State == WebSocketState.Open) _ = socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "leave", CancellationToken.None); }
            catch { /* already closing */ }
            if (mic != null) Microphone.End(null);
            mic = null;
            if (source != null) source.Stop();
            while (outbox.TryDequeue(out _)) { }
            lock (ringLock) { ringCount = 0; playing = false; }
            Status = "";
        }

        void OnDestroy() => Stop();
        void OnApplicationQuit() => Stop();

        // ---------------------------------------------------------------- transport

        async Task Run(CancellationToken ct)
        {
            float backoff = 1f;
            while (!ct.IsCancellationRequested)
            {
                var socket = new ClientWebSocket();
                try
                {
                    Status = "Voice connecting…";
                    await socket.ConnectAsync(uri, ct);
                    ws = socket;
                    backoff = 1f;
                    Status = "Voice on";
                    announce = true;
                    var decoder = new OpusDecoder(Rate, 1);
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        var receive = Receive(socket, decoder, linked.Token);
                        var send = Send(socket, linked.Token);
                        await Task.WhenAny(receive, send);
                        linked.Cancel();
                    }
                    // The server closes with 1001 when the room ends: don't come back.
                    if (socket.CloseStatus == WebSocketCloseStatus.EndpointUnavailable) { Status = "Voice ended"; return; }
                    if (socket.CloseStatus.HasValue) Debug.LogWarning($"[voice] closed {(int)socket.CloseStatus} {socket.CloseStatusDescription}");
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e)
                {
                    // A 401 (bad token / room gone) also lands here; backoff keeps it cheap.
                    Debug.LogWarning($"[voice] {e.Message}");
                }
                finally
                {
                    if (ws == socket) ws = null;
                    socket.Dispose();
                }
                if (ct.IsCancellationRequested) return;
                Status = "Voice reconnecting…";
                try { await Task.Delay(TimeSpan.FromSeconds(backoff), ct); } catch (OperationCanceledException) { return; }
                backoff = Mathf.Min(backoff * 2f, 15f);
            }
        }

        async Task Send(ClientWebSocket socket, CancellationToken ct)
        {
            var last = DateTime.UtcNow.AddSeconds(-1);
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                if (!outbox.TryDequeue(out var frame)) { await Task.Delay(5, ct); continue; }
                // Never more than one frame per 20 ms (server closes at >100/s sustained).
                var wait = TimeSpan.FromMilliseconds(20) - (DateTime.UtcNow - last);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, ct);
                last = DateTime.UtcNow;
                Interlocked.Increment(ref FramesSent);
            }
        }

        async Task Receive(ClientWebSocket socket, OpusDecoder decoder, CancellationToken ct)
        {
            var buffer = new byte[2048];
            var pcm = new short[Frame];
            int expected = -1;
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                int length = 0;
                WebSocketReceiveResult r;
                do
                {
                    r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), ct);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    length += r.Count;
                } while (!r.EndOfMessage && length < buffer.Length);

                if (r.MessageType != WebSocketMessageType.Binary || length < 4 || buffer[0] != 1) continue;
                int s = (buffer[1] << 8) | buffer[2];

                if (expected >= 0)
                {
                    int gap = (s - expected + 65536) % 65536;
                    if (gap >= 32768) continue; // late or duplicate: drop
                    for (int i = 0; i < Math.Min(gap, MaxConcealed); i++) // lost frames: let Opus conceal them
                        Push(pcm, decoder.Decode(null, 0, 0, pcm, 0, Frame, false));
                }
                expected = (s + 1) % 65536;

                int n = decoder.Decode(buffer, 3, length - 3, pcm, 0, Frame, false);
                Push(pcm, n);
                Interlocked.Exchange(ref lastReceived, DateTime.UtcNow.Ticks);
                Interlocked.Increment(ref FramesReceived);
            }
        }

        void Push(short[] pcm, int count)
        {
            lock (ringLock)
            {
                for (int i = 0; i < count; i++)
                {
                    if (ringCount == ring.Length) { ringStart = (ringStart + 1) % ring.Length; ringCount--; }
                    ring[(ringStart + ringCount) % ring.Length] = pcm[i] / 32768f;
                    ringCount++;
                }
                // Catch-up burst: bring latency back down.
                if (ringCount > MaxSamples)
                {
                    int drop = ringCount - StartSamples;
                    ringStart = (ringStart + drop) % ring.Length;
                    ringCount -= drop;
                }
            }
        }

        // ---------------------------------------------------------------- playback

        void StartPlayback()
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = true;
                // Streaming clip at 48 kHz; Unity resamples to the device rate.
                source.clip = AudioClip.Create("voice", Rate, 1, Rate, true, OnAudioRead);
            }
            source.Play();
        }

        // Audio thread. Never blocks for long; outputs silence when dry.
        void OnAudioRead(float[] data)
        {
            lock (ringLock)
            {
                if (!playing && ringCount >= StartSamples) playing = true;
                int n = playing ? Math.Min(ringCount, data.Length) : 0;
                for (int i = 0; i < n; i++) data[i] = MuteIncoming ? 0f : ring[(ringStart + i) % ring.Length];
                for (int i = n; i < data.Length; i++) data[i] = 0f;
                ringStart = (ringStart + n) % ring.Length;
                ringCount -= n;
                if (ringCount == 0) playing = false; // rebuffer after an underrun
            }
        }

        // ---------------------------------------------------------------- capture

        void RequestMic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
        }

        bool MicAllowed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
            return true;
#endif
        }

        void Update()
        {
            if (announce)
            {
                announce = false;
                GameManager.Instance.Toast("Voice on: hold TALK to speak. Use headphones (no echo cancellation).");
            }
            if (!Active) { Talking = false; return; }

            if (mic == null && MicAllowed() && Microphone.devices.Length > 0)
            {
                mic = Microphone.Start(null, true, 1, Rate);
                micPos = 0;
            }

            Talking = (TalkHeld || Input.GetKey(KeyCode.V) || openMicForTesting) && Connected && mic != null;
            if (mic == null) return;

            int pos = Microphone.GetPosition(null);
            int available = (pos - micPos + mic.samples) % mic.samples;
            if (available == 0) return;

            int channels = mic.channels;
            if (micRead.Length != available * channels) micRead = new float[available * channels];
            mic.GetData(micRead, micPos); // wraps around the looping clip
            micPos = pos;

            if (!Talking) { pending.Clear(); return; } // silence costs nothing

            // Downmix, and resample to 48 kHz if the device picked another rate.
            float step = (float)mic.frequency / Rate;
            int outCount = Mathf.FloorToInt(available / step);
            for (int i = 0; i < outCount; i++)
            {
                int src = Mathf.Min(available - 1, Mathf.FloorToInt(i * step));
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += micRead[src * channels + c];
                pending.Add(sum / channels);
            }

            while (pending.Count >= Frame)
            {
                for (int i = 0; i < Frame; i++) pcmOut[i] = (short)Mathf.Clamp(pending[i] * 32767f, -32768f, 32767f);
                pending.RemoveRange(0, Frame);
                int len = encoder.Encode(pcmOut, 0, Frame, packet, 0, MaxPayload);
                if (len <= 0) continue;
                var frame = new byte[3 + len];
                frame[0] = 1;
                frame[1] = (byte)(seq >> 8);
                frame[2] = (byte)seq;
                Buffer.BlockCopy(packet, 0, frame, 3, len);
                seq++;
                outbox.Enqueue(frame);
                while (outbox.Count > 10) outbox.TryDequeue(out _); // stalled network: keep it fresh
            }
        }
    }
}
