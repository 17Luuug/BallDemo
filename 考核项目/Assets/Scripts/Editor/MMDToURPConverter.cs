using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// MMD4Mecanim 的 shader 是内置管线 CG 写的，URP 下不渲染（品红）。
// 这里把 MMD 材质换成 URP 内置 shader，保留主贴图 / 颜色 / 双面 / 透明。
public static class MMDToURPConverter
{
    const string MatDir = "Assets/WBBB/URP_Materials";

    [MenuItem("Tools/MMD 材质一键转 URP")]
    static void ConvertMenu()
    {
        EnsureFolder();

        int count = 0, renderers = 0;

        // 1. 选中的 prefab 资产 / 文件夹
        HashSet<string> prefabPaths = new HashSet<string>();
        List<GameObject> sceneObjs = new List<GameObject>();
        foreach (var go in Selection.gameObjects)
        {
            string p = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(p) && p.EndsWith(".prefab"))
                prefabPaths.Add(p);
            else
                sceneObjs.Add(go);
        }
        foreach (var obj in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(p))
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { p }))
                    prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        foreach (string p in prefabPaths)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(p);
            int n = ConvertTree(contents, false);
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(contents, p);
            PrefabUtility.UnloadPrefabContents(contents);
            count += n;
            Debug.Log($"[MMD→URP] 预制体 {p}：替换 {n} 个材质");
        }

        // 2. 选中的场景物体；什么都没选就处理整个场景
        if (Selection.objects.Length == 0)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                sceneObjs.Add(r.gameObject);
        }

        foreach (var go in sceneObjs)
        {
            if (go == null) continue;
            int n = ConvertTree(go, true);
            count += n;
            if (n > 0) renderers++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (count == 0)
            Debug.Log("[MMD→URP] 没有找到 MMD4Mecanim 材质。请选中带了模型的对象/预制体再执行。");
        else
            Debug.Log($"[MMD→URP] 完成：{count} 个材质已换成 URP（新材质在 {MatDir}）。");
    }

    static int ConvertTree(GameObject root, bool undo)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            n += ConvertRenderer(r, undo);
        return n;
    }

    static int ConvertRenderer(Renderer r, bool undo)
    {
        Material[] mats = r.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < mats.Length; i++)
        {
            Material src = mats[i];
            if (src == null || src.shader == null) continue;
            if (!src.shader.name.StartsWith("MMD4Mecanim")) continue;

            mats[i] = MakeURPMaterial(src, r.name);
            changed = true;
        }

        if (changed)
        {
            if (undo) Undo.RecordObject(r, "MMD 材质转 URP");
            r.sharedMaterials = mats;
            if (undo) EditorUtility.SetDirty(r);
        }
        return changed ? 1 : 0;
    }

    static Material MakeURPMaterial(Material src, string ownerName)
    {
        bool isSky = IsSky(src.name) || IsSky(ownerName);
        Shader sh = Shader.Find(isSky ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (sh == null)
        {
            Debug.LogError("[MMD→URP] 找不到 URP 内置 shader，请确认项目已安装 URP。");
            return src;
        }

        Material mat = new Material(sh) { name = src.name + "_URP" };

        Texture mainTex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
        Color color = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;

        mat.SetTexture("_BaseMap", mainTex);
        mat.SetColor("_BaseColor", color);

        if (!isSky)
        {
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_EnvironmentReflections", 0f);

            if (src.HasProperty("_Emissive"))
            {
                Color em = src.GetColor("_Emissive");
                if (em.maxColorComponent > 0.001f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", em);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
        }

        // 双面：MMD 的 BothFaces 变体；天空球一律双面（相机常在球内）
        bool bothFaces = src.shader.name.Contains("BothFaces");
        mat.SetFloat("_Cull", (bothFaces || isSky) ? (float)CullMode.Off : (float)CullMode.Back);

        bool transparent = src.shader.name.Contains("Transparent") || src.renderQueue >= 3000;
        SetSurface(mat, transparent);

        AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath($"{MatDir}/{Sanitize(mat.name)}.mat"));
        return mat;
    }

    static void SetSurface(Material m, bool transparent)
    {
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);   // 1 = Transparent
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("SHADOWCASTER", false);
        }
        else
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Geometry;
        }
    }

    static bool IsSky(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        string s = n.ToLowerInvariant();
        return s.Contains("天空") || s.Contains("sky") || s.Contains("sora");
    }

    static string Sanitize(string n)
    {
        foreach (char c in new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            n = n.Replace(c, '_');
        return n;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/WBBB")) AssetDatabase.CreateFolder("Assets", "WBBB");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/WBBB", "URP_Materials");
    }
}
