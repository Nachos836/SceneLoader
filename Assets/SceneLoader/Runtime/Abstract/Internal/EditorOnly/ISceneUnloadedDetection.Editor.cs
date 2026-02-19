#if UNITY_EDITOR

using UnityEngine;

namespace SceneLoader.Abstract.Internal
{
    partial interface ISceneUnloadedDetection : IAfterSceneEdited
    {
        EntityId IAfterSceneEdited.IdForEditor => Id;
    }
}

#endif
