using System;
using UnityEngine;

namespace _Game_Assets.Scripts.Editor.Unity_Timeline
{
    // A lightweight replacement for Odin Inspector's [ShowIf] (runtime attribute).
    // Determines inspector visibility by checking a condition member (a field/property/parameterless method on the same object).
    //   [ShowIf("boolMember")]          → shown when the member is truthy (bool true / not null)
    //   [ShowIf("enumMember", EMode.A)] → shown when the member equals the specified value
    // Note: the condition member must be a top-level member of the target object (nested serialized objects are not supported).
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public readonly string MemberName;
        public readonly object CompareValue;
        public readonly bool HasCompareValue;

        public ShowIfAttribute(string memberName)
        {
            MemberName = memberName;
            HasCompareValue = false;
        }

        public ShowIfAttribute(string memberName, object compareValue)
        {
            MemberName = memberName;
            CompareValue = compareValue;
            HasCompareValue = true;
        }
    }
}
