using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;

namespace SceneLoader.Abstract
{
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    public interface ISceneLoader<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        UniTask<AsyncResult> LoadAsync(CancellationToken cancellation = default);
    }

    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    public interface ISceneLoadedEvent<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        IDisposable Subscribe(Func<None, CancellationToken, UniTask> whenLoaded);
    }
}
