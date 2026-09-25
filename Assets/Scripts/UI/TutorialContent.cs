using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.UI
{
    // Tutorial text, loaded from Resources/Tutorial.json. Edit the JSON, not this file, to change wording.
    public class TutorialContent
    {
        // Banner shown for the whole tutorial.
        [JsonProperty("goal")] public string Goal;
        // Hints on the controls, shown one at a time until the player has done each.
        [JsonProperty("hud")] public HudHints Hud = new HudHints();
        // Appended to a tip for anything with a side glow.
        [JsonProperty("sideLine")] public SideLines SideLine = new SideLines();
        [JsonProperty("rooms")] public RoomTips Rooms = new RoomTips();
        [JsonProperty("tips")] public List<ObjectTip> Tips = new List<ObjectTip>();

        public static TutorialContent Load(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                Debug.LogWarning($"[tutorial] Resources/{resource}.json not found");
                return null;
            }
            try { return JsonConvert.DeserializeObject<TutorialContent>(asset.text); }
            catch (JsonException e)
            {
                Debug.LogError($"[tutorial] Resources/{resource}.json: {e.Message}");
                return null;
            }
        }

        public ObjectTip ForType(string type) =>
            Tips.FirstOrDefault(t => t.Types.Any(x => string.Equals(x, type, System.StringComparison.OrdinalIgnoreCase)));
    }

    public class HudHints
    {
        [JsonProperty("move")] public string Move;
        [JsonProperty("use")] public string Use;
        [JsonProperty("switch")] public string Switch;
    }

    public class SideLines
    {
        [JsonProperty("mine")] public string Mine = "Changes your side";
        [JsonProperty("partner")] public string Partner = "Changes your partner's side";
        [JsonProperty("both")] public string Both = "Changes both sides";
    }

    public class RoomTips
    {
        [JsonProperty("flooded")] public Tip Flooded;
        [JsonProperty("dark")] public Tip Dark;
    }

    public class Tip
    {
        [JsonProperty("title")] public string Title;
        [JsonProperty("text")] public string Text;
    }

    public class ObjectTip
    {
        [JsonProperty("types")] public List<string> Types = new List<string>();
        [JsonProperty("title")] public string Title;
        // Text by the object's current value ("true", "false", "home", "held", ...), with "*" for
        // any other value. A value mapped to null hides the tip, e.g. a door once it is open.
        [JsonProperty("text")] public Dictionary<string, string> Text = new Dictionary<string, string>();
        // One tip for all of this type in a room, above the middle of them (the four code panels).
        [JsonProperty("group")] public bool Group;

        public string TextFor(JToken value)
        {
            string key = value == null || value.Type == JTokenType.Null ? "false"
                : value.Type == JTokenType.Boolean ? (value.Value<bool>() ? "true" : "false")
                : value.ToString().ToLowerInvariant();
            if (Text.TryGetValue(key, out var text)) return text;
            return Text.TryGetValue("*", out text) ? text : null;
        }
    }
}
