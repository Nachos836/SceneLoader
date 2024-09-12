using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine.ResourceManagement.ResourceProviders;
using Functional;
using Functional.Outcome;

namespace SceneLoader.Operations
{
    public abstract class ActivatorBasedSceneLoader<TSceneKey> : ISceneLoader<TSceneKey>, ISceneLoadedEvent<TSceneKey>, IDisposable where TSceneKey : struct, ISceneKey
    {
        private readonly SceneLoadingPrefetcher.ActivationHandler _activationHandler;
        private readonly IAsyncPublisher<ISceneKey, SceneInstance> _loadedScenes;
        private readonly ISceneKey _key;
        private readonly IDisposableAsyncPublisher<None> _loadedPublisher;
        private readonly IAsyncSubscriber<None> _loadedSubscriber;

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
                return await _activationHandler.ActivateAsync(cancellation)
                    .ContinueWith(result => result.Attach(_loadedPublisher)
                        .Run(static (instance, publisher, token) =>
                        {
                            if (token.IsCancellationRequested) return AsyncResult<SceneInstance>.Cancel;

                            publisher.PublishAsync(Expected.None, token)
                                .SuppressCancellationThrow()
                                .Forget();

                            return AsyncResult<SceneInstance>.FromResult(instance);

                        }, cancellation)
                        .Attach(_loadedScenes, _key)
                        .RunAsync(static (instance, publisher, key, token) =>
                        {
                            return publisher.PublishAsync(key, instance, token)
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
            return _loadedSubscriber.Subscribe(whenLoaded);
        }

        public virtual void Dispose()
        {
            _loadedPublisher.Dispose();
        }
    }
}
