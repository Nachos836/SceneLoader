#if !UNITY_EDITOR

#nullable enable

namespace SceneLoader.Core
{
    public sealed partial class AssetReferenceScene
    {
        public override partial bool ValidateAsset(string path)
        {
            return base.ValidateAsset(path);
        }

        public override partial bool ValidateAsset(UnityEngine.Object income)
        {
            return base.ValidateAsset(income);
        }
    }
}

#endif
