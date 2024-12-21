#if UNITY_EDITOR

using Functional.Core;

namespace SceneLoader.Abstract
{
    using Internal;

    public partial interface ISceneLoadedDetectionCustom : IBeforeSceneEditedCustom
    {
        Result IBeforeSceneEditedCustom.ExecuteInEditor() => Execute();
    }
}

#endif
