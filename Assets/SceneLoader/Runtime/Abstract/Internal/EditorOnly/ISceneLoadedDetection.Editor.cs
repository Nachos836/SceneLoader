#if UNITY_EDITOR

using UnityEngine;

namespace SceneLoader.Abstract.Internal
{
    partial interface ISceneLoadedDetection : IBeforeSceneEdited
    {
        EntityId IBeforeSceneEdited.IdForEditor => Id;
    }
}

#endif
