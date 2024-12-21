#if UNITY_EDITOR

using Functional.Core;

namespace SceneLoader.Abstract.Internal
{
    public partial interface IBeforeSceneEdited
    {
        protected internal int IdForEditor { get; }
    }

    public partial interface IBeforeSceneEditedCustom
    {
        protected internal Result ExecuteInEditor();
    }
}

#endif
