using System.Reflection;
using UnityEngine;

namespace Zombera.Testing
{
    internal static class SerializedFieldUtility
    {
        public static void SetField(Object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName)) return;

            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning($"[SerializedFieldUtility] Field '{fieldName}' not found on {type.Name}.");
                return;
            }

            if (value != null && !field.FieldType.IsAssignableFrom(value.GetType()))
            {
                Debug.LogWarning(
                    $"[SerializedFieldUtility] Cannot assign {value.GetType().Name} to {fieldName} ({field.FieldType.Name}) on {type.Name}.");
                return;
            }

            field.SetValue(target, value);
        }
    }
}
