using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 把「结束回合」按钮放到大肥鱼图片上：在 Canvas 下建一条鱼的 Image，按钮挂到鱼身上。
public static class STSFishButtonSetup
{
    const string SpritePath = "Assets/Slay The Spire/Media/UI/大肥鱼.png";
    const string FishName = "大肥鱼";
    const string ButtonName = "EndTurnButtonUI";

    // 想微调就改这几个数，然后重新点一次菜单
    const float FishScale = 1.0f;    // 鱼的整体缩放
    const float FishOffsetX = -190f; // 鱼中心距屏幕右边
    const float FishOffsetY = 200f;  // 鱼中心距屏幕下边
    const float ButtonOffsetY = 30f; // 按钮相对鱼中心的纵向偏移

    [MenuItem("Tools/STS 结束回合按钮放到大肥鱼上")]
    static void Setup()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[大肥鱼] 场景里没有 Canvas，先打开 Slay The Spire 场景。");
            return;
        }

        Sprite fishSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (fishSprite == null)
        {
            Debug.LogError($"[大肥鱼] 找不到 sprite：{SpritePath}，确认图片已导入且 Texture Type = Sprite。");
            return;
        }

        // 1. 鱼
        GameObject fish = GameObject.Find(FishName);
        if (fish != null && fish.transform.parent != canvas.transform) fish = null;
        if (fish == null)
        {
            fish = new GameObject(FishName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(fish, "创建大肥鱼");
            fish.transform.SetParent(canvas.transform, false);
        }

        RectTransform fishRT = fish.GetComponent<RectTransform>();
        Undo.RecordObject(fishRT, "摆放大肥鱼");
        fishRT.anchorMin = new Vector2(1f, 0f);
        fishRT.anchorMax = new Vector2(1f, 0f);
        fishRT.pivot = new Vector2(0.5f, 0.5f);
        fishRT.anchoredPosition = new Vector2(FishOffsetX, FishOffsetY);
        fishRT.sizeDelta = new Vector2(fishSprite.rect.width * FishScale,
                                       fishSprite.rect.height * FishScale);

        Image fishImg = fish.GetComponent<Image>();
        Undo.RecordObject(fishImg, "设置大肥鱼图片");
        fishImg.sprite = fishSprite;
        fishImg.preserveAspect = true;
        fishImg.raycastTarget = false; // 别挡住底下的点击

        // 2. 把结束回合按钮挂到鱼身上
        Transform btn = FindChild(canvas.transform, ButtonName);
        if (btn == null)
        {
            Debug.LogError($"[大肥鱼] Canvas 下没找到按钮 {ButtonName}。");
            return;
        }

        RectTransform btnRT = btn.GetComponent<RectTransform>();
        Vector2 keepSize = btnRT.sizeDelta;
        Undo.RecordObject(btnRT, "移动结束回合按钮");
        Undo.SetTransformParent(btnRT, fishRT, "结束回合按钮挂到大肥鱼");
        btnRT.anchorMin = new Vector2(0.5f, 0.5f);
        btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(0f, ButtonOffsetY);
        btnRT.sizeDelta = keepSize;
        btn.SetAsLastSibling(); // 渲染在鱼之后 = 显示在鱼上面

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[大肥鱼] 完成：鱼 {fishRT.sizeDelta} 放在右下角，按钮已挂在鱼身上（偏移 {ButtonOffsetY}）。");
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
