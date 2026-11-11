using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class MouseUtil
{
    // 用属性而不是静态字段：静态字段在类首次加载时就取 Camera.main，
    // 那时场景相机可能还没就绪，会把 null 一直缓存下来导致后面空引用
    private static Camera MainCamera => Camera.main;

    public static Vector3 GetMousePositionInWorldSpace(float zValue = 0f)
    {
        Camera camera = MainCamera;
        if (camera == null) return Vector3.zero;

        Plane dragPlane = new Plane(camera.transform.forward, new Vector3(0, 0, zValue));
        Ray ray = camera.ScreenPointToRay(GetScreenPosition());
        if (dragPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }

    // 项目用的是 Input System（Player Settings 里 Active Input Handling = Input System Package），
    // 此时 UnityEngine.Input.mousePosition 会抛 InvalidOperationException，必须走新 API
    private static Vector2 GetScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) return mouse.position.ReadValue();
#endif
        return Input.mousePosition;
    }
}
