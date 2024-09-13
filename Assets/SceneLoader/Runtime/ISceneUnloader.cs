using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;

namespace SceneLoader
{
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    public interface ISceneUnloader<TSceneKey> where TSceneKey : struct, ISceneKey
    {
        UniTask<AsyncRichResult> UnloadAsync(CancellationToken cancellation = default);
    }

    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    public interface ISceneUnloadedEvent<TSceneKey> where TSceneKey : struct, ISceneKey
    {
        IDisposable Subscribe(Func<None, CancellationToken, UniTask> whenUnloaded);
    }
}
