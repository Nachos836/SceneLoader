#nullable enable

using SceneLoader.Core.Components;

namespace SceneLoader.Editor
{
    [UnityEditor.CustomEditor(typeof(SceneRecordListener))]
    internal sealed class SceneRecordListenerEditor : UnityEditor.Editor
    {
        public override UnityEngine.UIElements.VisualElement CreateInspectorGUI()
        {
            var listener = (SceneRecordListener) target;
            var root = new UnityEngine.UIElements.VisualElement();
            var prefetchButton = new UnityEngine.UIElements.Button(listener.Prefetch)
            {
                text = nameof(SceneRecordListener.Prefetch)
            };
            var loadButton = new UnityEngine.UIElements.Button(listener.Load)
            {
                text = nameof(SceneRecordListener.Load)
            };
            var unloadButton = new UnityEngine.UIElements.Button(listener.Unload)
            {
                text = nameof(SceneRecordListener.Unload)
            };
            var completelyUnloadButton = new UnityEngine.UIElements.Button(listener.CompletelyUnload)
            {
                text = nameof(SceneRecordListener.CompletelyUnload)
            };

            UnityEditor.UIElements.InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.Add(prefetchButton);
            root.Add(loadButton);
            root.Add(unloadButton);
            root.Add(completelyUnloadButton);

            return root;
        }
    }
}
