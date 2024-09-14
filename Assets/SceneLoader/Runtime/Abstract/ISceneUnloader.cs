using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;

namespace SceneLoader.Abstract
{
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    public interface ISceneUnloader<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        UniTask<AsyncRichResult> UnloadAsync(CancellationToken cancellation = default);
    }

    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    public interface ISceneUnloadedEvent<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        IDisposable Subscribe(Func<None, CancellationToken, UniTask> whenUnloaded);
    }
}
