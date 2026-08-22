#if UNITY_EDITOR

#nullable enable

using System.Linq;
using JetBrains.Annotations;
using UnityEngine.AddressableAssets;

// ReSharper disable once CheckNamespace
namespace SceneLoader.Core
{
    using Abstract.Internal;

    public sealed partial class AssetReferenceScene
    {
        private static System.Type SceneAssetType { get; } = typeof(UnityEditor.SceneAsset);

        /// <summary>
        /// Type-specific override of parent editorAsset.<br/>
        /// Used by the editor to represent the asset referenced.
        /// </summary>
        [UsedImplicitly] // ReSharper disable once InconsistentNaming
        public new UnityEditor.SceneAsset editorAsset => (UnityEditor.SceneAsset) base.editorAsset;

        public override partial bool ValidateAsset(string path)
        {
            var assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
            return SceneAssetType.IsAssignableFrom(assetType);
        }

        public override partial bool ValidateAsset(UnityEngine.Object income)
        {
            return income as UnityEditor.SceneAsset != null;
        }

        internal static bool CheckRequiredCustomFlow(AssetReference? income)
        {
            if (income is null) return false;

            var scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(income.AssetGUID);
            if (string.IsNullOrEmpty(scenePath)) return false;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var requiresCustomFlow = scene.GetRootGameObjects()
                .Any(static root => root.TryGetComponent<ISceneCustomProcessing>(out _));

            if (scene.buildIndex == -1) return requiresCustomFlow;

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, removeScene: true);

            return requiresCustomFlow;
        }
    }
}

#endif
