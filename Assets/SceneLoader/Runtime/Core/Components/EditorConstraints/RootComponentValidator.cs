using System.Diagnostics;
using UnityEngine;

namespace SceneLoader.Core.Components.EditorConstraints
{
    internal static class RootComponentValidator
    {
        [Conditional("UNITY_EDITOR")]
        public static void Validate(Component target)
        {
            if (target.transform.parent == null) return;
            if (target.gameObject.scene != default) return;

            Object.DestroyImmediate(target.gameObject);
            UnityEngine.Debug.LogErrorFormat(target, "Component must be added to the root of {0}", target.gameObject);
        }
    }
}
