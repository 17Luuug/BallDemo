using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 屏幕碎裂：抓屏 → 裂纹从中心蔓延 → 沿纹路裂开飞散
// 通用组件，HenShin 转场和 STS 结局都用它
public class ScreenShatter : MonoBehaviour
{
    [Header("时长（秒）")]
    public float crackTime = 1.1f;
    public float breakTime = 0.9f;

    [Header("裂纹（从中心放射的切割线数量）")]
    public int mainCuts = 7;
    public int maxShards = 100;
    public float minShardArea = 150f; // 过滤细得看不见的碎片

    [Header("抓屏上下颠倒时勾上")]
    public bool flipY = false;

    public Canvas Fx { get; private set; }

    RectTransform _full;
    RawImage _crackImg;
    List<Shard> _shards = new List<Shard>();
    List<Edge> _edges = new List<Edge>();
    static Material _mat;

    class Shard { public GameObject go; public RectTransform rt; public CanvasRenderer cr; public Vector2 center; }
    struct Edge { public Vector2 a, b; public float dist; }
    struct Cut { public Vector2 o, dir; }

    public IEnumerator Play()
    {
        yield return new WaitForEndOfFrame();

        var shot = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        shot.Create();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(shot);

        float W = Screen.width, H = Screen.height;
        var polys = SplitScreen(W, H, BuildCuts(W, H), maxShards);
        CollectEdges(polys, W, H);

        Fx = MakeCanvas();
        MakeBlackBack(Fx.transform);
        MakeShards(Fx.transform, polys, shot, W, H); // 先加：在下层
        _full = MakeFullShot(Fx.transform, shot);    // 后加：盖住碎片
        _crackImg = MakeCrackLayer(Fx.transform);

        _crackImg.color = new Color(1, 1, 1, 0);
        yield return StartCoroutine(GrowCrack(crackTime));

        _full.gameObject.SetActive(false);
        yield return StartCoroutine(BreakShards(breakTime));

        foreach (var s in _shards) Destroy(s.go);
        _shards.Clear();
        Destroy(_crackImg.gameObject);
    }

    // ---------- 切割：用直线把屏幕切成不规则碎片 ----------
    List<Cut> BuildCuts(float W, float H)
    {
        var cuts = new List<Cut>();
        Vector2 c = new Vector2(W * 0.5f, H * 0.5f)
                  + new Vector2(Random.Range(-W * 0.03f, W * 0.03f), Random.Range(-H * 0.03f, H * 0.03f));
        float reach = Mathf.Max(W, H) * 0.5f;

        for (int i = 0; i < mainCuts; i++)
        {
            float a = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / mainCuts;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            // 起点沿方向错开一点，避免所有裂纹精确交于一点
            cuts.Add(new Cut { o = c + dir * reach * Random.Range(0.03f, 0.14f), dir = dir });

            if (Random.value < 0.7f)
            {
                Vector2 p = c + dir * reach * Random.Range(0.25f, 0.7f);
                float da = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.5f, 1.1f);
                Vector2 d2 = Rot(dir, da);
                cuts.Add(new Cut { o = p, dir = d2 });
                if (Random.value < 0.4f)
                    cuts.Add(new Cut { o = p + d2 * reach * 0.2f, dir = Rot(d2, -da * 0.7f) });
            }
        }

        // 蛛网式环向裂纹：垂直于半径切一刀，把放射块再切成环
        for (int ring = 0; ring < 2; ring++)
        {
            float r = (ring == 0 ? 0.32f : 0.62f) * reach;
            int n = Random.Range(2, 5);
            for (int k = 0; k < n; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                cuts.Add(new Cut
                {
                    o = c + dir * r * Random.Range(0.8f, 1.2f),
                    dir = Rot(dir, Mathf.PI * 0.5f + Random.Range(-0.3f, 0.3f))
                });
            }
        }
        return cuts;
    }

    static Vector2 Rot(Vector2 v, float a)
    {
        float c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // 依次用每条切割线（无限直线）裁剪所有多边形，得到一堆凸多边形
    static List<List<Vector2>> SplitScreen(float W, float H, List<Cut> cuts, int maxShards)
    {
        var polys = new List<List<Vector2>>
        {
            new List<Vector2> { new Vector2(0,0), new Vector2(W,0), new Vector2(W,H), new Vector2(0,H) }
        };

        foreach (var cut in cuts)
        {
            if (polys.Count >= maxShards) break;
            Vector2 n = new Vector2(-cut.dir.y, cut.dir.x).normalized;
            float d = Vector2.Dot(n, cut.o);
            var next = new List<List<Vector2>>();
            foreach (var p in polys)
            {
                var a = Clip(p, n, d, true);
                var b = Clip(p, n, d, false);
                if (a.Count >= 3) next.Add(a);
                if (b.Count >= 3) next.Add(b);
            }
            if (next.Count == 0) continue;
            polys = next;
        }
        return polys;
    }

    // Sutherland-Hodgman 半平面裁剪
    static List<Vector2> Clip(List<Vector2> poly, Vector2 n, float d, bool positive)
    {
        var outp = new List<Vector2>();
        int m = poly.Count;
        for (int i = 0; i < m; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % m];
            float fa = Vector2.Dot(n, a) - d, fb = Vector2.Dot(n, b) - d;
            if (!positive) { fa = -fa; fb = -fb; }
            bool inA = fa >= 0f, inB = fb >= 0f;
            if (inA) outp.Add(a);
            if (inA != inB) outp.Add(a + (b - a) * (fa / (fa - fb)));
        }
        return outp;
    }

    // 碎片的内部边 = 屏幕上看到的裂纹（屏幕矩形边不算）
    void CollectEdges(List<List<Vector2>> polys, float W, float H)
    {
        _edges.Clear();
        Vector2 c = new Vector2(W * 0.5f, H * 0.5f);
        float maxD = Mathf.Sqrt(W * W + H * H) * 0.5f;
        const float e = 0.6f;
        foreach (var p in polys)
        {
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % p.Count];
                bool onBorder =
                    (Mathf.Abs(a.x - b.x) < e && (a.x < e || a.x > W - e)) ||
                    (Mathf.Abs(a.y - b.y) < e && (a.y < e || a.y > H - e));
                if (onBorder) continue;
                _edges.Add(new Edge { a = a, b = b, dist = Vector2.Distance((a + b) * 0.5f, c) / maxD });
            }
        }
        _edges.Sort((x, y) => x.dist.CompareTo(y.dist)); // 从中心向外蔓延
    }

    // ---------- 裂纹绘制 ----------
    IEnumerator GrowCrack(float dur)
    {
        int w = Mathf.Clamp(Screen.width / 2, 256, 1024);
        int h = Mathf.Clamp(Screen.height / 2, 256, 1024);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        _crackImg.texture = tex;

        float sx = w / (float)Screen.width, sy = h / (float)Screen.height;
        const int batches = 10;
        int done = 0;
        for (int b = 1; b <= batches; b++)
        {
            int end = Mathf.CeilToInt(_edges.Count * b / (float)batches);
            for (; done < end; done++)
            {
                var ed = _edges[done];
                int width = Mathf.Clamp(Mathf.RoundToInt(3f - 2f * ed.dist), 1, 3);
                var pa = new Vector2(ed.a.x * sx, ed.a.y * sy);
                var pb = new Vector2(ed.b.x * sx, ed.b.y * sy);
                DrawSeg(px, w, h, pa, pb, width + 1, new Color32(0, 0, 0, 130));  // 暗色衬线：裂纹有厚度
                DrawSeg(px, w, h, pa, pb, width, new Color32(255, 255, 255, 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            _crackImg.color = new Color(1, 1, 1, 0.35f + 0.65f * b / batches);
            yield return new WaitForSecondsRealtime(dur / batches);
        }
    }

    // 沿边画一条带锯齿抖动的线（真实裂纹不是笔直的），抖动幅度 <2px，不影响碎片边界
    static void DrawSeg(Color32[] px, int w, int h, Vector2 a, Vector2 b, int r, Color32 col)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / 16f));
        Vector2 dir = (b - a) / Mathf.Max(len, 0.0001f);
        Vector2 perp = new Vector2(-dir.y, dir.x);
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float j = (i == 0 || i == n) ? 0f : Random.Range(-1.6f, 1.6f);
            pts[i] = Vector2.Lerp(a, b, i / (float)n) + perp * j;
        }
        for (int i = 0; i < n; i++) Raster(px, w, h, pts[i], pts[i + 1], r, col);
    }

    static void Raster(Color32[] px, int w, int h, Vector2 a, Vector2 b, int r, Color32 col)
    {
        int x0 = Mathf.RoundToInt(a.x), y0 = Mathf.RoundToInt(a.y);
        int x1 = Mathf.RoundToInt(b.x), y1 = Mathf.RoundToInt(b.y);
        int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Stamp(px, w, h, x0, y0, r, col);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    static void Stamp(Color32[] px, int w, int h, int cx, int cy, int r, Color32 col)
    {
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= h) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= w) continue;
                bool edge = Mathf.Abs(x - cx) == r || Mathf.Abs(y - cy) == r;
                byte a = (byte)(col.a * (edge ? 0.66f : 1f));
                px[y * w + x] = new Color32(col.r, col.g, col.b, a);
            }
        }
    }

    // ---------- 碎片飞散 ----------
    IEnumerator BreakShards(float dur)
    {
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var st = new List<State>();
        foreach (var s in _shards)
        {
            Vector2 outward = (s.center - center).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = Random.insideUnitCircle.normalized;
            st.Add(new State
            {
                s = s, start = s.center,
                vel = outward * Random.Range(140f, 340f) + new Vector2(Random.Range(-60f, 60f), Random.Range(-40f, 160f)),
                spin = Random.Range(-180f, 180f),
                delay = Random.Range(0f, 0.25f)
            });
        }

        for (float t = 0f; t < dur + 0.25f; t += Time.unscaledDeltaTime)
        {
            foreach (var x in st)
            {
                float lt = Mathf.Clamp01((t - x.delay) / dur);
                float e = lt * lt;
                x.s.rt.anchoredPosition = x.start + x.vel * e + new Vector2(0f, -900f * e * e);
                x.s.rt.localEulerAngles = new Vector3(0f, 0f, x.spin * e);
                x.s.cr.SetColor(new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((lt - 0.55f) / 0.45f)));
            }
            yield return null;
        }
    }

    class State { public Shard s; public Vector2 start, vel; public float spin, delay; }

    // ---------- UI 搭建 ----------
    public static Material ShardMat()
    {
        if (_mat == null)
        {
            var sh = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        return _mat;
    }

    public static Canvas MakeCanvas()
    {
        var go = new GameObject("ShatterFX", typeof(Canvas), typeof(CanvasScaler));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 32767;
        go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        return c;
    }

    public static RectTransform NewRect(string name, Transform parent, bool stretch)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = stretch ? Vector2.one : Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static void MakeBlackBack(Transform p)
    {
        var rt = NewRect("Black", p, true);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    void MakeShards(Transform p, List<List<Vector2>> polys, Texture shot, float W, float H)
    {
        var mat = ShardMat();
        foreach (var poly in polys)
        {
            if (poly.Count < 3) continue;
            if (Mathf.Abs(Area(poly)) < minShardArea) continue;
            if (Area(poly) < 0f) poly.Reverse();

            Vector2 c = Vector2.zero;
            foreach (var v in poly) c += v;
            c /= poly.Count;

            var verts = new Vector3[poly.Count];
            var uv = new Vector2[poly.Count];
            for (int i = 0; i < poly.Count; i++)
            {
                verts[i] = new Vector3(poly[i].x - c.x, poly[i].y - c.y, 0f);
                uv[i] = new Vector2(poly[i].x / W, flipY ? 1f - poly[i].y / H : poly[i].y / H);
            }
            var tris = new int[(poly.Count - 2) * 3];
            for (int i = 0, k = 0; i < poly.Count - 2; i++)
            {
                tris[k++] = 0; tris[k++] = i + 1; tris[k++] = i + 2;
            }

            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3>(verts));
            mesh.SetUVs(0, new List<Vector2>(uv));
            mesh.SetTriangles(tris, 0);

            var go = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(p, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = c;

            var cr = go.GetComponent<CanvasRenderer>();
            cr.SetMesh(mesh);
            cr.SetMaterial(mat, shot);
            cr.SetColor(Color.white);

            _shards.Add(new Shard { go = go, rt = rt, cr = cr, center = c });
        }
    }

    static float Area(List<Vector2> p)
    {
        float s = 0f;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            s += a.x * b.y - b.x * a.y;
        }
        return s * 0.5f;
    }

    static RectTransform MakeFullShot(Transform p, Texture shot)
    {
        var rt = NewRect("Full", p, true);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = shot;
        img.raycastTarget = false;
        return rt;
    }

    static RawImage MakeCrackLayer(Transform p)
    {
        var rt = NewRect("Crack", p, true);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }
}
