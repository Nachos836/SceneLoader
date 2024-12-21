#if UNITY_EDITOR

using Functional.Core;

namespace SceneLoader.Abstract.Internal
{
    public partial interface IAfterSceneEdited
    {
        protected internal int IdForEditor { get; }
    }

    public partial interface IAfterSceneEditedCustom
    {
        protected internal Result ExecuteInEditor();
    }
}

#endif
