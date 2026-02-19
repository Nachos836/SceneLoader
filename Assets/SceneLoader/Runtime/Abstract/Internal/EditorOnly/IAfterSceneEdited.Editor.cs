#if UNITY_EDITOR

using Functional.Core;
using UnityEngine;

namespace SceneLoader.Abstract.Internal
{
    partial interface IAfterSceneEdited
    {
        protected internal EntityId IdForEditor { get; }
    }

    partial interface IAfterSceneEditedCustom
    {
        protected internal Result ExecuteInEditor();
    }
}

#endif
