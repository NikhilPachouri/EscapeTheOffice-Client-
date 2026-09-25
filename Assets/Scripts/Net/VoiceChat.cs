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
        const int StartSamples = Frame * 5;     // start playback with 100 ms buffered (internet jitter)
        const int MaxSamples = Rate * 3 / 10;   // past 300 ms, drop the oldest
        const int RebufferAfter = Rate / 5;     // 200 ms of *fully* dry output before rebuffering
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
        public int FramesLost;      // gaps seen in incoming sequence numbers (concealed by Opus)
        public int FramesDropped;   // outgoing frames discarded because the socket fell behind
        public int Underruns;       // playback ran dry long enough to rebuffer
        public int MicRate;         // what the device actually captures at
        public int OutputRate;      // Unity's DSP output rate
        public int BufferMs;        // decoded audio waiting to play
        public int Outbox => outbox.Count;
        public int MicAvailable;    // samples the last Update pulled from the mic
        float nextLog;
        bool wasTalking;

        // Every voice log line also goes to the server (game socket "log"), so a phone's numbers
        // show up in the server log without adb. Some lines come from the socket task, and the
        // game socket must be used from the main thread, so they are queued and flushed in Update.
        static readonly ConcurrentQueue<string> serverLog = new ConcurrentQueue<string>();
        static void Log(string line)
        {
            Debug.Log(line);
            serverLog.Enqueue(line);
            while (serverLog.Count > 32) serverLog.TryDequeue(out _);
        }

        void FlushServerLog()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            while (serverLog.TryDequeue(out var line)) gm.SendLog(line);
        }
        public string Diag => $"tx {FramesSent} rx {FramesReceived} lost {FramesLost} drop {FramesDropped} under {Underruns} buf {BufferMs}ms q {Outbox} mic {MicRate}Hz/{MicAvailable} out {OutputRate}Hz {(Talking ? "TALK" : "")}";

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
        readonly List<float> pending = new List<float>();   // 48 kHz mono, waiting to be framed
        readonly List<float> micQueue = new List<float>();  // mono at the mic's own rate, not yet resampled
        double resamplePos;                                  // fractional read position into micQueue
        readonly short[] pcmOut = new short[Frame];
        readonly byte[] packet = new byte[MaxPayload];
        ushort seq;

        // Playback: decoded samples, written by the receive task, read by the audio thread.
        readonly float[] ring = new float[Rate];
        int ringStart, ringCount;
        int drySamples;
        double readPos;      // fractional position into the ring, for the 48 kHz -> output rate resample
        bool playing;
        readonly object ringLock = new object();
        AudioSource source;

        // voiceUrl: the server's separate relay (wss://host/voice) when it runs one; otherwise
        // /voice on the game server.
        public void Begin(string gameUrl, string roomCode, string roomToken, string voiceUrl = null)
        {
            if (Active && roomCode == code && roomToken == token) return; // same slot, already running
            Stop();
            if (string.IsNullOrEmpty(roomToken)) return;
            code = roomCode;
            token = roomToken;

            var query = $"code={Uri.EscapeDataString(code)}&token={Uri.EscapeDataString(token)}";
            if (!string.IsNullOrEmpty(voiceUrl))
            {
                uri = new UriBuilder(voiceUrl) { Query = query }.Uri;
            }
            else
            {
                // wss://host/ws -> wss://host/voice?code=..&token=..
                var game = new Uri(gameUrl);
                uri = new UriBuilder(game.Scheme, game.Host, game.IsDefaultPort ? -1 : game.Port, "/voice") { Query = query }.Uri;
            }

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
            pending.Clear();
            micQueue.Clear();
            resamplePos = 0;
            lock (ringLock) { ringCount = 0; drySamples = 0; playing = false; }
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
                    if (socket.CloseStatus.HasValue) Log($"[voice] closed {(int)socket.CloseStatus} {socket.CloseStatusDescription}");
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e)
                {
                    // A 401 (bad token / room gone) also lands here; backoff keeps it cheap.
                    Log($"[voice] error {e.Message}");
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

        // Frames go out as soon as they exist. The mic produces 50/s and the server allows 100/s
        // sustained with a burst of 20, so no pacing is needed; pacing with Task.Delay only fell
        // behind (timer slop) and forced the outbox to drop frames, which the partner heard as crackle.
        async Task Send(ClientWebSocket socket, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                if (!outbox.TryDequeue(out var frame)) { await Task.Delay(4, ct); continue; }
                await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, ct);
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
                    if (gap > 0) Interlocked.Add(ref FramesLost, gap);
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

        // Playback goes through OnAudioFilterRead on a silent looping AudioSource: the audio thread
        // asks for one DSP buffer at a time at the device rate, and we resample from the 48 kHz ring
        // ourselves. Streaming clips (PCMReaderCallback) glitched at their loop point and asked for
        // unpredictable chunk sizes, which chopped the voice at fixed intervals.
        //
        // The source lives on its own child GameObject: Unity binds OnAudioFilterRead to every
        // AudioSource on the object that holds the script, and GameManager also carries the music
        // and SFX sources, so each of them drained the ring and most of the voice was thrown away
        // (with a red "multiple AudioSources" error per frame).
        void StartPlayback()
        {
            if (source == null)
            {
                var go = new GameObject("VoicePlayback");
                go.transform.SetParent(transform, false);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = true;
                source.clip = AudioClip.Create("voice-carrier", Rate / 10, 1, Rate, false); // 100 ms of silence
                go.AddComponent<VoiceSink>().owner = this;
            }
            OutputRate = AudioSettings.outputSampleRate;
            if (OutputRate <= 0) OutputRate = Rate;
            source.Play();
        }

        // Sits next to the playback AudioSource and forwards the audio-thread callback.
        class VoiceSink : MonoBehaviour
        {
            public VoiceChat owner;
            void OnAudioFilterRead(float[] data, int channels) => owner?.Fill(data, channels);
        }

        // Audio thread. Never blocks for long; outputs silence when dry.
        // A momentary shortfall (one late packet) is padded with silence and playback carries on;
        // only a long fully-dry spell drops back to rebuffering.
        internal void Fill(float[] data, int channels)
        {
            if (channels <= 0) return;
            int frames = data.Length / channels;
            double step = (double)Rate / OutputRate; // ring samples per output frame
            lock (ringLock)
            {
                if (!playing && ringCount >= StartSamples) { playing = true; drySamples = 0; readPos = 0; }
                int produced = 0;
                if (playing)
                {
                    while (produced < frames)
                    {
                        int i0 = (int)readPos;
                        if (i0 + 1 >= ringCount) break;
                        float frac = (float)(readPos - i0);
                        float a = ring[(ringStart + i0) % ring.Length];
                        float b = ring[(ringStart + i0 + 1) % ring.Length];
                        float v = MuteIncoming ? 0f : a + (b - a) * frac;
                        for (int c = 0; c < channels; c++) data[produced * channels + c] = v;
                        produced++;
                        readPos += step;
                    }
                    int consumed = (int)readPos;
                    ringStart = (ringStart + consumed) % ring.Length;
                    ringCount -= consumed;
                    readPos -= consumed;
                    drySamples = produced == 0 ? drySamples + frames : 0;
                    if (drySamples > RebufferAfter) { playing = false; drySamples = 0; Underruns++; }
                }
                for (int i = produced * channels; i < data.Length; i++) data[i] = 0f;
                BufferMs = ringCount * 1000 / Rate;
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
            FlushServerLog();
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
                Log($"[voice] mic '{Microphone.devices[0]}' started: {mic.frequency} Hz, {mic.channels} ch, {mic.samples} samples, output {AudioSettings.outputSampleRate} Hz");
            }

            Talking = (TalkHeld || Input.GetKey(KeyCode.V) || openMicForTesting) && Connected && mic != null;
            if (Talking != wasTalking)
            {
                wasTalking = Talking;
                Log(Talking ? "[voice] TALK down" : $"[voice] TALK up — {Diag}");
            }
            // Periodic line while voice is up; every 2 s when talking or receiving, otherwise every 10 s.
            if (Time.unscaledTime >= nextLog)
            {
                bool busy = Talking || PartnerSpeaking;
                nextLog = Time.unscaledTime + (busy ? 2f : 10f);
                Log($"[voice] {(Connected ? "on" : Status)} {Diag}");
            }
            if (mic == null) return;

            int pos = Microphone.GetPosition(null);
            int available = (pos - micPos + mic.samples) % mic.samples;
            MicAvailable = available;
            if (available == 0) return;

            int channels = mic.channels;
            if (micRead.Length != available * channels) micRead = new float[available * channels];
            mic.GetData(micRead, micPos); // wraps around the looping clip
            micPos = pos;

            if (!Talking) { pending.Clear(); micQueue.Clear(); resamplePos = 0; return; } // silence costs nothing
            MicRate = mic.frequency;

            // Downmix to mono at the mic's rate.
            for (int i = 0; i < available; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += micRead[i * channels + c];
                micQueue.Add(sum / channels);
            }

            // Resample to 48 kHz. The fractional read position carries across Update calls and
            // samples are interpolated; truncating per call dropped a sample or two every frame
            // on 44.1 kHz mics, an audible tick under the voice.
            if (mic.frequency == Rate)
            {
                pending.AddRange(micQueue);
                micQueue.Clear();
            }
            else
            {
                double step = (double)mic.frequency / Rate;
                while (resamplePos + 1 < micQueue.Count)
                {
                    int i0 = (int)resamplePos;
                    float frac = (float)(resamplePos - i0);
                    pending.Add(micQueue[i0] + (micQueue[i0 + 1] - micQueue[i0]) * frac);
                    resamplePos += step;
                }
                int consumed = (int)resamplePos;
                if (consumed > 0)
                {
                    micQueue.RemoveRange(0, consumed);
                    resamplePos -= consumed;
                }
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
                while (outbox.Count > 10) { outbox.TryDequeue(out _); FramesDropped++; } // stalled network: keep it fresh
            }
        }
    }
}
