using UnityEditor;
using UnityEngine.UIElements;

namespace SceneLoader.Editor
{
    using Core.Components.EditorConstraints;

    [CustomEditor(typeof(ExecuteOnSceneLoaded), editorForChildClasses: true, isFallback = true)]
    internal sealed class ExecuteOnSceneLoadedEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => new ();
    }
}
