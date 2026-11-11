using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

// STS 结局演出：
//   敌人（FeiDoDo）血量归零 → 屏幕碎裂 → 全屏苦夏图 → 关羽之歌 → 播完/左键 → 退出运行
//   英雄（NaiLong）血量归零 → 春泥Dj → 屏幕正中逐段浮现台词 → 结束后血条回满
public class STSEnding : MonoBehaviour
{
    [Header("敌人败北")]
    public string enemyImage = "kuxia.jpg";
    public string enemyMusicRes = "Audio/guanyu";
    public AudioClip enemyMusic;
    public float imageFade = 1.2f;

    [Header("英雄败北")]
    public string heroBgVideo = "hero_bg.mp4";           // 背景视频（StreamingAssets 下）
    public bool heroBgLoop = true;                       // 台词很长，视频必须循环才不会黑屏
    public AspectRatioFitter.AspectMode bgFit = AspectRatioFitter.AspectMode.EnvelopeParent;
    public string heroBgImage = "hero_bg.jpg";           // 视频缺失时的兜底静态图
    public float textDim = 0.25f; // 背景上压一层暗色，保证白字看得清
    public string heroMusicRes = "Audio/chunni";
    public AudioClip heroMusic;
    public float lineFadeIn = 0.5f;
    public float lineHold = 1.4f;
    public float lineFadeOut = 0.45f;
    public float fontSize = 46f;
    public bool verticalText = true;

    [Header("碎屏参数")]
    public float crackTime = 1.1f;
    public float breakTime = 0.9f;
    public int mainCuts = 7;

    static readonly string[] Lines =
    {
        "我不能死，不能死！", "我还有强敌未收拾", "还有罪孽未还清", "还有错误未纠正！",
        "我怎可以倒下？", "我不能倒下！", "同伴们需要我", "龙族需要我！",
        "需要我奶龙！", "所以……", "哪怕再累，再是疲乏不堪", "哪怕沉眠于墓中，哪怕是死了",
        "脑袋都被砍下来！", "我奶龙啊……", "也绝不能倒下！", "我要站起来",
        "我要……再战！", "三气归来！"
    };

    bool _busy;
    bool _bgShown;
    VideoPlayer _bgVp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<STSEnding>() != null) return;
        var go = new GameObject("STSEnding");
        go.AddComponent<STSEnding>();
        DontDestroyOnLoad(go); // 跨场景保留：从 Ball 转场过来后仍要工作
    }

    void Start() => StartCoroutine(Watch());

    IEnumerator Watch()
    {
        var wait = new WaitForSecondsRealtime(0.25f);
        while (true)
        {
            if (!_busy)
            {
                if (AnyDead<EnemyView>()) { _busy = true; yield return StartCoroutine(EnemyEnding()); }
                else if (AnyDead<HeroView>()) { _busy = true; yield return StartCoroutine(HeroEnding()); _busy = false; }
            }
            yield return wait;
        }
    }

    static bool AnyDead<T>() where T : CombatantView
    {
        var all = FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
            if (all[i].MaxHealth > 0 && all[i].CurrentHealth <= 0) return true;
        return false;
    }

    // ---------- 敌人败北 ----------
    IEnumerator EnemyEnding()
    {
        var shatter = gameObject.AddComponent<ScreenShatter>();
        shatter.crackTime = crackTime;
        shatter.breakTime = breakTime;
        shatter.mainCuts = mainCuts;
        yield return StartCoroutine(shatter.Play());

        var canvas = shatter.Fx;
        yield return StartCoroutine(ShowFullImage(canvas, enemyImage, imageFade));

        var src = PlayMusic(enemyMusic, enemyMusicRes);
        yield return new WaitForSecondsRealtime(0.4f);

        float t = 0f;
        while (true)
        {
            if (MouseDown()) break;
            if (src != null) { if (!src.isPlaying) break; }
            else { t += Time.unscaledDeltaTime; if (t > 20f) break; } // 没音乐就等点击（上限保护）
            yield return null;
        }

        StopPlaying();
    }

    IEnumerator ShowFullImage(Canvas canvas, string file, float fade)
    {
        if (canvas == null || string.IsNullOrEmpty(file)) yield break;
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, file);
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("[STSEnding] 找不到图片：" + path); yield break; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { Debug.LogWarning("[STSEnding] 读取失败：" + path); yield break; }

        var rt = ScreenShatter.NewRect("EndingImage", canvas.transform, true);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            img.color = new Color(1f, 1f, 1f, t / fade);
            yield return null;
        }
        img.color = Color.white;
    }

    // ---------- 背景视频（静音，只当画面）----------
    IEnumerator ShowFullVideo(Canvas canvas, string file, float fade)
    {
        if (canvas == null || string.IsNullOrEmpty(file)) yield break;
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, file);
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("[STSEnding] 找不到背景视频：" + path); yield break; }

        var holder = ScreenShatter.NewRect("VideoHolder", canvas.transform, true);
        var vGo = new GameObject("BgVideo", typeof(RawImage), typeof(AspectRatioFitter));
        vGo.transform.SetParent(holder, false);
        var raw = vGo.GetComponent<RawImage>();
        var fit = vGo.GetComponent<AspectRatioFitter>();
        fit.aspectMode = bgFit;
        raw.raycastTarget = false;
        raw.color = new Color(1f, 1f, 1f, 0f);

        var vp = canvas.gameObject.AddComponent<VideoPlayer>();
        _bgVp = vp;
        vp.playOnAwake = false;
        vp.source = VideoSource.Url;
        vp.url = path;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.audioOutputMode = VideoAudioOutputMode.None; // 去掉视频原声，只留春泥Dj
        vp.isLooping = heroBgLoop;
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
        vp.Play();
        _bgShown = true;

        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            raw.color = new Color(1f, 1f, 1f, t / fade);
            yield return null;
        }
        raw.color = Color.white;
    }

    // ---------- 英雄败北 ----------
    IEnumerator HeroEnding()
    {
        // 1. 先碎屏（和 HenShin / 敌人败北同一套）
        var shatter = gameObject.AddComponent<ScreenShatter>();
        shatter.crackTime = crackTime;
        shatter.breakTime = breakTime;
        shatter.mainCuts = mainCuts;
        yield return StartCoroutine(shatter.Play());
        var canvas = shatter.Fx;
        Destroy(shatter);

        // 2. 背景视频全屏淡入（失败就用静态图兜底）
        _bgShown = false;
        yield return StartCoroutine(ShowFullVideo(canvas, heroBgVideo, imageFade));
        if (!_bgShown && !string.IsNullOrEmpty(heroBgImage))
            yield return StartCoroutine(ShowFullImage(canvas, heroBgImage, imageFade));
        if (canvas != null && textDim > 0f) MakeDim(canvas.transform, textDim);

        // 3. 音乐
        var src = PlayMusic(heroMusic, heroMusicRes);

        // 4. 逐段台词（最后添加 = 画在背景图之上）
        if (canvas != null)
        {
            var rt = ScreenShatter.NewRect("Lines", canvas.transform, true);
            var txt = rt.gameObject.AddComponent<TextMeshProUGUI>();
            txt.font = FindFont();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = fontSize;
            txt.color = new Color(1f, 1f, 1f, 0f);
            txt.raycastTarget = false;
            txt.outlineColor = new Color32(0, 0, 0, 255);
            txt.outlineWidth = 0.18f;
            txt.text = "";

            foreach (var line in Lines)
                yield return StartCoroutine(FadeLine(txt, verticalText ? Vertical(line) : line));
        }

        if (_bgVp != null)
        {
            _bgVp.Stop();
            var rt = _bgVp.targetTexture;
            Destroy(_bgVp);
            if (rt != null) rt.Release();
            _bgVp = null;
        }
        if (canvas != null) Destroy(canvas.gameObject);

        if (src != null) src.Stop();

        foreach (var h in FindObjectsByType<HeroView>(FindObjectsInactive.Include))
            h.Heal(h.MaxHealth);
    }

    static void MakeDim(Transform p, float a)
    {
        var rt = ScreenShatter.NewRect("Dim", p, true);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.color = new Color(0f, 0f, 0f, a);
        img.raycastTarget = false;
    }

    IEnumerator FadeLine(TMP_Text txt, string s)
    {
        txt.text = s;
        for (float t = 0f; t < lineFadeIn; t += Time.unscaledDeltaTime)
        {
            txt.color = new Color(1f, 1f, 1f, t / lineFadeIn);
            yield return null;
        }
        txt.color = Color.white;
        yield return new WaitForSecondsRealtime(lineHold);
        for (float t = 0f; t < lineFadeOut; t += Time.unscaledDeltaTime)
        {
            txt.color = new Color(1f, 1f, 1f, 1f - t / lineFadeOut);
            yield return null;
        }
        txt.color = new Color(1f, 1f, 1f, 0f);
    }

    static string Vertical(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            if (i < s.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    // 复用场景里已有的 TMP 字体，避免新建文字缺中文字形
    static TMP_FontAsset FindFont()
    {
        var all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
            if (all[i].font != null) return all[i].font;
        return TMP_Settings.defaultFontAsset;
    }

    static AudioSource PlayMusic(AudioClip clip, string res)
    {
        if (clip == null && !string.IsNullOrEmpty(res)) clip = Resources.Load<AudioClip>(res);
        if (clip == null) { Debug.LogWarning("[STSEnding] 音乐没加载到：" + res); return null; }

        var go = new GameObject("EndingMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = false;
        src.volume = 1f;
        src.Play();
        return src;
    }

    static void StopPlaying()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
