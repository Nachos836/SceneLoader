#if UNITY_EDITOR

using Functional.Core;

namespace SceneLoader.Abstract.Internal
{
    partial interface ISceneLoadedDetectionCustom : IBeforeSceneEditedCustom
    {
        Result IBeforeSceneEditedCustom.ExecuteInEditor() => Execute();
    }
}

#endif
