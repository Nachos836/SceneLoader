#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;

namespace SceneLoader.Abstract
{
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [PublicAPI]
    public interface ISceneLoader<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        UniTask<AsyncRichResult> LoadAsync(CancellationToken cancellation = default);
    }

    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [PublicAPI]
    public interface ISceneLoadedEvent<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        IDisposable Subscribe(Action whenLoaded);
    }
}
