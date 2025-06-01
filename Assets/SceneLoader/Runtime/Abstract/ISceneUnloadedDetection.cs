using Functional.Core;

namespace SceneLoader.Abstract
{
    /// <summary>
    /// Disable GameObject <br/>
    /// when Scene is Unloaded
    /// </summary>
    public partial interface ISceneUnloadedDetection
    {
        int Id { get; }
    }

    /// <summary>
    /// Execute Custom Callback on GameObject <br/>
    /// when Scene is Unloaded
    /// </summary>
    public partial interface ISceneUnloadedDetectionCustom
    {
        Result Execute();
    }
}
