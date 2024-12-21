#if UNITY_EDITOR

using Functional.Core;

namespace SceneLoader.Abstract
{
    using Internal;

    public partial interface ISceneUnloadedDetectionCustom : IAfterSceneEditedCustom
    {
        Result IAfterSceneEditedCustom.ExecuteInEditor() => Execute();
    }
}

#endif
