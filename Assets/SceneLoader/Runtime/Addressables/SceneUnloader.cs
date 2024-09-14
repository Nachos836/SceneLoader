#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using MessagePipe;
using UnityEngine.ResourceManagement.ResourceProviders;
using VContainer.Unity;

namespace SceneLoader.Addressables
{
    using Abstract;

    public abstract class SceneUnloader<TSceneKey> : IAsyncStartable, ISceneUnloader<TSceneKey>, ISceneUnloadedEvent<TSceneKey>, IDisposable where TSceneKey : class, ISceneKey
    {
        private readonly PlayerLoopTiming _initializationPoint;
        private readonly IAsyncSubscriber<ISceneKey, SceneInstance> _loadedScenes;
        private readonly TSceneKey _key;
        private readonly (IDisposableAsyncPublisher<None> Publisher, IAsyncSubscriber<None> Notifier) _currentSceneUnloaded;

        private SceneInstance? _scene;
        private IDisposable? _subscription;

        private SceneUnloader
        (
            PlayerLoopTiming initializationPoint,
            IAsyncSubscriber<ISceneKey, SceneInstance> loadedScenes,
            EventFactory eventFactory,
            TSceneKey key
        ) {
            _initializationPoint = initializationPoint;
            _loadedScenes = loadedScenes;
            _key = key;
            _currentSceneUnloaded = eventFactory.CreateAsyncEvent<None>();
        }

        async UniTask IAsyncStartable.StartAsync(CancellationToken cancellation)
        {
            if (await UniTask.Yield(_initializationPoint, cancellationToken: cancellation, cancelImmediately: true)
                .SuppressCancellationThrow()) return;

            _subscription?.Dispose();
            _subscription = _loadedScenes.Subscribe(_key, (scene, token) =>
            {
                if (token.IsCancellationRequested) return UniTask.CompletedTask;

                _scene = scene;

                return UniTask.CompletedTask;
            });
        }

        UniTask<AsyncRichResult> ISceneUnloader<TSceneKey>.UnloadAsync(CancellationToken cancellation)
        {
            if (_scene is null) return UniTask.FromResult<AsyncRichResult>(new Exception("Scene wasn't loaded"));
            if (_scene is not { Scene: { isLoaded: true } }) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene wasn't active to be unloaded"));
            if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

            return RoutineAsync(_scene.Value, cancellation)
                .ContinueWith(result => result.Run(() =>
                {
                    _currentSceneUnloaded.Publisher.PublishAsync(Expected.None, cancellation)
                        .SuppressCancellationThrow()
                        .Forget();
                }));
        }

        IDisposable ISceneUnloadedEvent<TSceneKey>.Subscribe(Func<None, CancellationToken, UniTask> whenUnloaded)
        {
            return _currentSceneUnloaded.Notifier.Subscribe(whenUnloaded);
        }

        protected abstract UniTask<AsyncRichResult> RoutineAsync(SceneInstance instance, CancellationToken cancellation);

        public virtual void Dispose()
        {
            _currentSceneUnloaded.Publisher.Dispose();
            _subscription?.Dispose();
            _subscription = null;
        }

        internal abstract class Complete : SceneUnloader<TSceneKey>
        {
            protected Complete(PlayerLoopTiming initializationPoint, IAsyncSubscriber<ISceneKey, SceneInstance> loadedScenes, EventFactory eventFactory, TSceneKey scopeKey)
                : base(initializationPoint, loadedScenes, eventFactory, scopeKey) { }

            protected override UniTask<AsyncRichResult> RoutineAsync(SceneInstance instance, CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

                return UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(instance)
                    .ToUniTask(progress: null!, PlayerLoopTiming.Initialization, cancellation, cancelImmediately: true, autoReleaseWhenCanceled: true)
                    .SuppressCancellationThrow()
                    .ContinueWith(static operation => operation.IsCanceled is not true
                        ? AsyncRichResult.Success
                        : AsyncRichResult.Cancel);
            }
        }

        internal abstract class Deactivate : SceneUnloader<TSceneKey>
        {
            protected Deactivate(PlayerLoopTiming initializationPoint, IAsyncSubscriber<ISceneKey, SceneInstance> loadedScenes, EventFactory eventFactory, TSceneKey key)
                : base(initializationPoint, loadedScenes, eventFactory, key) { }

            protected override UniTask<AsyncRichResult> RoutineAsync(SceneInstance instance, CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

                var roots = instance.Scene.GetRootGameObjects();

                for (var i = 0; i < roots.Length && cancellation.IsCancellationRequested is not true; i++)
                {
                    var root = roots[i];

                    if (root.TryGetComponent<LifetimeScope>(out _) is not true
                    && cancellation.IsCancellationRequested is not true)
                    {
                        root.SetActive(false);
                    }
                }

                return cancellation.IsCancellationRequested
                    ? UniTask.FromResult(AsyncRichResult.Cancel)
                    : UniTask.FromResult(AsyncRichResult.Success);
            }
        }
    }
}
