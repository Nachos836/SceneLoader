#if UNITY_EDITOR

#nullable enable

using Functional.Core;

namespace SceneLoader.Abstract.Internal
{
    partial interface ISceneUnloadedDetectionCustom : IAfterSceneEditedCustom
    {
        Result IAfterSceneEditedCustom.ExecuteInEditor() => Execute();
    }
}

#endif
