using System;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    // Authored, mesh-only views. No inventory, production or navigation state lives here.
    public sealed class WorldPropCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string Key; public GameObject Prefab; public string Source; }
        public Entry[] Entries = Array.Empty<Entry>();
        private static WorldPropCatalog cached;
        public static WorldPropCatalog Current => cached != null ? cached :
            cached = UnityEngine.Resources.Load<WorldPropCatalog>("World/WorldProps");
        public GameObject Find(string key) => Entries.FirstOrDefault(x => x.Key == key)?.Prefab;
        public static GameObject Create(string key, Transform parent)
        {
            var prefab = Current != null ? Current.Find(key) : null;
            if (prefab == null) throw new InvalidOperationException("Missing authored world prop: " + key);
            var view = UnityEngine.Object.Instantiate(prefab, parent, false);
            view.name = "Imported " + key;
            return view;
        }
    }
}
