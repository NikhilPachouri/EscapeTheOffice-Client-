using UnityEngine;

namespace EscapeOffice.Objects
{
    // Unknown `type`: spawn something bright instead of silently dropping it.
    public class PlaceholderObject : WorldObject
    {
        protected override void Build()
        {
            SetBodyColor(Palette.Unknown);
            Debug.LogWarning($"[world] unknown object type '{Type}' (id {Id}) at {Def.X},{Def.Y}; using a placeholder.");
        }
    }
}
