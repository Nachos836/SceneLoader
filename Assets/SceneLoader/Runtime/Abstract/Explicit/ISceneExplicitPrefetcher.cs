#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;

namespace SceneLoader.Abstract.Explicit
{
    /// <summary>
    /// This is not planned as part of the main API and Philosophy.
    /// Use only when it's really necessary to be explicit about scene prefetching
    /// </summary>
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    [PublicAPI]
    public interface ISceneExplicitPrefetcher<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        UniTask<AsyncRichResult> PrefetchAsync(CancellationToken cancellation = default);
    }
}
