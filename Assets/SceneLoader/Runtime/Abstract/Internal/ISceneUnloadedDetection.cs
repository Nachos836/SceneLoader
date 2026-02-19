using Functional.Core;
using UnityEngine;

namespace SceneLoader.Abstract.Internal
{
    /// <inheritdoc />
    /// <summary>
    /// Disable GameObject when Scene is Unloaded
    /// </summary>
    internal partial interface ISceneUnloadedDetection
    {
        EntityId Id { get; }
    }

    /// <inheritdoc />
    /// <summary>
    /// Execute Custom Callback on GameObject when Scene is Unloaded
    /// </summary>
    internal partial interface ISceneUnloadedDetectionCustom
    {
        Result Execute();
    }
}
