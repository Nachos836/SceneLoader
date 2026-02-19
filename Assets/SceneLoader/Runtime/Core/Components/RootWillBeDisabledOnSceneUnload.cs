#nullable enable

using System.Diagnostics;
using UnityEngine;

namespace SceneLoader.Core.Components
{
    using Abstract.Internal;
    using EditorConstraints;

    [Tooltip("This component will Disable Root GameObject when Scene Unloaded.")]
    [AddComponentMenu("Scene Loader/Scene's Root Flags/Root Will Be Disabled On Scene Unload")]
    internal sealed class RootWillBeDisabledOnSceneUnload : ExecuteOnSceneUnloaded, ISceneUnloadedDetection
    {
        [SerializeField, HideInInspector] private GameObject _cachedGameObject = default!;

        EntityId ISceneUnloadedDetection.Id => _cachedGameObject.GetEntityId();

        [Conditional("UNITY_EDITOR")] private void Reset() => _cachedGameObject = gameObject;
    }
}
