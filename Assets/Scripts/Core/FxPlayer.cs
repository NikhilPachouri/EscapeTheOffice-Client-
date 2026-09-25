using System.Collections.Generic;
using EscapeOffice.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice
{
    // One-off cues. Most changes happen off-screen, so each cue is played spatially (pan and
    // volume by distance) with a pulse at its source and an edge-of-screen indicator when the
    // source is outside the vision radius.
    public class FxPlayer : MonoBehaviour
    {
        public struct Indicator
        {
            public Vector2 Position;
            public float Born;
            public string Label;
        }

        public const float IndicatorLifetime = 2.5f;
        public readonly List<Indicator> Indicators = new List<Indicator>();

        const int Voices = 8;
        readonly AudioSource[] voices = new AudioSource[Voices];
        int nextVoice;

        void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f; // panned by hand: an ortho camera makes 3D rolloff odd
            }
        }

        public void Play(FxData fx)
        {
            if (fx == null || string.IsNullOrEmpty(fx.Effect)) return;
            Vector2? at = Resolve(fx.At);
            PlayAt(fx.Effect, at);

            if (at.HasValue)
            {
                Pulse(at.Value, ColorFor(fx.Effect));
                var rig = GameManager.Instance.CameraRig;
                if (rig != null && rig.IsOffscreen(at.Value))
                    Indicators.Add(new Indicator { Position = at.Value, Born = Time.time, Label = fx.Effect.Replace('_', ' ') });
            }
        }

        public void PlayAt(string effect, Vector2? at)
        {
            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            src.clip = Sfx.Get(effect);

            var player = GameManager.Instance.Player;
            if (at.HasValue && player != null)
            {
                var d = at.Value - player.Position;
                src.panStereo = Mathf.Clamp(d.x / 10f, -0.8f, 0.8f);
                src.volume = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(d.magnitude / 30f));
            }
            else
            {
                src.panStereo = 0f;
                src.volume = 1f;
            }
            src.Play();
        }

        Vector2? Resolve(JToken at)
        {
            if (at == null || at.Type == JTokenType.Null) return null;
            var gm = GameManager.Instance;
            if (at.Type == JTokenType.String)
            {
                if (gm.World.Objects.TryGetValue(at.Value<string>(), out var obj)) return obj.Bounds.center;
                // The source is on the other side (their button opened our door): use the object
                // here whose key the patch just before this cue changed.
                if (Time.time - gm.LastPatchTime < 0.5f)
                    foreach (var o in gm.World.Objects.Values)
                        if (o.Def.Key != null && gm.LastPatchKeys.Contains(o.Def.Key)) return o.Bounds.center;
                return null;
            }
            if (at is JArray || at is JObject) return gm.World.TileCenter(at);
            return null;
        }

        static Color ColorFor(string effect)
        {
            var e = effect.ToLowerInvariant();
            if (e.Contains("explo") || e.Contains("fire")) return new Color(1f, 0.5f, 0.1f);
            if (e.Contains("buzz") || e.Contains("wrong")) return new Color(1f, 0.2f, 0.2f);
            if (e.Contains("water") || e.Contains("drain")) return Palette.Water;
            return Color.white;
        }

        void Pulse(Vector2 at, Color color)
        {
            var sr = SpriteFactory.Child(transform, "Pulse", SpriteFactory.Ring, color, Layers.Fx, at);
            sr.transform.SetParent(null, true);
            sr.transform.position = at;
            StartCoroutine(Expand(sr));
        }

        System.Collections.IEnumerator Expand(SpriteRenderer sr)
        {
            var c = sr.color;
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                if (sr == null) yield break;
                float k = t / 0.8f;
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3f, k);
                sr.color = new Color(c.r, c.g, c.b, 1f - k);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        void Update() => Indicators.RemoveAll(i => Time.time - i.Born > IndicatorLifetime);
    }
}
