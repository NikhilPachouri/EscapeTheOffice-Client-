using System;
using System.Collections.Generic;
using EscapeOffice.Net;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Maps an object's `type` to its prefab (ObjectCatalog) or default component.
    public static class ObjectFactory
    {
        static readonly Dictionary<string, Type> types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "door", typeof(DoorObject) },
            { "button_door", typeof(DoorObject) },
            { "code_door", typeof(DoorObject) },
            { "key_door", typeof(KeyDoorObject) },
            { "exit_door", typeof(ExitDoorObject) },
            { "exit", typeof(ExitDoorObject) },

            { "lasers", typeof(LaserObject) },
            { "laser", typeof(LaserObject) },
            { "fire", typeof(FireObject) },
            { "bombable_wall", typeof(BombableWallObject) },

            { "button", typeof(ButtonObject) },
            { "final_button", typeof(ButtonObject) },
            { "latch_button", typeof(ButtonObject) },
            { "lever", typeof(ButtonObject) },
            { "switch", typeof(ButtonObject) },
            { "light_switch", typeof(ButtonObject) },
            { "laser_switch", typeof(ButtonObject) },
            { "drain", typeof(ButtonObject) },
            { "valve", typeof(ButtonObject) },

            { "keypad", typeof(KeypadObject) },
            { "code_panel", typeof(CodePanelObject) },

            { "bomb", typeof(ItemObject) },
            { "key", typeof(ItemObject) },

            { "boss", typeof(BossController) },
        };

        static ObjectCatalog catalog;
        static bool catalogLoaded;

        public static WorldObject Create(ObjectDef def, Transform parent)
        {
            if (!catalogLoaded)
            {
                catalog = Resources.Load<ObjectCatalog>("ObjectCatalog");
                catalogLoaded = true;
            }

            types.TryGetValue(def.Type ?? "", out var componentType);
            componentType ??= typeof(PlaceholderObject);

            var prefab = catalog != null ? catalog.Find(def.Type) : null;
            GameObject go = prefab != null ? UnityEngine.Object.Instantiate(prefab, parent) : new GameObject();
            go.transform.SetParent(parent, false);

            var obj = go.GetComponent<WorldObject>();
            if (obj == null) obj = (WorldObject)go.AddComponent(componentType);
            return obj;
        }
    }
}
