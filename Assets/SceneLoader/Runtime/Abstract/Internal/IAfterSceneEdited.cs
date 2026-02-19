namespace SceneLoader.Abstract.Internal
{
    /// <inheritdoc />
    /// <summary>
    /// Disables GameObject after Scene Edited
    /// </summary>
    internal partial interface IAfterSceneEdited : ISceneCustomProcessing { }

    /// <inheritdoc />
    /// <summary>
    /// Execute Custom Callback on GameObject after Scene Edited
    /// </summary>
    internal partial interface IAfterSceneEditedCustom : ISceneCustomProcessing { }
}
