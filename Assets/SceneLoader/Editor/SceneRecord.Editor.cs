#if UNITY_EDITOR

#nullable enable

using UnityEngine.UIElements;

namespace SceneLoader.Editor
{
    using Core;

    [UnityEditor.CustomEditor(typeof(SceneRecord))]
    internal sealed class SceneRecordEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            UnityEditor.UIElements.InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var sceneRecord = (SceneRecord) target;
            var customFlowLabel = new Label($"Custom Flow Needed: { sceneRecord._customFlowNeeded }")
            {
                style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold }
            };
            root.Add(customFlowLabel);

            var button = new Button(() =>
            {
                sceneRecord.Configure();
                UnityEditor.EditorUtility.SetDirty(sceneRecord);

                customFlowLabel.text = $"Custom Flow Needed: { sceneRecord._customFlowNeeded }";
            })
            {
                text = "Check if custom flow needed",
                style = { marginTop = 5 }
            };
            root.Add(button);

            return root;
        }
    }
}

#endif
