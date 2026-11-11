using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BallUIController : MonoBehaviour
{
    public BallMove ballMove;
    public Slider speedSlider;
    public Slider waitSlider;
    public TextMeshProUGUI speedTextTMP;
    public TextMeshProUGUI waitTextTMP;
    public TMP_Dropdown easeDropdownTMP;
    void Start()
    {
        if (ballMove == null) ballMove = FindAnyObjectByType<BallMove>();
        if (ballMove == null)
        {
            Debug.LogWarning("[BallUIController] 没有找到 BallMove，请把小球脚本拖进来。");
            return;
        }

        // 初始化UI值和小球同步
        if (speedSlider != null)
        {
            speedSlider.value = ballMove.moveSpeed;
            speedSlider.onValueChanged.AddListener(OnSpeedChange);
        }
        if (waitSlider != null)
        {
            waitSlider.value = ballMove.waitTime;
            waitSlider.onValueChanged.AddListener(OnWaitChange);
        }
        if (easeDropdownTMP != null)
        {
            easeDropdownTMP.value = (int)ballMove.easeType;
            easeDropdownTMP.onValueChanged.AddListener(OnEaseChange);
        }
        OnSpeedChange(ballMove.moveSpeed);
        OnWaitChange(ballMove.waitTime);
    }

    void OnSpeedChange(float value)
    {
        ballMove.SetSpeed(value);
        string s = $"{value:F2}";
        if (speedTextTMP != null) speedTextTMP.text = s;
    }

    void OnWaitChange(float value)
    {
        ballMove.SetWaitTime(value);
        string s = $"{value:F2} s";
        if (waitTextTMP != null) waitTextTMP.text = s;
    }

    void OnEaseChange(int index)
    {
        ballMove.SetEaseType((EaseType)index);
    }
}
