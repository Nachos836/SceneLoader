namespace SceneLoader.Abstract.Internal
{
    /// <summary>
    /// Simply Disable GameObject after Scene Edited
    /// </summary>
    public partial interface IAfterSceneEdited : ISceneCustomProcessing { }

    /// <summary>
    /// Execute Custom Callback on GameObject after Scene Edited
    /// </summary>
    public partial interface IAfterSceneEditedCustom : ISceneCustomProcessing { }
}
