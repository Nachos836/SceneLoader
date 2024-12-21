#if UNITY_EDITOR

namespace SceneLoader.Abstract
{
    using Internal;

    public partial interface ISceneLoadedDetection : IBeforeSceneEdited
    {
        int IBeforeSceneEdited.IdForEditor => Id;
    }
}

#endif
