using UnityEngine;

namespace SceneLoader.Core.Components.EditorConstraints
{
    [DisallowMultipleComponent]
    internal abstract class ExecuteOnSceneLoaded : MonoBehaviour
    {
        private void Reset() => RootComponentValidator.Validate(this);
        private void OnValidate() => RootComponentValidator.Validate(this);
    }
}
