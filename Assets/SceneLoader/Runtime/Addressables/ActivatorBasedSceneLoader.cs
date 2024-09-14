using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using MessagePipe;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Addressables
{
    using Abstract;

    public abstract class ActivatorBasedSceneLoader<TSceneKey> : ISceneLoader<TSceneKey>, ISceneLoadedEvent<TSceneKey>, IDisposable where TSceneKey : class, ISceneKey
    {
        private readonly SceneLoadingPrefetcher.ActivationHandler _activationHandler;
        private readonly IAsyncPublisher<ISceneKey, SceneInstance> _loadedScenes;
        private readonly TSceneKey _key;
        private readonly (IDisposableAsyncPublisher<None> Publisher, IAsyncSubscriber<None> Subscriber) _currentSceneLoaded;

        protected ActivatorBasedSceneLoader
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
                return await _activationHandler.ActivateAsync(cancellation)
                    .ContinueWith(result => result.Attach(_currentSceneLoaded.Publisher)
                        .Run(static (scene, sceneLoaded, token) =>
                        {
                            if (token.IsCancellationRequested) return AsyncResult<SceneInstance>.Cancel;

                            sceneLoaded.PublishAsync(Expected.None, token)
                                .SuppressCancellationThrow()
                                .Forget();

                            return AsyncResult<SceneInstance>.FromResult(scene);

                        }, cancellation)
                        .Attach(_key, _loadedScenes)
                        .RunAsync(static (scene, key, loadedScenes, token) =>
                        {
                            return loadedScenes.PublishAsync(key, scene, token)
                                .SuppressCancellationThrow()
                                .ContinueWith(static isCanceled => isCanceled is not true
                                    ? AsyncResult.Success
                                    : AsyncResult.Cancel);

                        }, cancellation));
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
    }
}
