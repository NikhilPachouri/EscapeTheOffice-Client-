using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Net
{
    // Offline stand-in for the Go server, for testing the client alone. Loads
    // Resources/FakeWorld.json (one side's `world` message plus rules in the world.json
    // vocabulary) and runs a minimal version of the server loop: rules, inventory, codes,
    // derived keys and diffed patches. It is a test harness, not the real rules engine.
    public class FakeServer : MonoBehaviour, IServerLink
    {
        const float Latency = 0.05f;

        public event Action<string, JToken> MessageReceived;
        public event Action<string> StatusChanged;
        public bool IsOnline => true;

        readonly Queue<(float at, string type, JToken data)> outbox = new Queue<(float, string, JToken)>();
        Dictionary<string, JToken> state = new Dictionary<string, JToken>();
        Dictionary<string, JObject> objects = new Dictionary<string, JObject>();
        List<JObject> rules = new List<JObject>();
        Dictionary<string, string> derived = new Dictionary<string, string>();
        string side = "A";
        bool finished;

        string InvKey => "inv_" + side;

        // The level document being played (FakeWorld.json format). The level editor edits a
        // copy and hands it back through Reload.
        public JObject Level { get; private set; }

        public void Begin(JObject level = null)
        {
            level ??= LevelStore.Load(LevelStore.Default);
            if (level == null)
            {
                StatusChanged?.Invoke("Resources/FakeWorld.json not found");
                return;
            }
            var world = Load(level);

            StatusChanged?.Invoke("Offline");
            Emit(MsgType.Assigned, JObject.FromObject(new { side, token = "offline" }));
            Emit(MsgType.Waiting, new JObject());
            Emit(MsgType.World, world, 0.6f);
        }

        // New world on the same connection with fresh state (level editor, level switch).
        public void Reload(JObject level)
        {
            outbox.Clear();
            Emit(MsgType.World, Load(level));
        }

        JObject Load(JObject root)
        {
            Level = root;
            finished = false;
            side = root.Value<string>("side") ?? "A";
            var world = (JObject)root["world"].DeepClone();

            state = ((JObject)world["state"] ?? new JObject()).Properties().ToDictionary(p => p.Name, p => p.Value);
            if (!state.ContainsKey(InvKey)) state[InvKey] = new JArray();
            // Codes are generated randomly per session, like the real server.
            foreach (var code in root["codes"]?.Values<string>() ?? Enumerable.Empty<string>())
                state[code] = UnityEngine.Random.Range(0, 10000).ToString("0000");

            objects = ((JArray)world["objects"] ?? new JArray()).Cast<JObject>()
                .Where(o => o.Value<string>("id") != null)
                .GroupBy(o => o.Value<string>("id")).ToDictionary(g => g.Key, g => g.First());
            rules = (root["rules"] as JArray)?.Cast<JObject>().ToList() ?? new List<JObject>();
            derived = root["derived"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            RecomputeDerived();
            world["state"] = JObject.FromObject(state);
            return world;
        }

        public void Send(string type, object data)
        {
            if (finished) return;
            var d = JToken.FromObject(data);
            var before = Snapshot();

            switch (type)
            {
                case MsgType.Interact:
                    Interact(d.Value<string>("id"), d.Value<string>("action"), d.Value<string>("value"));
                    break;
                case MsgType.Enter:
                    if (Truthy("exit_open"))
                    {
                        finished = true;
                        Emit(MsgType.GameComplete, new JObject());
                        return;
                    }
                    break;
            }

            RecomputeDerived();
            SendChanges(before);
        }

        public void Disconnect() { }

        // Debug overlay: set a key as if a rule had.
        public void DebugSet(string key, JToken value)
        {
            var before = Snapshot();
            state[key] = value;
            RecomputeDerived();
            SendChanges(before);
        }

        void Interact(string id, string action, string value)
        {
            if (id == null || !objects.TryGetValue(id, out var obj)) return; // must exist on this side

            var matching = rules.Where(r => r.Value<string>("on") == $"{id}:{action}").ToList();
            if (matching.Count == 0 && action == "pickup") { Pickup(obj); return; }

            foreach (var rule in matching)
            {
                var req = rule["requires"] as JObject;
                if (req?["holding"] != null && !Holding(req.Value<string>("holding"))) continue;
                if (req?["code"] != null)
                {
                    var expected = state.TryGetValue(req.Value<string>("code"), out var c) ? c.ToString() : null;
                    if (value != expected)
                    {
                        Emit(MsgType.Fx, JObject.FromObject(new { effect = "buzz", at = id }));
                        continue; // a wrong code buzzes and nothing else
                    }
                }
                foreach (var step in (JArray)rule["do"]) Do((JObject)step, id);
            }
        }

        void Do(JObject step, string sourceId)
        {
            if (step["toggle"] != null)
            {
                var key = step.Value<string>("toggle");
                state[key] = !Truthy(key);
            }
            if (step["set"] is JObject set)
                foreach (var p in set.Properties()) state[p.Name] = p.Value;
            if (step["fx"] != null)
                Emit(MsgType.Fx, JObject.FromObject(new { effect = step.Value<string>("fx"), at = step["at"] ?? sourceId }));
            if (step["consume"] != null)
            {
                var item = step.Value<string>("consume");
                state[InvKey] = new JArray(Inventory().Where(i => i != item));
                foreach (var o in objects.Values.Where(o => o.Value<string>("type") == item))
                    state[o.Value<string>("key")] = "used";
            }
        }

        void Pickup(JObject obj)
        {
            var key = obj.Value<string>("key");
            if (key == null || state.TryGetValue(key, out var v) && v.ToString() != "home") return;
            state[key] = "held";
            state[InvKey] = new JArray(Inventory().Append(obj.Value<string>("type")));
            Emit(MsgType.Fx, JObject.FromObject(new { effect = "pickup", at = obj.Value<string>("id") }));
        }

        IEnumerable<string> Inventory() =>
            state.TryGetValue(InvKey, out var inv) && inv is JArray a ? a.Values<string>() : Enumerable.Empty<string>();

        bool Holding(string item) => Inventory().Contains(item);

        bool Truthy(string key) => state.TryGetValue(key, out var v) && EscapeOffice.WorldState.Truthy(v);

        // Only "a && b && c" expressions; enough for exit_open.
        void RecomputeDerived()
        {
            foreach (var kv in derived)
                state[kv.Key] = kv.Value.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries)
                    .All(k => Truthy(k.Trim()));
        }

        Dictionary<string, JToken> Snapshot() => state.ToDictionary(kv => kv.Key, kv => kv.Value.DeepClone());

        void SendChanges(Dictionary<string, JToken> before)
        {
            var patch = new JObject();
            foreach (var kv in state)
                if (!before.TryGetValue(kv.Key, out var old) || !JToken.DeepEquals(old, kv.Value))
                    patch[kv.Key] = kv.Value.DeepClone();
            if (patch.HasValues) Emit(MsgType.Patch, patch);
        }

        void Emit(string type, JToken data, float delay = Latency) =>
            outbox.Enqueue((Time.unscaledTime + delay, type, data));

        void Update()
        {
            while (outbox.Count > 0 && outbox.Peek().at <= Time.unscaledTime)
            {
                var (_, type, data) = outbox.Dequeue();
                MessageReceived?.Invoke(type, data);
            }
        }
    }
}
