using Functional.Core;

namespace SceneLoader.Abstract
{
    /// <summary>
    /// Enable GameObject <br/>
    /// when Scene is Loaded
    /// </summary>
    public partial interface ISceneLoadedDetection
    {
        int Id { get; }
    }

    /// <summary>
    /// Execute Custom Callback on GameObject <br/>
    /// when Scene is Loaded
    /// </summary>
    public partial interface ISceneLoadedDetectionCustom
    {
        Result Execute();
    }
}
