using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// HenShin 转场：碎屏 → 标题图浮现 → 播视频 → 跳转场景
// 碎屏部分复用通用组件 ScreenShatter
public class HenShinTransition : MonoBehaviour
{
    [Header("视频（StreamingAssets 下的文件名）")]
    public string videoFile = "henshin.mp4";

    [Header("碎完后浮现的标题图（留空则跳过）")]
    public string titleImage = "henshin_title.jpg";
    public float titleFade = 1.5f;
    public float titleHold = 2f;

    [Header("碎屏参数")]
    public float crackTime = 1.1f;
    public float breakTime = 0.9f;
    public int mainCuts = 7;
    public int maxShards = 100;
    public float minShardArea = 150f;
    public bool flipY = false;

    Canvas _fx;
    RectTransform _title;

    public IEnumerator PlayThenLoad(string scene)
    {
        var shatter = gameObject.AddComponent<ScreenShatter>();
        shatter.crackTime = crackTime;
        shatter.breakTime = breakTime;
        shatter.mainCuts = mainCuts;
        shatter.maxShards = maxShards;
        shatter.minShardArea = minShardArea;
        shatter.flipY = flipY;

        yield return StartCoroutine(shatter.Play());
        _fx = shatter.Fx;

        yield return StartCoroutine(ShowTitle());
        yield return StartCoroutine(PlayVideo(scene));
    }

    // ---------- 标题图全屏浮现 ----------
    IEnumerator ShowTitle()
    {
        if (string.IsNullOrEmpty(titleImage) || _fx == null) yield break;

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, titleImage);
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("[HenShin] 找不到标题图：" + path);
            yield break;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
        {
            Debug.LogWarning("[HenShin] 标题图读取失败：" + path);
            yield break;
        }

        var rt = ScreenShatter.NewRect("Title", _fx.transform, true);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        _title = rt;

        for (float t = 0f; t < titleFade; t += Time.unscaledDeltaTime)
        {
            img.color = new Color(1f, 1f, 1f, t / titleFade);
            yield return null;
        }
        img.color = Color.white;
        yield return new WaitForSecondsRealtime(titleHold);
    }

    // ---------- 视频（可点击跳过）----------
    IEnumerator PlayVideo(string scene)
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFile);
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("[HenShin] 找不到视频：" + path + "，直接跳场景。");
            SceneManager.LoadScene(scene);
            yield break;
        }

        if (_title != null) _title.gameObject.SetActive(false); // 标题图让位给视频

        var holder = ScreenShatter.NewRect("VideoHolder", _fx.transform, true);
        var vGo = new GameObject("Video", typeof(RawImage), typeof(AspectRatioFitter));
        vGo.transform.SetParent(holder, false);
        var raw = vGo.GetComponent<RawImage>();
        var fit = vGo.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        raw.color = Color.black;

        var vp = _fx.gameObject.AddComponent<VideoPlayer>();
        var au = _fx.gameObject.AddComponent<AudioSource>();
        vp.playOnAwake = false;
        vp.source = VideoSource.Url;
        vp.url = path;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        vp.SetTargetAudioSource(0, au);
        vp.targetTexture = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGB32);

        vp.Prepare();
        float waited = 0f;
        while (!vp.isPrepared && waited < 8f) { waited += Time.unscaledDeltaTime; yield return null; }

        if (vp.width > 0 && vp.height > 0)
        {
            var rt = new RenderTexture((int)vp.width, (int)vp.height, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            vp.targetTexture = rt;
            fit.aspectRatio = (float)vp.width / vp.height;
        }
        raw.texture = vp.targetTexture;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            raw.color = new Color(1f, 1f, 1f, t / 0.25f);
            yield return null;
        }
        raw.color = Color.white;

        vp.Play();
        yield return new WaitForSecondsRealtime(0.2f);

        long last = -1, total = (long)vp.frameCount;
        float stuck = 0f;
        while (true)
        {
            if (MouseDown()) break;
            if (total > 0 && vp.frame >= total - 1) break;
            if (vp.frame == last) { stuck += Time.unscaledDeltaTime; if (stuck > 1f) break; } else stuck = 0f;
            last = vp.frame;
            yield return null;
        }

        SceneManager.LoadScene(scene);
    }

    static bool MouseDown()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
