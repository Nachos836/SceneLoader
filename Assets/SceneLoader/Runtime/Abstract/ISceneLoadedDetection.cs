using Functional.Core;

namespace SceneLoader.Abstract
{
    /// <summary>
    /// Simply Enable GameObject <br/>
    /// when Scene will be Loaded
    /// </summary>
    public partial interface ISceneLoadedDetection
    {
        int Id { get; }
    }

    /// <summary>
    /// Execute Custom Callback on GameObject <br/>
    /// when Scene will be Loaded
    /// </summary>
    public partial interface ISceneLoadedDetectionCustom
    {
        Result Execute();
    }
}
