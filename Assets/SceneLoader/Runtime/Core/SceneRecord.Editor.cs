#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEngine;

namespace SceneLoader.Core
{
    partial class SceneRecord
    {
        private void Configure()
        {
            _customFlowNeeded = AssetReferenceScene.CheckRequiredCustomFlow(Target);
        }

        [UnityEditor.CustomEditor(typeof(SceneRecord))]
        internal sealed class SceneRecordEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var sceneRecord = (SceneRecord) target;

                UnityEditor.EditorGUILayout.LabelField("Custom Flow Needed", sceneRecord._customFlowNeeded.ToString());
                if (GUILayout.Button("Check if custom flow needed") is false) return;

                sceneRecord.Configure();

                UnityEditor.EditorUtility.SetDirty(sceneRecord);
            }
        }

        internal sealed class SceneModificationPostprocessor : UnityEditor.AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                var sceneAssetPaths = importedAssets.Concat(deletedAssets)
                    .Concat(movedAssets)
                    .Concat(movedFromAssetPaths)
                    .Where(static assetPath => assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));

                foreach (var assetPath in sceneAssetPaths)
                {
                    RevalidateSceneRecordsForAsset(assetPath);
                }
            }

            private static void RevalidateSceneRecordsForAsset(string assetPath)
            {
                var sceneRecords = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(SceneRecord))
                    .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<SceneRecord>);

                foreach (var record in sceneRecords.Where(record => record.Target.AssetGUID == UnityEditor.AssetDatabase.AssetPathToGUID(assetPath)))
                {
                    record.Configure();

                    UnityEditor.EditorUtility.SetDirty(record);
                }
            }
        }
    }
}

#endif
