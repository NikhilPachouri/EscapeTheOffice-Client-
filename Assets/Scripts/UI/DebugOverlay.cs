using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.UI
{
    // Dev-only: state keys and the last few messages (F1 / three-finger tap), vision mask (F2 /
    // four-finger tap). Off in the shipped game; set DebugOverlay.Enabled = true from code to use it.
    // Offline, clicking a boolean key flips it locally (online the server owns state).
    public class DebugOverlay : MonoBehaviour
    {
        public static bool Enabled = false;

        bool visible;
        Vector2 scroll;
        GUIStyle mono;

        void Update()
        {
            if (!Enabled) { visible = false; return; }
            // Phones: three-finger tap toggles the overlay, four-finger tap the vision mask.
            if (Input.touchCount == 3 && Input.GetTouch(2).phase == TouchPhase.Began) visible = !visible;
            if (Input.touchCount == 4 && Input.GetTouch(3).phase == TouchPhase.Began) GameManager.Instance.DebugNoFog = !GameManager.Instance.DebugNoFog;
            if (Input.GetKeyDown(KeyCode.F1)) visible = !visible;
            if (Input.GetKeyDown(KeyCode.F2)) GameManager.Instance.DebugNoFog = !GameManager.Instance.DebugNoFog;
        }

        void OnGUI()
        {
            if (!visible) return;
            mono ??= new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false, richText = true };

            var gm = GameManager.Instance;
            float w = Mathf.Min(560, Screen.width * 0.45f);
            var area = new Rect(Screen.width - w - 10, 10, w, Screen.height - 20);
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));
            var room = gm.Player != null ? gm.Player.GetComponent<RoomTracker>().Current : null;
            GUILayout.Label($"<b>side</b> {gm.Side}   <b>phase</b> {gm.Current}   <b>room</b> {room?.Id ?? "-"}   <b>fog</b> {(gm.DebugNoFog ? "off" : "on")} (4-finger tap)", mono);
            if (gm.Player != null)
                GUILayout.Label($"<b>pos</b> tile {Mathf.FloorToInt(gm.Player.Position.x)},{gm.World.Height - 1 - Mathf.FloorToInt(gm.Player.Position.y)}   <b>focus</b> {gm.Player.Focus?.Id ?? "-"}", mono);

            GUILayout.Label("<b>state</b>" + (gm.Offline ? "  (click a bool to flip it)" : ""), mono);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(area.height * 0.55f));
            foreach (var kv in gm.State.Values.OrderBy(k => k.Key))
            {
                string v = kv.Value?.ToString(Formatting.None) ?? "null";
                string line = $"{kv.Key} = {v}";
                if (gm.Offline && kv.Value != null && kv.Value.Type == JTokenType.Boolean)
                {
                    if (GUILayout.Button(line, mono)) gm.GetComponent<Net.FakeServer>()?.DebugSet(kv.Key, !kv.Value.Value<bool>());
                }
                else GUILayout.Label(line, mono);
            }
            GUILayout.EndScrollView();

            GUILayout.Label("<b>messages</b>", mono);
            for (int i = gm.MessageLog.Count - 1; i >= 0; i--) GUILayout.Label(gm.MessageLog[i], mono);
            GUILayout.EndArea();
        }
    }
}
