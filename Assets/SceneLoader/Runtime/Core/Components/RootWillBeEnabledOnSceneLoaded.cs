#nullable enable

using System.Diagnostics;
using UnityEngine;

namespace SceneLoader.Core.Components
{
    using Abstract;
    using EditorConstraints;

    [Tooltip("This component will Enable Root GameObject when Scene Loaded.")]
    [AddComponentMenu("Scene Loader/Scene's Root Flags/Root Will Be Enabled On Scene Loaded")]
    internal sealed class RootWillBeEnabledOnSceneLoaded : ExecuteOnSceneLoaded, ISceneLoadedDetection
    {
        [SerializeField] [HideInInspector] private GameObject _cachedGameObject = default!;

        int ISceneLoadedDetection.Id => _cachedGameObject.GetInstanceID();

        [Conditional("UNITY_EDITOR")] private void Reset() => _cachedGameObject = gameObject;
    }
}
