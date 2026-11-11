using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NaiDan
{
    /// <summary>
    /// 程序化生成"奶蛋"：蛋形身体 + 贴面分层眼睛（眼窝 / 瞳孔 / 高光）+ 贴面弧线嘴。
    ///
    /// 关键：眼睛和嘴不是内嵌的球体，而是"贴着蛋面"的薄片——顶点高度统一由
    /// 蛋面方程 SurfaceZ(x,y) 决定，再按所属层抬高一点点。
    /// 原因：蛋面在眼睛那块高度上起伏约 0.05，和眼睛自身尺寸同一量级，
    /// 用球体拼装必然一半陷进脑袋里、一半浮在外面。
    ///
    /// 用法：
    ///   var go = NaiDanBuilder.Build();
    ///   var go = NaiDanBuilder.Build(parent, bodyMat, socketMat, pupilMat, highlightMat, mouthMat);
    /// </summary>
    public static class NaiDanBuilder
    {
        // ---------- 蛋体 ----------
        public const float BodyRadius = 0.50f;   // 赤道半径
        public const float HeightRatio = 1.09f;  // 半高 / 赤道半径
        public const float Taper = 0.16f;        // 上窄下宽的强度
        public const float HalfHeight = BodyRadius * HeightRatio;

        // ---------- 配色 ----------
        public static readonly Color BodyColor = new Color(0.97f, 0.70f, 0.20f);      // 蛋黄色
        public static readonly Color SocketColor = new Color(0.27f, 0.32f, 0.17f);    // 眼窝：暗橄榄
        public static readonly Color PupilColor = new Color(0.02f, 0.02f, 0.022f);    // 近黑瞳孔
        public static readonly Color HighlightColor = new Color(1.00f, 1.00f, 1.00f); // 眼睛高光
        public static readonly Color MouthColor = new Color(0.33f, 0.18f, 0.05f);     // 深棕嘴线

        // ---------- 五官尺寸 ----------
        private const float EyeX = 0.130f;        // 眼睛水平中心（左右对称）
        private const float EyeY = 0.299f;        // 眼睛高度
        private const float EyeAspect = 1.08f;    // 眼睛纵向拉伸
        private const float EyeRimR = 0.043f;     // 眼窝外圈半径
        private const float EyeRimH = 0.013f;     // 眼窝凸起高度（凸透镜感）
        private const float PupilX = 0.019f;      // 瞳孔半宽
        private const float PupilY = 0.022f;      // 瞳孔半高
        private const float PupilLift = 0.0008f;  // 瞳孔浮在眼窝之上
        private const float PupilN = 3.4f;        // 瞳孔超椭圆指数（2=椭圆，越大越方）
        private const float HiOffX = 0.005f;      // 高光相对眼心的偏移：必须落在眼窝内、瞳孔外，
        private const float HiOffY = 0.034f;      // 否则会被瞳孔的面盖住（蛋面起伏和眼睛尺寸同量级）
        private const float HiR = 0.009f;         // 高光半径
        private const float HiLift = 0.0010f;     // 高光浮在眼窝之上

        private const float MouthY = 0.188f;      // 嘴的高度
        private const float MouthHW = 0.085f;     // 嘴半宽
        private const float MouthSag = 0.016f;    // 弧线弧度
        private const float MouthTh = 0.010f;     // 嘴线粗细
        private const float MouthLift = 0.004f;   // 嘴浮出蛋面

        // ---------- 网格细分 ----------
        private const int BodyRing = 40;
        private const int BodySeg = 60;
        private const int RimRing = 4;
        private const int RimSeg = 56;
        private const int PupilSeg = 72;
        private const int HiSeg = 28;
        private const int MouthSeg = 25;

        // 蛋面最大截面半径（用于归一化，使赤道处半径恰好 = BodyRadius）
        private static readonly float Rmax;

        static NaiDanBuilder()
        {
            float a = Taper;
            float uStar = (1f - Mathf.Sqrt(1f + 8f * a * a)) / (4f * a);
            Rmax = EggAxisR(uStar);
        }

        // ---------- 对外入口 ----------

        /// <summary>生成奶蛋模型，返回根节点"奶蛋"。</summary>
        public static GameObject Build(Transform parent = null,
                                       Material bodyMat = null,
                                       Material socketMat = null,
                                       Material pupilMat = null,
                                       Material highlightMat = null,
                                       Material mouthMat = null)
        {
            var root = new GameObject("奶蛋");
            if (parent != null) root.transform.SetParent(parent, false);

            // 身体
            var body = new GameObject("身体");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = EnsureMesh(CreateEggMesh(), "奶蛋_身体");
            body.AddComponent<MeshRenderer>().sharedMaterial =
                bodyMat != null ? bodyMat : DefaultMaterial(BodyColor, 0.30f);
            body.AddComponent<SphereCollider>().radius = BodyRadius;

            // 眼睛：左右各一个物体，mesh 顶点自带坐标，所以物体本身不做位移
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                var eye = new GameObject(left ? "眼睛_左" : "眼睛_右");
                eye.transform.SetParent(root.transform, false);
                eye.AddComponent<MeshFilter>().sharedMesh =
                    EnsureMesh(CreateEyeMesh(side * EyeX), left ? "奶蛋_眼_左" : "奶蛋_眼_右");
                eye.AddComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    socketMat != null ? socketMat : DefaultMaterial(SocketColor, 0.35f),
                    pupilMat != null ? pupilMat : DefaultMaterial(PupilColor, 0.55f),
                    highlightMat != null ? highlightMat : DefaultHighlightMaterial(),
                };
            }

            // 嘴
            var mouth = new GameObject("嘴");
            mouth.transform.SetParent(root.transform, false);
            mouth.AddComponent<MeshFilter>().sharedMesh = EnsureMesh(CreateMouthLineMesh(), "奶蛋_嘴");
            mouth.AddComponent<MeshRenderer>().sharedMaterial =
                mouthMat != null ? mouthMat : DefaultMaterial(MouthColor, 0.10f);

            return root;
        }

        /// <summary>运行时兜底材质：URP Lit 优先，Standard 兜底。</summary>
        public static Material DefaultMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        /// <summary>眼睛高光材质：白色 + 自发光，保证任何光照下都是亮点。</summary>
        public static Material DefaultHighlightMaterial()
        {
            var mat = DefaultMaterial(HighlightColor, 1f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", HighlightColor);
            return mat;
        }

        // ---------- 蛋面方程 ----------

        // 旋转曲面母线的归一化半径：r(u) = √(1−u²)·(1−Taper·u)，u∈[−1,1]
        private static float EggAxisR(float u)
        {
            return Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - Taper * u);
        }

        // 高度 y 处的截面半径
        private static float RadiusAtY(float y)
        {
            return EggAxisR(Mathf.Clamp(y / HalfHeight, -1f, 1f)) / Rmax * BodyRadius;
        }

        // 蛋面在 (x, y) 处的 z（正面，+Z）
        private static float SurfaceZ(float x, float y)
        {
            float r = RadiusAtY(y);
            return Mathf.Sqrt(Mathf.Max(0f, r * r - x * x));
        }

        // 眼窝凸起：以眼心为中心的凸透镜，边缘归零
        private static float RimLift(float x, float y, float cx)
        {
            float dx = x - cx;
            float dy = (y - EyeY) / EyeAspect;
            float k = 1f - (dx * dx + dy * dy) / (EyeRimR * EyeRimR);
            return k > 0f ? EyeRimH * Mathf.Pow(k, 0.7f) : 0f;
        }

        // 蛋面法线（数值差分，够用且不会在极点退化）
        private static Vector3 FaceNormal(float x, float y)
        {
            const float e = 6e-4f;
            float zx = (SurfaceZ(x + e, y) - SurfaceZ(x - e, y)) / (2f * e);
            float zy = (SurfaceZ(x, y + e) - SurfaceZ(x, y - e)) / (2f * e);
            return new Vector3(-zx, -zy, 1f).normalized;
        }

        // ---------- 网格生成 ----------

        /// <summary>蛋形身体：旋转曲面，法线解析求解。</summary>
        public static Mesh CreateEggMesh(int ring = BodyRing, int seg = BodySeg)
        {
            var verts = new List<Vector3>((ring + 1) * seg);
            var normals = new List<Vector3>((ring + 1) * seg);
            var tris = new List<int>(ring * seg * 6);

            for (int i = 0; i <= ring; i++)
            {
                float th = Mathf.PI * i / ring;
                float u = Mathf.Cos(th);
                float r = EggAxisR(u) / Rmax * BodyRadius;
                float y = u * HalfHeight;

                // 法线 ∝ (cosφ, −dR/dy, sinφ)
                float dr = (EggAxisR(Mathf.Min(1f, u + 1e-3f)) - EggAxisR(Mathf.Max(-1f, u - 1e-3f))) / 2e-3f;
                float ry = dr / Rmax * BodyRadius / HalfHeight;

                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Mathf.PI * j / seg;
                    float cs = Mathf.Cos(ph);
                    float sn = Mathf.Sin(ph);
                    verts.Add(new Vector3(r * cs, y, r * sn));
                    normals.Add(new Vector3(cs, -ry, sn).normalized);
                }
            }

            for (int i = 0; i < ring; i++)
            {
                // 两极处一圈顶点重合，其中一个三角形面积为零，直接跳过
                bool topPole = (i == 0);
                bool botPole = (i == ring - 1);
                for (int j = 0; j < seg; j++)
                {
                    int a = i * seg + j;
                    int b = i * seg + (j + 1) % seg;
                    int c = (i + 1) * seg + j;
                    int d = (i + 1) * seg + (j + 1) % seg;
                    if (!topPole) { tris.Add(a); tris.Add(b); tris.Add(d); }
                    if (!botPole) { tris.Add(a); tris.Add(d); tris.Add(c); }
                }
            }

            return FinishMesh("NaiDanEgg", verts, normals, tris);
        }

        /// <summary>
        /// 单只眼睛：3 个子网格 = 眼窝(0) / 瞳孔(1) / 高光(2)，全部贴在蛋面上。
        /// </summary>
        public static Mesh CreateEyeMesh(float cx, int rimRing = RimRing, int rimSeg = RimSeg)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var rim = new List<int>();
            var pupil = new List<int>();
            var hi = new List<int>();

            // 眼窝：椭圆薄片，中间凸
            AddDisc(verts, normals, rim,
                cx, EyeY, EyeRimR, EyeRimR * EyeAspect, rimRing, rimSeg, 2f,
                (x, y) => RimLift(x, y, cx));

            // 瞳孔：超椭圆方片，压在眼窝上再抬一点，避免 z-fighting
            AddDisc(verts, normals, pupil,
                cx, EyeY, PupilX, PupilY * EyeAspect, 1, PupilSeg, PupilN,
                (x, y) => RimLift(x, y, cx) + PupilLift);

            // 高光：眼窝内偏上的小圆点
            AddDisc(verts, normals, hi,
                cx + HiOffX, EyeY + HiOffY, HiR, HiR, 1, HiSeg, 2f,
                (x, y) => RimLift(x, y, cx) + HiLift);

            var mesh = new Mesh { name = "NaiDanEye" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(rim, 0);
            mesh.SetTriangles(pupil, 1);
            mesh.SetTriangles(hi, 2);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>嘴：贴着蛋面、两端收细的弧线薄片。</summary>
        public static Mesh CreateMouthLineMesh(float halfWidth = MouthHW,
                                               float thickness = MouthTh,
                                               float sag = MouthSag,
                                               int segments = MouthSeg)
        {
            var verts = new List<Vector3>(segments * 2);
            var normals = new List<Vector3>(segments * 2);
            var tris = new List<int>((segments - 1) * 6);

            for (int i = 0; i < segments; i++)
            {
                float t = -1f + 2f * i / (segments - 1);
                float px = t * halfWidth;
                float py = MouthY + sag * (1f - t * t);

                // 弧线切线 (1, dy/dx)，法向与之垂直；taper 让两端收细
                float slope = -2f * sag * t / halfWidth;
                float len = Mathf.Sqrt(1f + slope * slope);
                // 下限 0.15：两端收细但不收成零，否则端点两顶点重合出零面积面
                float taper = Mathf.Max(0.15f, Mathf.Min(1f, (1f - Mathf.Abs(t)) * 4f));
                float nx = -slope / len * thickness * 0.5f * taper;
                float ny = 1f / len * thickness * 0.5f * taper;

                AddVert(verts, normals, px + nx, py + ny);
                AddVert(verts, normals, px - nx, py - ny);
            }

            for (int i = 0; i < segments - 1; i++)
            {
                int a = i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 3);
                tris.Add(a); tris.Add(a + 3); tris.Add(a + 2);
            }

            return FinishMesh("NaiDanMouth", verts, normals, tris);
        }

        // ---------- 内部工具 ----------

        // 以 (cx, cy) 为中心加一片薄片；n=2 是椭圆，n>2 是圆角方（超椭圆）
        private static void AddDisc(List<Vector3> verts, List<Vector3> normals, List<int> tris,
                                    float cx, float cy, float rx, float ry,
                                    int ring, int seg, float n,
                                    System.Func<float, float, float> lift)
        {
            int b = verts.Count;
            AddVert(verts, normals, cx, cy, lift(cx, cy));

            for (int i = 1; i <= ring; i++)
            {
                float rr = (float)i / ring;
                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Mathf.PI * j / seg;
                    float cs = Mathf.Cos(ph);
                    float sn = Mathf.Sin(ph);
                    float k = Mathf.Pow(Mathf.Pow(Mathf.Abs(cs), n) + Mathf.Pow(Mathf.Abs(sn), n), -1f / n);
                    float x = cx + rr * k * cs * rx;
                    float y = cy + rr * k * sn * ry;
                    AddVert(verts, normals, x, y, lift(x, y));
                }
            }

            for (int j = 0; j < seg; j++)
            {
                tris.Add(b);
                tris.Add(b + 1 + j);
                tris.Add(b + 1 + (j + 1) % seg);
            }

            for (int i = 1; i < ring; i++)
            {
                int inner = b + 1 + (i - 1) * seg;
                int outer = b + 1 + i * seg;
                for (int j = 0; j < seg; j++)
                {
                    int j2 = (j + 1) % seg;
                    tris.Add(inner + j); tris.Add(outer + j); tris.Add(outer + j2);
                    tris.Add(inner + j); tris.Add(outer + j2); tris.Add(inner + j2);
                }
            }
        }

        private static void AddVert(List<Vector3> verts, List<Vector3> normals, float x, float y, float lift)
        {
            verts.Add(new Vector3(x, y, SurfaceZ(x, y) + lift));
            normals.Add(FaceNormal(x, y));
        }

        private static void AddVert(List<Vector3> verts, List<Vector3> normals, float x, float y)
        {
            AddVert(verts, normals, x, y, MouthLift);
        }

        private static Mesh FinishMesh(string name, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 编辑模式下把 mesh 存成资产，否则保存场景时引用会丢
        private static Mesh EnsureMesh(Mesh mesh, string assetName)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                const string folder = "Assets/NaiDan/Meshes";
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder("Assets/NaiDan", "Meshes");

                string path = folder + "/" + assetName + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    existing.Clear();
                    existing.SetVertices(new List<Vector3>(mesh.vertices));
                    existing.SetNormals(new List<Vector3>(mesh.normals));
                    existing.subMeshCount = mesh.subMeshCount;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                        existing.SetTriangles(new List<int>(mesh.GetTriangles(i)), i);
                    existing.RecalculateBounds();
                    EditorUtility.SetDirty(existing);
                    return existing;
                }

                AssetDatabase.CreateAsset(mesh, path);
                var created = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (created != null) return created;
            }
#endif
            return mesh;
        }
    }
}
