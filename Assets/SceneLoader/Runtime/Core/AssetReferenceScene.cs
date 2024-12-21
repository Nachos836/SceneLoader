#nullable enable

using UnityEngine.AddressableAssets;

namespace SceneLoader.Core
{
    [System.Serializable]
    public sealed partial class AssetReferenceScene : AssetReference
    {
        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        /// <param name="guid">The guid of the asset.</param>
        public AssetReferenceScene(string guid) : base(guid) {}

        /// <inheritdoc/>
        public override partial bool ValidateAsset(string path);

        /// <inheritdoc/>
        public override partial bool ValidateAsset(UnityEngine.Object income);
    }
}
