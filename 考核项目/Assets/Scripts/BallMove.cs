using UnityEngine;
//枚举声明
#region
// 两边快，中间慢;两边慢，中间快;匀速
public enum EaseType
{
    EaseOutInSine,   
    EaseInOutSine,   
    Linear           
}
#endregion
public class BallMove : MonoBehaviour
{
    //变量
    #region 
    public Transform pointA;
    public Transform pointB;
    public float moveSpeed = 2f;
    public float waitTime = 1f;
    public EaseType easeType = EaseType.EaseOutInSine;
    public bool rollWhileMoving = true;
    public float rollRadius = 0.5f;
    // 曲线强度
    private const float CurveStrength = 0.7f;

    // 运行时动态参数
    private float _t;
    private float _waitTimer;
    private bool _isWaiting;
    // 1:A→B  -1:B→A
    private int _dir = 1;
    #endregion
    //奶龙往返主循环，这里运用了一个AI教的“_t：归一化进度 0~1，唯一的核心状态量”
    #region
    void Update()
    {
        if (pointA == null || pointB == null) return;

        if (_isWaiting)
        {
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= waitTime)
            {
                _isWaiting = false;
                _waitTimer = 0;
            }
            return;
        }
        float dist = Vector3.Distance(pointA.position, pointB.position);
        if (dist < 0.0001f) return;
        float before = GetEaseValue(_t, easeType); 
        // 这一帧开始时的位置比例
        // 除以路径长度,单位“米/秒”；
        _t += _dir * (moveSpeed / dist) * Time.deltaTime;
        // 保留未钳制的值：到达端点时溢出的那一小段也要算进滚动量，
        // 否则每趟都会少滚一点，来回几次后奶蛋的朝向就歪了
        float rawT = _t;
        // 到达端点，转向+等待
        if (_t >= 1f)
        {
            _t = 1f;
            _dir = -1;
            _isWaiting = true;
            _waitTimer = 0f;
        }
        else if (_t <= 0f)
        {
            _t = 0f;
            _dir = 1;
            _isWaiting = true;
            _waitTimer = 0f;
        }
        // 根据选择的缓动类型计算插值t
        float easedT = GetEaseValue(_t, easeType);
        transform.position = Vector3.Lerp(pointA.position, pointB.position, easedT);
        ApplyRoll(GetEaseValue(rawT, easeType) - before, dist);
    }
    #endregion
    //奶龙滚动逻辑，其实看着奶蛋滚起来很神秘
    #region
    // 转角 = 走过距离 / 半径，转轴 = up × 前进方向
    private void ApplyRoll(float deltaEased, float dist)
    {
        if (!rollWhileMoving) return;
        //想要奶蛋不转，关掉rollWhileMoving就好啦
        float moved = Mathf.Abs(deltaEased) * dist;
        if (moved < 1e-6f) return;
        if (moved > rollRadius * 3f) return; 
        // 单帧位移异常（切换缓动曲线等）时不滚，避免突然疯转
        Vector3 forward = (pointB.position - pointA.position).normalized;
        if (deltaEased < 0f) forward = -forward; 
        // 往 A 端走就反向滚
        Vector3 axis = Vector3.Cross(Vector3.up, forward);
        if (axis.sqrMagnitude < 1e-8f) return;
        transform.Rotate(axis.normalized,
            moved / Mathf.Max(rollRadius, 0.0001f) * Mathf.Rad2Deg, Space.World);
    }
    #endregion
    //自定义缓动插值函数，三种移动状态
    #region
    // 缓动入口
    private float GetEaseValue(float t, EaseType type)
    {
        return type switch
        {
            EaseType.EaseOutInSine => EaseOutInSine(t),
            EaseType.EaseInOutSine => EaseInOutSine(t),
            EaseType.Linear => t,
            _ => t
        };
    }

    // 两边快，中间慢 s(t) = t + k·sin(2πt)/(2π)
    //   s(0)=0、s(1)=1           —— 端点精确，不越界
    //   速度 s'(t) = 1 + k·cos(2πt) —— 端点 1+k 最快、中点 1−k 最慢
    //   sin 一个周期平均为 0      —— 平均速度不变，单程耗时仍 = 距离/速度
    private float EaseOutInSine(float t)
    {
        return t + CurveStrength * Mathf.Sin(2f * Mathf.PI * t) / (2f * Mathf.PI);
    }

    // 两边慢，中间快：s(t) = t - k·sin(2πt)/(2π)
    private float EaseInOutSine(float t)
    {
        return t - CurveStrength * Mathf.Sin(2f * Mathf.PI * t) / (2f * Mathf.PI);
    }
    #endregion
    // 暴露接口，拓展UI面板
    #region
    public void SetSpeed(float val) => moveSpeed = Mathf.Max(0.01f, val);
    public void SetWaitTime(float val) => waitTime = Mathf.Max(0f, val);
    public void SetEaseType(EaseType type) => easeType = type;
    #endregion
    //代码怎么这么难，窝怎么这么区（）
}
