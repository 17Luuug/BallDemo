using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using NaiDan;

namespace NaiDan.EditorTools
{
    /// <summary>
    /// 编辑器入口：菜单一键生成 / 更新"奶蛋"模型，并把材质保存为资产。
    /// 菜单位置：Tools / 创建奶蛋模型     Tools / 更新场景中的奶蛋模型
    /// </summary>
    public static class NaiDanBuilderEditor
    {
        const string MaterialDir = "Assets/NaiDan/Materials";

        [MenuItem("Tools/创建奶蛋模型")]
        public static void CreateNaiDan()
        {
            var go = BuildWithAssets();
            go.transform.position = new Vector3(0f, NaiDanBuilder.HalfHeight, 0f); // 底部落在 y=0

            Undo.RegisterCreatedObjectUndo(go, "创建奶蛋模型");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            Debug.Log("[奶蛋] 模型已创建于场景原点，材质保存在 " + MaterialDir, go);
        }

        /// <summary>把场景里已有的"奶蛋"就地重建为新造型（保留位置/旋转/缩放）。</summary>
        [MenuItem("Tools/更新场景中的奶蛋模型")]
        public static void UpdateNaiDan()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            int count = 0;

            foreach (var root in roots)
            {
                if (root.name != "奶蛋") continue;
                var t = root.transform;
                var pos = t.position;
                var rot = t.rotation;
                var scl = t.localScale;

                Undo.DestroyObjectImmediate(root);
                var go = BuildWithAssets();
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = scl;
                Undo.RegisterCreatedObjectUndo(go, "更新奶蛋模型");
                count++;
            }

            AssetDatabase.SaveAssets();
            if (count == 0)
                Debug.LogWarning("[奶蛋] 场景根物体中没有找到名为“奶蛋”的模型，请先用 Tools/创建奶蛋模型。");
            else
                Debug.Log($"[奶蛋] 已就地更新 {count} 个奶蛋模型。");
        }

        static GameObject BuildWithAssets()
        {
            EnsureFolder("Assets/NaiDan");
            EnsureFolder(MaterialDir);

            var bodyMat      = SaveMaterial("奶蛋_身体", NaiDanBuilder.BodyColor, 0.75f);
            var socketMat    = SaveMaterial("奶蛋_眼窝", NaiDanBuilder.SocketColor, 0.4f);
            var pupilMat     = SaveMaterial("奶蛋_瞳孔", NaiDanBuilder.PupilColor, 0.35f);
            var mouthMat     = SaveMaterial("奶蛋_嘴", NaiDanBuilder.MouthColor, 0.4f);
            var highlightMat = SaveHighlightMaterial("奶蛋_高光");

            return NaiDanBuilder.Build(null, bodyMat, socketMat, pupilMat, highlightMat, mouthMat);
        }

        static Material SaveMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // 已存在则刷新颜色，保证与配色常量一致
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", color);
                else existing.color = color;
                if (existing.HasProperty("_Smoothness")) existing.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var mat = NaiDanBuilder.DefaultMaterial(color, smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material SaveHighlightMaterial(string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = NaiDanBuilder.DefaultHighlightMaterial();
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
