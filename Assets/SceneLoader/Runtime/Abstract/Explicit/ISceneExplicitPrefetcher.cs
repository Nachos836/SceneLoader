using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;

namespace SceneLoader.Abstract.Explicit
{
    /// <summary>
    /// This is not planned as part of main API and Philosophy.
    /// Use only when it's really needed to be explicit about scene prefetching
    /// </summary>
    /// <typeparam name="TSceneKey">TSceneKey is used for the sake of polymorphism. It makes it easy to find an appropriate type when using DI</typeparam>
    public interface ISceneExplicitPrefetcher<in TSceneKey> where TSceneKey : class, ISceneKey
    {
        UniTask<AsyncRichResult> PrefetchAsync(CancellationToken cancellation = default);
    }
}
