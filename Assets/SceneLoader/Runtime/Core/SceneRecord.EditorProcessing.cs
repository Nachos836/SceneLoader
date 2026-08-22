#if UNITY_EDITOR

#nullable enable

using System;
using System.Linq;

namespace SceneLoader.Core
{
    partial class SceneRecord
    {
        internal void Configure()
        {
            _customFlowNeeded = AssetReferenceScene.CheckRequiredCustomFlow(_target);
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

                foreach (var record in sceneRecords.Where(record => record._target.AssetGUID == UnityEditor.AssetDatabase.AssetPathToGUID(assetPath)))
                {
                    record.Configure();

                    UnityEditor.EditorUtility.SetDirty(record);
                }
            }
        }
    }
}

#endif
