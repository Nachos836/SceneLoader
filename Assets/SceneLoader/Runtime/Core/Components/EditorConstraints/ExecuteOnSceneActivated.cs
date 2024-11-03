using Functional.Core;
using UnityEngine;

namespace SceneLoader.Core.Components.EditorConstraints
{
    using Abstract;

    [DisallowMultipleComponent]
    internal abstract class ExecuteOnSceneActivated : MonoBehaviour, ISceneActivated
    {
#   if UNITY_EDITOR
        private void Reset() => RootComponentValidator.Validate(this);
# endif

        Result ISceneActivated.Execute() => Execute();

        protected internal abstract Result Execute();
    }
}
