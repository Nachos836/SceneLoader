#nullable enable

using UnityEditor;
using UnityEngine.UIElements;

namespace SceneLoader.Editor
{
    using Core.Components.EditorConstraints;

    [CustomEditor(typeof(ExecuteOnSceneUnloaded), editorForChildClasses: true, isFallback = true)]
    internal sealed class ExecuteOnSceneUnloadedEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => new ();
    }
}
