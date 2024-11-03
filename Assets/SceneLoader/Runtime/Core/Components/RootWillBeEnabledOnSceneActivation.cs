#nullable enable

using System;
using Functional.Core;
using UnityEngine;

namespace SceneLoader.Core.Components
{
    using EditorConstraints;

    [AddComponentMenu("Scene Loader/Scene's Root Flags/Root Will Be Enabled On Scene Activation")]
    internal sealed class RootWillBeEnabledOnSceneActivation : ExecuteOnSceneActivated
    {
        [SerializeField] [HideInInspector] private GameObject _cachedGameObject = default!;

#   if UNITY_EDITOR
        private void Reset() => _cachedGameObject = gameObject;
#   endif

        private void OnDestroy() => _cachedGameObject = null!;

        protected internal override Result Execute()
        {
            if (_cachedGameObject == null) return Result.FromException(new NullReferenceException("Cached GameObject is null"));

            _cachedGameObject.SetActive(true);

            return Result.Success;
        }
    }
}
