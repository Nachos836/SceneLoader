namespace SceneLoader.Abstract.Internal
{
    /// <summary>
    /// Simply Enable GameObject before Scene Edited
    /// </summary>
    public partial interface IBeforeSceneEdited : ISceneCustomProcessing { }

    /// <summary>
    /// Execute Custom Callback on GameObject before Scene Edited
    /// </summary>
    public partial interface IBeforeSceneEditedCustom : ISceneCustomProcessing { }
}
