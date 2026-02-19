namespace SceneLoader.Abstract.Internal
{
    /// <inheritdoc />
    /// <summary>
    /// Enables GameObject before Scene Edited
    /// </summary>
    internal partial interface IBeforeSceneEdited : ISceneCustomProcessing { }

    /// <inheritdoc />
    /// <summary>
    /// Execute Custom Callback on GameObject before Scene Edited
    /// </summary>
    internal partial interface IBeforeSceneEditedCustom : ISceneCustomProcessing { }
}
