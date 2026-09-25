using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Optional type -> prefab table. Create one at Assets/Resources/ObjectCatalog.asset
    // (Create > Escape Office > Object Catalog) to replace placeholder art per type.
    // A prefab without a WorldObject component gets the default one for its type.
    [CreateAssetMenu(menuName = "Escape Office/Object Catalog", fileName = "ObjectCatalog")]
    public class ObjectCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string type;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();

        public GameObject Find(string type)
        {
            foreach (var e in entries)
                if (e.prefab != null && string.Equals(e.type, type, StringComparison.OrdinalIgnoreCase))
                    return e.prefab;
            return null;
        }
    }
}
