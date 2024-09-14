#nullable enable

using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using MessagePipe;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Addressables
{
    using Abstract;

    public abstract class ReActivatorBasedSceneLoader<TSceneKey> : ISceneLoader<TSceneKey>, ISceneLoadedEvent<TSceneKey>, IDisposable where TSceneKey : class, ISceneKey
    {
        private readonly SceneLoadingPrefetcher.ActivationHandler _activationHandler;
        private readonly IAsyncPublisher<ISceneKey, SceneInstance> _loadedScenes;
        private readonly TSceneKey _key;
        private readonly (IDisposableAsyncPublisher<None> Publisher, IAsyncSubscriber<None> Subscriber) _currentSceneLoaded;

        private SceneReActivator? _reActivationHandler;

        protected ReActivatorBasedSceneLoader
        (
            SceneLoadingPrefetcher.ActivationHandler activationHandler,
            IAsyncPublisher<ISceneKey, SceneInstance> loadedScenes,
            EventFactory eventFactory,
            TSceneKey key
        ) {
            _activationHandler = activationHandler;
            _loadedScenes = loadedScenes;
            _key = key;
            _currentSceneLoaded = eventFactory.CreateAsyncEvent<None>();
        }

        /// <summary>
        /// Make sure to provide CancellationToken which can track Application Exit
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        async UniTask<AsyncResult> ISceneLoader<TSceneKey>.LoadAsync(CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested) return AsyncResult.Cancel;

            try
            {
                if (_reActivationHandler is { } candidate)
                {
                    return await candidate.ActivateAsync(_currentSceneLoaded.Publisher, cancellation);
                }

                var activation = await _activationHandler.ActivateAsync(cancellation);

                _reActivationHandler = activation.Match<SceneReActivator?>
                (
                    success: static scene => new SceneReActivator(scene),
                    cancellation: static () => null,
                    error: static _ => null
                );

                return await activation.Attach(_currentSceneLoaded.Publisher)
                    .Run(static (scene, sceneLoaded, token) =>
                    {
                        if (token.IsCancellationRequested) return AsyncResult<SceneInstance>.Cancel;

                        sceneLoaded.PublishAsync(Expected.None, token)
                            .SuppressCancellationThrow()
                            .Forget();

                        return AsyncResult<SceneInstance>.FromResult(scene);
                    })
                    .Attach(_key, _loadedScenes)
                    .RunAsync(static (scene, key, loadedScenes, token) =>
                    {
                        return loadedScenes.PublishAsync(key, scene, token)
                            .SuppressCancellationThrow()
                            .ContinueWith(static isCanceled => isCanceled is not true
                                ? AsyncResult.Success
                                : AsyncResult.Cancel);

                    }, cancellation);
            }
            catch (Exception unexpected)
            {
                return AsyncResult.FromException(unexpected);
            }
        }

        IDisposable ISceneLoadedEvent<TSceneKey>.Subscribe(Func<None, CancellationToken, UniTask> whenLoaded)
        {
            return _currentSceneLoaded.Subscriber.Subscribe(whenLoaded);
        }

        public virtual void Dispose()
        {
            _currentSceneLoaded.Publisher.Dispose();
        }

        private sealed class SceneReActivator
        {
            private readonly SceneInstance _instance;

            public SceneReActivator(SceneInstance instance) => _instance = instance;

            public UniTask<AsyncResult> ActivateAsync
            (
                IDisposableAsyncPublisher<None> loadedPublisher,
                CancellationToken cancellation = default
            ) {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncResult.Cancel);

                var roots = _instance.Scene.GetRootGameObjects();

                for (var i = 0; i < roots.Length && cancellation.IsCancellationRequested is not true; i++)
                {
                    var root = roots[i];

                    if (cancellation.IsCancellationRequested is not true)
                    {
                        root.SetActive(true);
                    }
                }

                var result = cancellation.IsCancellationRequested
                    ? AsyncResult.Cancel
                    : AsyncResult.Success;

                return result.Attach(loadedPublisher, roots)
                    .RunAsync(static (publisher, roots, token) =>
                    {
                        if (token.IsCancellationRequested) return UniTask.FromResult(AsyncResult.Cancel);

                        return UniTask.WaitUntil(() => roots.All(static root => root.activeInHierarchy), PlayerLoopTiming.Initialization, token)
                            .SuppressCancellationThrow()
                            .ContinueWith(isCanceled =>
                            {
                                if (isCanceled) return AsyncResult.Cancel;

                                publisher.PublishAsync(Expected.None, token)
                                    .SuppressCancellationThrow()
                                    .Forget();

                                return token.IsCancellationRequested
                                    ? AsyncResult.Cancel
                                    : AsyncResult.Success;
                            });
                    }, cancellation);
            }
        }
    }
}
