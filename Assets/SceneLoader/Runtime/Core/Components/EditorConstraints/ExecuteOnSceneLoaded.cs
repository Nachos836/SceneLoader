#nullable enable

using System.Diagnostics;
using UnityEngine;

namespace SceneLoader.Core.Components.EditorConstraints
{
    [DisallowMultipleComponent]
    internal abstract class ExecuteOnSceneLoaded : MonoBehaviour
    {
        [Conditional("UNITY_EDITOR")] private void Reset() => RootComponentValidator.Validate(this);
        [Conditional("UNITY_EDITOR")] private void OnValidate() => Reset();
    }
}
