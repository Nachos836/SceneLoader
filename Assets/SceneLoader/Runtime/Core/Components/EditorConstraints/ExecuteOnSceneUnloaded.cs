using Functional.Core;
using UnityEngine;

namespace SceneLoader.Core.Components.EditorConstraints
{
    using Abstract;

    [DisallowMultipleComponent]
    internal abstract class ExecuteOnSceneUnloaded : MonoBehaviour, ISceneUnloaded
    {
#   if UNITY_EDITOR
        private void Reset() => RootComponentValidator.Validate(this);
# endif

        Result ISceneUnloaded.Execute() => Execute();

        protected internal abstract Result Execute();
    }
}
