using Functional.Core;

namespace SceneLoader.Abstract
{
    public interface IAfterSceneEdited
    {
#   if UNITY_EDITOR
        protected internal Result ExecuteInEditor();
#   endif
    }

    public interface ISceneUnloaded : IAfterSceneEdited
    {
#   if UNITY_EDITOR
        Result IAfterSceneEdited.ExecuteInEditor() => Execute();
#   endif

        Result Execute();
    }
}
