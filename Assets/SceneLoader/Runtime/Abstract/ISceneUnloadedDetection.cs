using Functional.Core;

namespace SceneLoader.Abstract
{
    /// <summary>
    /// Simply Disable GameObject <br/>
    /// when Scene will be Unloaded
    /// </summary>
    public partial interface ISceneUnloadedDetection
    {
        int Id { get; }
    }

    /// <summary>
    /// Execute Custom Callback on GameObject <br/>
    /// when Scene will be Unloaded
    /// </summary>
    public partial interface ISceneUnloadedDetectionCustom
    {
        Result Execute();
    }
}
