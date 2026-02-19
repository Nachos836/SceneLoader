#if UNITY_EDITOR

using Functional.Core;
using UnityEngine;

namespace SceneLoader.Abstract.Internal
{
    partial interface IBeforeSceneEdited
    {
        protected internal EntityId IdForEditor { get; }
    }

    partial interface IBeforeSceneEditedCustom
    {
        protected internal Result ExecuteInEditor();
    }
}

#endif
