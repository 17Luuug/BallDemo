using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 点击 Ball 场景的 HenShin 按钮，跳转到 Slay The Spire
public class SceneSwitch : MonoBehaviour
{
    [Header("目标场景名（必须已加入 Build Settings）")]
    public string targetScene = "Slay The Spire";

    [Header("触发按钮（留空则按名字自动查找）")]
    public string buttonName = "HenShin";
    public Button triggerButton;

    static SceneSwitch _inst;
    bool _loading;

    // 进 Play 自动建一个，不用手动摆到场景里
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (_inst != null) return;
        var go = new GameObject("SceneSwitch");
        _inst = go.AddComponent<SceneSwitch>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start() => Bind();

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _loading = false;
        Bind(); // 换场景后按钮是新实例，重新绑
    }

    void Bind()
    {
        if (triggerButton != null) return; // 已绑过（销毁过的引用 == null，会自动重找）
        triggerButton = FindButton(buttonName);
        if (triggerButton == null) return; // 不在 Ball 场景，静默跳过
        triggerButton.onClick.AddListener(Go);
    }

    public void Go()
    {
        if (_loading) return;
        if (string.IsNullOrEmpty(targetScene)) return;
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError("[SceneSwitch] 场景未加入 Build Settings：" + targetScene);
            return;
        }
        _loading = true;

        // 有转场脚本就先演一遍（裂纹 → 碎开 → 视频），演完再跳
        var fx = FindAnyObjectByType<HenShinTransition>();
        if (fx == null) fx = new GameObject("HenShinTransition").AddComponent<HenShinTransition>();
        StartCoroutine(fx.PlayThenLoad(targetScene));
    }

    static Button FindButton(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        Button[] all = FindObjectsByType<Button>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == n) return all[i];
        return null;
    }
}
