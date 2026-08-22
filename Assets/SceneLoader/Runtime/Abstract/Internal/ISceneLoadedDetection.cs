#nullable enable

using Functional.Core;
using UnityEngine;

namespace SceneLoader.Abstract.Internal
{
    /// <inheritdoc />
    /// <summary>
    /// Enable GameObject when Scene is Loaded
    /// </summary>
    internal partial interface ISceneLoadedDetection
    {
        EntityId Id { get; }
    }

    /// <inheritdoc />
    /// <summary>
    /// Execute Custom Callback on GameObject when Scene is Loaded
    /// </summary>
    internal partial interface ISceneLoadedDetectionCustom
    {
        Result Execute();
    }
}
