using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

[CustomPropertyDrawer(typeof(Effect), true)]
public class EffectPropertyDrawer : PropertyDrawer
{
    private Type[] _effectTypes;
    private string[] _typeNames;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (_effectTypes == null)
        {
            _effectTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Effect)))
                .ToArray();
            _typeNames = _effectTypes.Select(t => t.Name).ToArray();
        }

        EditorGUI.BeginProperty(position, label, property);

        object refObj = property.managedReferenceValue;
        int selectedIndex = -1;
        if (refObj != null)
        {
            selectedIndex = Array.FindIndex(_effectTypes, t => t == refObj.GetType());
        }

        Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        int newSelected = EditorGUI.Popup(dropdownRect, "Effect类型", selectedIndex, _typeNames);

        if (newSelected != selectedIndex)
        {
            Type targetType = _effectTypes[newSelected];
            property.managedReferenceValue = Activator.CreateInstance(targetType);
        }

        // 直接用PropertyField绘制整个子对象，不再手动NextVisible遍历（删掉会越界的代码）
        Rect contentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 4, position.width, position.height - EditorGUIUtility.singleLineHeight - 4);
        EditorGUI.indentLevel++;
        EditorGUI.PropertyField(contentRect, property, GUIContent.none, true);
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float popupHeight = EditorGUIUtility.singleLineHeight + 4;
        float contentHeight = EditorGUI.GetPropertyHeight(property, true);
        return popupHeight + contentHeight;
    }
}
