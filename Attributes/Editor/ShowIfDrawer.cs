using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace _Game_Assets.Scripts.Editor.Unity_Timeline
{
    // If the ShowIfAttribute condition is false, the field is not drawn at all (its height collapses too).
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
                EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // When hidden, also offset the spacing between properties so no empty line remains.
            return ShouldShow(property)
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : -EditorGUIUtility.standardVerticalSpacing;
        }

        bool ShouldShow(SerializedProperty property)
        {
            var attr = (ShowIfAttribute)attribute;
            object target = property.serializedObject.targetObject;

            if (!TryResolveMember(target, attr.MemberName, out object value))
            {
                Debug.LogWarning($"[ShowIf] Condition member '{attr.MemberName}' was not found on {target?.GetType().Name}.");
                return true;   // Don't hide it when not found, so configuration mistakes stay noticeable.
            }

            return attr.HasCompareValue ? Equals(value, attr.CompareValue) : IsTruthy(value);
        }

        static bool IsTruthy(object value) => value switch
        {
            null => false,
            bool b => b,
            UnityEngine.Object o => o != null,   // Destroyed objects (null-equivalent) are also false
            _ => true,
        };

        // Looks up the condition member on the target object in order: field → property → parameterless method.
        static bool TryResolveMember(object target, string name, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrEmpty(name)) return false;

            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = type.GetField(name, flags);
            if (field != null) { value = field.GetValue(target); return true; }

            var prop = type.GetProperty(name, flags);
            if (prop != null && prop.CanRead) { value = prop.GetValue(target); return true; }

            var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (method != null) { value = method.Invoke(target, null); return true; }

            return false;
        }
    }
}
