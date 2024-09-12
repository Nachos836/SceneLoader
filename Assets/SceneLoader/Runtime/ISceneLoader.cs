using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional;
using Functional.Outcome;

namespace SceneLoader
{
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    public interface ISceneLoader<TSceneKey> where TSceneKey : struct, ISceneKey
    {
        UniTask<AsyncResult> LoadAsync(CancellationToken cancellation = default);
    }

    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    public interface ISceneLoadedEvent<out TSceneKey> where TSceneKey : struct, ISceneKey
    {
        IDisposable Subscribe(Func<None, CancellationToken, UniTask> whenLoaded);
    }
}
