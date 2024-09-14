namespace SceneLoader.Abstract
{
    /// <summary>
    /// Type with this implemented interface makes Editor Utilities skip processing of a GameObject on the scene
    /// You need to add a component to the desired GameObject with this implemented interface
    /// </summary>
    public interface IPreserveGameObjectStateFlag { }
}
