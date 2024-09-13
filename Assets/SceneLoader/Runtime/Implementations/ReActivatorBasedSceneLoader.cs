#nullable enable

using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using MessagePipe;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Implementations
{
    public abstract class ReActivatorBasedSceneLoader<TSceneKey> : ISceneLoader<TSceneKey>, ISceneLoadedEvent<TSceneKey>, IDisposable where TSceneKey : struct, ISceneKey
    {
        private readonly SceneLoadingPrefetcher.ActivationHandler _activationHandler;
        private readonly IAsyncPublisher<ISceneKey, SceneInstance> _loadedScenes;
        private readonly ISceneKey _key;
        private readonly IDisposableAsyncPublisher<None> _loadedPublisher;
        private readonly IAsyncSubscriber<None> _loadedSubscriber;

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
            (_loadedPublisher, _loadedSubscriber) = eventFactory.CreateAsyncEvent<None>();
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
                if (_reActivationHandler is { } scene)
                {
                    return await scene.ActivateAsync(_loadedPublisher, cancellation);
                }

                var activation = await _activationHandler.ActivateAsync(cancellation);

                return await activation
                    .Attach(_loadedPublisher)
                    .Run((instance, loadedPublisher, token) =>
                    {
                        if (token.IsCancellationRequested) return AsyncResult<SceneInstance>.Cancel;

                        loadedPublisher.PublishAsync(Expected.None, token)
                            .SuppressCancellationThrow()
                            .Forget();

                        _reActivationHandler = new SceneReActivator(instance);

                        return AsyncResult<SceneInstance>.FromResult(instance);
                    })
                    .Attach(_loadedScenes, _key)
                    .RunAsync(static (instance, loadedScenes, key, token) =>
                    {
                        return loadedScenes.PublishAsync(key, instance, token)
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
            return _loadedSubscriber.Subscribe(whenLoaded);
        }

        public virtual void Dispose()
        {
            _loadedPublisher.Dispose();
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
