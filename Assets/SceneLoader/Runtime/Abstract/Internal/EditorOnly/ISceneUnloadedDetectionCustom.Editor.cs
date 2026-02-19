#if UNITY_EDITOR

using Functional.Core;

namespace SceneLoader.Abstract.Internal
{
    partial interface ISceneUnloadedDetectionCustom : IAfterSceneEditedCustom
    {
        Result IAfterSceneEditedCustom.ExecuteInEditor() => Execute();
    }
}

#endif
