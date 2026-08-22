#if !UNITY_EDITOR

#nullable enable

// ReSharper disable once CheckNamespace
namespace SceneLoader.Core
{
    public sealed partial class AssetReferenceScene
    {
        public override partial bool ValidateAsset(string path) => base.ValidateAsset(path);
        public override partial bool ValidateAsset(UnityEngine.Object income) => base.ValidateAsset(income);
    }
}

#endif
