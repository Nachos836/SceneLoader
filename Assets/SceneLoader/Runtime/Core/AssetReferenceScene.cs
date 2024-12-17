#nullable enable

using System.Linq;
using UnityEngine.AddressableAssets;

namespace SceneLoader.Core
{
    using Abstract;

    [System.Serializable]
    public sealed class AssetReferenceScene : AssetReference
    {
#if UNITY_EDITOR
        private static System.Type SceneAssetType { get; } = typeof(UnityEditor.SceneAsset);
#endif

        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        /// <param name="guid">The guid of the asset.</param>
        public AssetReferenceScene(string guid) : base(guid) {}

        /// <inheritdoc/>
        public override bool ValidateAsset(UnityEngine.Object income)
        {
#if UNITY_EDITOR
            return income as UnityEditor.SceneAsset != null;
#else
            return base.ValidateAsset(income);
#endif
        }

        /// <inheritdoc/>
        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            var assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
            return SceneAssetType.IsAssignableFrom(assetType);
#else
            return base.ValidateAsset(path);
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Type-specific override of parent editorAsset.  Used by the editor to represent the asset referenced.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public new UnityEditor.SceneAsset editorAsset => (UnityEditor.SceneAsset) base.editorAsset;

        public static bool CheckRequiredCustomFlow(AssetReference? income)
        {
            if (income is null) return false;

            var scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(income.AssetGUID);
            if (string.IsNullOrEmpty(scenePath)) return false;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var requiresCustomFlow = scene.GetRootGameObjects().Any(static root =>
            {
                return root.TryGetComponent<ISceneActivated>(out _)
                    || root.TryGetComponent<ISceneUnloaded>(out _);
            });

            if (scene.buildIndex == -1) return requiresCustomFlow;

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, removeScene: true);

            return requiresCustomFlow;
        }
#endif
    }
}
