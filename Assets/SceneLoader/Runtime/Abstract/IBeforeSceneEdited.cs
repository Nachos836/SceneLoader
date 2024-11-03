using Functional.Core;

namespace SceneLoader.Abstract
{
    public interface IBeforeSceneEdited
    {
#   if UNITY_EDITOR
        protected internal Result ExecuteInEditor();
#   endif
    }

    public interface ISceneActivated : IBeforeSceneEdited
    {
#   if UNITY_EDITOR
        Result IBeforeSceneEdited.ExecuteInEditor() => Execute();
#   endif

        Result Execute();
    }
}
