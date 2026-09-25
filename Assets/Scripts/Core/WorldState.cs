using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace EscapeOffice
{
    public interface IStateListener
    {
        void Apply(JToken value);
    }

    // Flat key-value mirror of the server state this player can see, plus the key index:
    // state key -> everything that reads it. A patch calls Apply on each listed reader.
    public class WorldState
    {
        readonly Dictionary<string, JToken> values = new Dictionary<string, JToken>();
        readonly Dictionary<string, List<IStateListener>> index = new Dictionary<string, List<IStateListener>>();

        public event Action<string, JToken> Changed;

        public IReadOnlyDictionary<string, JToken> Values => values;

        public void Reset(IDictionary<string, JToken> initial)
        {
            values.Clear();
            index.Clear();
            if (initial == null) return;
            foreach (var kv in initial) values[kv.Key] = kv.Value;
        }

        public void Register(string key, IStateListener listener)
        {
            if (string.IsNullOrEmpty(key) || listener == null) return;
            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<IStateListener>();
            list.Add(listener);
        }

        // Pushes the current value of every indexed key to its readers (after the world is built).
        public void ApplyAll()
        {
            foreach (var kv in index)
            {
                var value = Get(kv.Key);
                foreach (var l in kv.Value) l.Apply(value);
            }
        }

        public void Patch(JObject patch)
        {
            if (patch == null) return;
            foreach (var prop in patch.Properties()) Set(prop.Name, prop.Value);
        }

        // Also used by the offline fake server and the debug overlay.
        public void Set(string key, JToken value)
        {
            values[key] = value;
            if (index.TryGetValue(key, out var list))
                foreach (var l in list.ToArray()) l.Apply(value);
            Changed?.Invoke(key, value);
        }

        public JToken Get(string key) =>
            key != null && values.TryGetValue(key, out var v) ? v : null;

        public bool GetBool(string key, bool fallback = false)
        {
            var v = Get(key);
            return v == null || v.Type == JTokenType.Null ? fallback : Truthy(v);
        }

        public static bool Truthy(JToken t)
        {
            if (t == null) return false;
            switch (t.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined: return false;
                case JTokenType.Boolean: return t.Value<bool>();
                case JTokenType.Integer:
                case JTokenType.Float: return Math.Abs(t.Value<double>()) > double.Epsilon;
                case JTokenType.String:
                    var s = t.Value<string>().Trim().ToLowerInvariant();
                    return s.Length > 0 && s != "false" && s != "0" && s != "off" && s != "no";
                default: return t.HasValues;
            }
        }
    }
}
