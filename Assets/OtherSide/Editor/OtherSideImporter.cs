using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// Turns the raw "The Other Side" asset pack (README.md next to this folder) into Unity assets:
// import settings for models/textures/audio, materials from palette.json (Built-in pipeline),
// and one prefab per model with the colliders and lights listed in manifest.json.
// Run Tools → Other Side → Build Assets after changing palette.json or any FBX.
public class OtherSideImporter : AssetPostprocessor
{
    const string Root = "Assets/OtherSide";
    const string ModelsDir = Root + "/Models/FBX";
    const string MaterialsDir = Root + "/Materials";
    const string PrefabsDir = Root + "/Prefabs";

    // ---------- import settings ----------

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ModelsDir)) return;
        var mi = (ModelImporter)assetImporter;
        // These files are Z-up with a +90° X root that Bake Axis Conversion doesn't remove;
        // BakeAxes() fixes the geometry when the prefabs are built instead.
        mi.bakeAxisConversion = false;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.addCollider = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root + "/Textures")) return;
        var ti = (TextureImporter)assetImporter;
        var name = Path.GetFileNameWithoutExtension(assetPath);
        if (name.StartsWith("Icon_") || name.StartsWith("UI_Icon_") || name == "FX_SoftDot")
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
        }
        ti.wrapMode = TextureWrapMode.Clamp;
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root + "/Audio")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.ADPCM;
        ai.defaultSampleSettings = s;
    }

    // ---------- build ----------

    [MenuItem("Tools/Other Side/Build Assets")]
    public static void BuildAll()
    {
        // Play mode forbids reading the source meshes, and a failed bake would save un-baked meshes.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[OtherSide] Leave Play mode before running Build Assets.");
            return;
        }
        var palette = JObject.Parse(File.ReadAllText(Root + "/palette.json"));
        var manifest = JObject.Parse(File.ReadAllText(Root + "/manifest.json"));

        var mats = BuildMaterials((JObject)palette["materials"]);
        RemapModelMaterials(mats);
        BuildFxMaterials();
        int prefabs = BuildPrefabs((JArray)manifest["assets"]);
        prefabs += TosProps.BuildAll(); // the procedural props from tos-assets.js
        BuildCatalog((JArray)manifest["assets"]);

        AssetDatabase.SaveAssets();
        Debug.Log($"[OtherSide] {mats.Count} materials, {prefabs} prefabs built.");
    }

    static Dictionary<string, Material> BuildMaterials(JObject defs)
    {
        EnsureFolder(MaterialsDir);
        var result = new Dictionary<string, Material>();
        foreach (var p in defs.Properties())
        {
            var d = (JObject)p.Value;
            var path = $"{MaterialsDir}/{p.Name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = ShaderFor((string)d["shader"], d["texture"] != null);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            Apply(mat, d);
            EditorUtility.SetDirty(mat);
            result[p.Name] = mat;
        }
        return result;
    }

    // Particle materials for EscapeOffice.Fx3D (soft dot, tinted by particle colour). Made here
    // so their shaders ship with builds.
    static void BuildFxMaterials()
    {
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/FX_SoftDot.png");
        foreach (var (name, shader) in new[] { ("FX_Additive", "Legacy Shaders/Particles/Additive"), ("FX_Alpha", "Legacy Shaders/Particles/Alpha Blended"), ("FX_Energy", "Legacy Shaders/Particles/Additive") })
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(shader);
            mat.mainTexture = name == "FX_Energy" ? null : dot; // FX_Energy: solid colour for lines and shells
            mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // 2 × tint = particle colour as-is
            EditorUtility.SetDirty(mat);
        }
    }

    static Shader ShaderFor(string kind, bool textured)
    {
        kind ??= "Lit";
        if (kind.StartsWith("Unlit / Additive")) return Shader.Find("Legacy Shaders/Particles/Additive");
        if (kind.StartsWith("Unlit")) return Shader.Find(textured ? "Unlit/Texture" : "Unlit/Color");
        return Shader.Find("Standard");
    }

    static void Apply(Material m, JObject d)
    {
        var kind = (string)d["shader"] ?? "Lit";
        var color = Hex((string)d["baseColor"] ?? "#ffffff");
        float opacity = (float?)d["opacity"] ?? 1f;
        var tex = d["texture"] != null
            ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{(string)d["texture"]}") : null;

        if (kind.StartsWith("Unlit / Additive"))
        {
            // The legacy additive shader outputs 2 × tint × texture.
            m.SetColor("_TintColor", new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, opacity * 0.5f));
            m.renderQueue = 3000;
            return;
        }
        if (kind.StartsWith("Unlit"))
        {
            m.color = color;
            if (tex) m.mainTexture = tex;
            return;
        }

        m.color = new Color(color.r, color.g, color.b, opacity);
        if (tex) m.mainTexture = tex;
        m.SetFloat("_Metallic", (float?)d["metallic"] ?? 0f);
        m.SetFloat("_Glossiness", 1f - ((float?)d["roughness"] ?? 1f));

        if (d["emission"] != null)
        {
            float k = (float?)d["emissionIntensity"] ?? 1f;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex((string)d["emission"]) * k);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }

        bool transparent = kind.Contains("Transparent") || opacity < 1f;
        SetStandardFade(m, transparent);
    }

    internal static void SetStandardFade(Material m, bool fade)
    {
        m.SetFloat("_Mode", fade ? 2 : 0);
        m.SetInt("_SrcBlend", (int)(fade ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.One));
        m.SetInt("_DstBlend", (int)(fade ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
        m.SetInt("_ZWrite", fade ? 0 : 1);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (fade) m.EnableKeyword("_ALPHABLEND_ON"); else m.DisableKeyword("_ALPHABLEND_ON");
        m.renderQueue = fade ? 3000 : -1;
    }

    // Point each FBX's embedded M_* materials at the generated palette materials.
    static void RemapModelMaterials(Dictionary<string, Material> mats)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            var existing = mi.GetExternalObjectMap();
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                if (!mats.TryGetValue(embedded.name, out var target)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name);
                if (existing.TryGetValue(id, out var cur) && cur == target) continue;
                mi.AddRemap(id, target);
                changed = true;
            }
            if (changed) mi.SaveAndReimport();
        }
    }

    static int BuildPrefabs(JArray assets)
    {
        int count = 0;
        foreach (JObject a in assets)
        {
            var name = (string)a["name"];
            if (name == "MaterialLibrary") continue;
            var fbx = $"{Root}/{(string)a["fbx"]}";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogWarning($"[OtherSide] Missing model {fbx}"); continue; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.position = Vector3.zero;
            BakeAxes(go, name);

            AddCollider(go, name, a);
            AddLight(go, name);
            if (name == "BombableWall") go.transform.Find("Rubble")?.gameObject.SetActive(false);

            var dir = $"{PrefabsDir}/{(string)a["folder"]}";
            EnsureFolder(dir);
            PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{name}.prefab");
            Object.DestroyImmediate(go);
            count++;
        }
        return count;
    }

    // Imported roots carry the file's Z-up rotation, and the importer's handedness flip mirrors X
    // (LeafL ends up at +X). Bake root rotation + an X mirror into new meshes so every prefab
    // matches the pack's contract: Y up, front +Z, LeafL at -X, all transforms identity.
    static void BakeAxes(GameObject go, string name)
    {
        var root = go.transform;
        var a = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * Matrix4x4.Rotate(root.localRotation);
        var aInv = a.inverse;
        root.localRotation = Quaternion.identity;

        var meshPath = $"{Root}/Meshes/{name}.asset";
        EnsureFolder($"{Root}/Meshes");
        var existing = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().ToDictionary(m => m.name);
        bool created = existing.Count == 0;
        var used = new HashSet<string>();

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root)
            {
                t.localPosition = a.MultiplyPoint3x4(t.localPosition);
                t.localRotation = (a * Matrix4x4.Rotate(t.localRotation) * aInv).rotation;
            }
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var baked = Transformed(mf.sharedMesh, a);
            baked.name = t.name;
            for (int n = 1; !used.Add(baked.name); n++) baked.name = $"{t.name}_{n}";
            if (existing.TryGetValue(baked.name, out var target))
            {
                EditorUtility.CopySerialized(baked, target);
                Object.DestroyImmediate(baked);
            }
            else
            {
                target = baked;
                if (created) { AssetDatabase.CreateAsset(target, meshPath); created = false; }
                else AssetDatabase.AddObjectToAsset(target, meshPath);
                existing[target.name] = target;
            }
            mf.sharedMesh = target;
        }
    }

    static Mesh Transformed(Mesh src, Matrix4x4 a)
    {
        var m = Object.Instantiate(src);
        m.vertices = src.vertices.Select(v => a.MultiplyPoint3x4(v)).ToArray();
        m.normals = src.normals.Select(n => a.MultiplyVector(n).normalized).ToArray();
        if (src.tangents.Length > 0)
            m.tangents = src.tangents.Select(t => { var v = a.MultiplyVector(t); return new Vector4(v.x, v.y, v.z, -t.w); }).ToArray();
        for (int s = 0; s < m.subMeshCount; s++)   // mirror → reverse winding
        {
            var tris = m.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            m.SetTriangles(tris, s);
        }
        m.RecalculateBounds();
        return m;
    }

    // The runtime index the game loads from Resources (EscapeOffice.Art).
    static void BuildCatalog(JArray assets)
    {
        const string path = "Assets/Resources/ArtCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<EscapeOffice.ArtCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EscapeOffice.ArtCatalog>();
            EnsureFolder("Assets/Resources");
            AssetDatabase.CreateAsset(catalog, path);
        }

        catalog.prefabs = Load<GameObject>("t:Prefab", PrefabsDir);
        catalog.materials = Load<Material>("t:Material", MaterialsDir);
        catalog.sprites = Load<Sprite>("t:Sprite", Root + "/Textures");
        catalog.clips = Load<AudioClip>("t:AudioClip", Root + "/Audio");
        catalog.symbols = Load<Texture2D>("t:Texture2D", Root + "/Textures/Symbols");
        catalog.codeFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/ChakraPetch-Bold.ttf");
        catalog.titleFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/ChakraPetch-SemiBold.ttf");
        catalog.uiFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Barlow-SemiBold.ttf");

        var byTheme = new SortedDictionary<string, List<string>>();
        foreach (JObject a in assets)
            foreach (var theme in a["themes"] ?? new JArray())
            {
                if (!byTheme.TryGetValue((string)theme, out var list)) byTheme[(string)theme] = list = new List<string>();
                list.Add((string)a["name"]);
            }
        catalog.decor = byTheme.Select(kv => new EscapeOffice.ArtCatalog.ThemeDecor { theme = kv.Key, prefabs = kv.Value }).ToList();

        EditorUtility.SetDirty(catalog);
    }

    static List<T> Load<T>(string filter, string folder) where T : Object =>
        AssetDatabase.FindAssets(filter, new[] { folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null)
            .OrderBy(a => a.name)
            .ToList();

    static void AddCollider(GameObject go, string name, JObject a)
    {
        if (name == "Wall_Full" || name == "BombableWall") Box(go, new Vector3(1f, 1.35f, 1f));
        else if (name.StartsWith("Door_")) Box(go, new Vector3(1f, 1.35f, 0.3f));
        else if (name == "Laser_Beams_1m") Box(go, new Vector3(1f, 1f, 0.3f));
        else if (name == "Fire_Tile") Box(go, new Vector3(1f, 1f, 1f));
        else if (name.StartsWith("Player_"))
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = 0.28f; c.height = 1.1f; c.center = new Vector3(0, 0.55f, 0);
        }
        else if (name.StartsWith("Decor_") && name != "Decor_CafeTable")
        {
            var b = RendererBounds(go);
            Box(go, new Vector3(Mathf.Min(b.size.x, 0.9f), b.size.y, Mathf.Min(b.size.z, 0.9f)), b.center.x, b.center.z);
        }
    }

    static void Box(GameObject go, Vector3 size, float cx = 0f, float cz = 0f)
    {
        var c = go.AddComponent<BoxCollider>();
        c.size = size;
        c.center = new Vector3(cx, size.y * 0.5f, cz);
    }

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Point lights from manifest.json "light".
    static void AddLight(GameObject go, string name)
    {
        switch (name)
        {
            case "Player_A":
            // 1.1 in the prototype; Unity's falloff burns out nearby wall tops at that.
            case "Player_B": Light(go.transform.Find("Lamp_Anchor") ?? go.transform, "#ffe2b8", 0.6f, 7.5f, 0f); break;
            case "Boss_Drone": Light(go.transform.Find("Hover") ?? go.transform, "#ff3030", 0.9f, 3.2f, 0f); break;
            case "Pickup_Bomb": Light(go.transform, "#ffb030", 0.6f, 2.5f, 0.55f); break;
            case "Pickup_Key": Light(go.transform, "#ffe08a", 0.6f, 2.5f, 0.55f); break;
            case "Fire_Tile": Light(go.transform, "#ff8a2a", 2.2f, 4.5f, 1.1f); break;
            case "Laser_Beams_1m": Light(go.transform, "#ff2a4a", 0.8f, 3.5f, 0.6f); break;
        }
    }

    static void Light(Transform parent, string hex, float intensity, float range, float y)
    {
        var l = new GameObject("PointLight").AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.localPosition = new Vector3(0, y, 0);
        l.type = LightType.Point;
        l.color = Hex(hex);
        l.intensity = intensity;
        l.range = range;
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
