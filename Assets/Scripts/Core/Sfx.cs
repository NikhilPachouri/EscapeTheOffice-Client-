using System.Collections.Generic;
using UnityEngine;

namespace EscapeOffice
{
    // Looks for Resources/Sfx/<name>; falls back to a synthesized placeholder so every cue is
    // audible before real audio exists.
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            name = string.IsNullOrEmpty(name) ? "blip" : name;
            if (cache.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>("Sfx/" + name) ?? Synthesize(name);
            cache[name] = clip;
            return clip;
        }

        static AudioClip Synthesize(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("buzz") || n.Contains("wrong") || n.Contains("deny"))
                return Tone(name, 0.35f, t => Square(140f, t) * 0.35f);
            if (n.Contains("explo") || n.Contains("boom"))
                return Tone(name, 0.9f, t => Noise() * Mathf.Exp(-t * 4f) * 0.8f);
            if (n.Contains("door") || n.Contains("clunk") || n.Contains("thud"))
                return Tone(name, 0.3f, t => (Sine(70f, t) + Noise() * 0.3f) * Mathf.Exp(-t * 12f) * 0.8f);
            if (n.Contains("laser") || n.Contains("zap"))
                return Tone(name, 0.4f, t => Square(900f - t * 1500f, t) * Mathf.Exp(-t * 5f) * 0.25f);
            if (n.Contains("fire") || n.Contains("water") || n.Contains("drain") || n.Contains("hiss"))
                return Tone(name, 0.8f, t => Noise() * Mathf.Sin(Mathf.PI * t / 0.8f) * 0.3f);
            if (n.Contains("light") || n.Contains("switch") || n.Contains("click") || n.Contains("press"))
                return Tone(name, 0.06f, t => Square(1800f, t) * Mathf.Exp(-t * 60f) * 0.4f);
            if (n.Contains("pickup") || n.Contains("item"))
                return Tone(name, 0.25f, t => Sine(t < 0.1f ? 660f : 990f, t) * 0.4f);
            if (n.Contains("alarm") || n.Contains("caught") || n.Contains("boss"))
                return Tone(name, 0.6f, t => Square(Mathf.Repeat(t, 0.3f) < 0.15f ? 520f : 390f, t) * 0.3f);
            if (n.Contains("exit") || n.Contains("win") || n.Contains("complete") || n.Contains("open"))
                return Tone(name, 0.8f, t => Sine(t < 0.2f ? 523f : t < 0.4f ? 659f : t < 0.6f ? 784f : 1046f, t) * 0.35f);
            return Tone(name, 0.18f, t => Sine(t < 0.09f ? 440f : 660f, t) * 0.35f);
        }

        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Square(float hz, float t) => Mathf.Sign(Sine(hz, t));
        static float Noise() => Random.value * 2f - 1f;

        static AudioClip Tone(string name, float seconds, System.Func<float, float> wave)
        {
            int samples = Mathf.CeilToInt(seconds * Rate);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / Rate;
                float fade = Mathf.Clamp01((seconds - t) * 40f) * Mathf.Clamp01(t * 400f);
                data[i] = wave(t) * fade;
            }
            var clip = AudioClip.Create(name, samples, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
