using UnityEngine;
using UnityEngine.UI;
//（本段代码全程使用AI制作，无任何手搓，不要误会）
// 点击 UI 上的 Music 按钮播放奶龙大笑
public class NaiDanLaugh : MonoBehaviour
{
    [Header("大笑音频（留空则自动读 Resources/Audio/nailong_laugh）")]
    public AudioClip laughClip;

    [Header("Music 按钮（留空则按名字自动查找）")]
    public Button musicButton;

    [Range(0f, 1f)] public float volume = 1f;

    AudioSource _src;

    // 场景里没挂这个组件时，进 Play 自动建一个，不用手动摆
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<NaiDanLaugh>() != null) return;
        new GameObject("NaiDanLaugh").AddComponent<NaiDanLaugh>();
    }

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        if (_src == null) _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.spatialBlend = 0f; // 2D 声音，不随距离衰减
        if (laughClip == null) laughClip = Resources.Load<AudioClip>("Audio/nailong_laugh");
    }

    void Start()
    {
        if (musicButton == null) musicButton = FindButtonByName("Music");
        if (musicButton != null)
        {
            musicButton.onClick.AddListener(Play);
        }
        else
        {
            Debug.LogWarning("[NaiDanLaugh] 没找到名为 Music 的按钮，请把按钮拖到 Music Button 字段。");
        }
    }

    static Button FindButtonByName(string n)
    {
        Button[] all = FindObjectsByType<Button>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == n) return all[i];
        return null;
    }

    public void Play()
    {
        if (laughClip == null)
        {
            Debug.LogWarning("[NaiDanLaugh] 没有音频，请确认 Assets/Resources/Audio/nailong_laugh.mp3 已导入。");
            return;
        }
        if (_src.isPlaying) _src.Stop(); // 连点时不叠音，从头重播
        _src.clip = laughClip;
        _src.volume = volume;
        _src.Play();
    }
}
