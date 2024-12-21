#if UNITY_EDITOR

namespace SceneLoader.Abstract
{
    using Internal;

    public partial interface ISceneUnloadedDetection : IAfterSceneEdited
    {
        int IAfterSceneEdited.IdForEditor => Id;
    }
}

#endif
