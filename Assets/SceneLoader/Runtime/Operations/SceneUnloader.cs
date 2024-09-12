﻿#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using VContainer.Unity;
using Functional;
using Functional.Outcome;

namespace SceneLoader.Operations
{
    public abstract class SceneUnloader<TSceneKey> : IAsyncStartable, ISceneUnloader<TSceneKey>, ISceneUnloadedEvent<TSceneKey>, IDisposable where TSceneKey : struct, ISceneKey
    {
        private readonly PlayerLoopTiming _initializationPoint;
        private readonly IAsyncSubscriber<ISceneKey, SceneInstance> _loadedScenes;
        private readonly ISceneKey _key;
        private readonly IDisposableAsyncPublisher<None> _unloadedPublisher;
        private readonly IAsyncSubscriber<None> _unloadedSubscriber;

        private SceneInstance? _instance;
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
            (_unloadedPublisher, _unloadedSubscriber) = eventFactory.CreateAsyncEvent<None>();
        }

        async UniTask IAsyncStartable.StartAsync(CancellationToken cancellation)
        {
            if (await UniTask.Yield(_initializationPoint, cancellationToken: cancellation, cancelImmediately: true)
                .SuppressCancellationThrow()) return;

            _subscription?.Dispose();
            _subscription = _loadedScenes.Subscribe(_key, (instance, token) =>
            {
                if (token.IsCancellationRequested) return UniTask.CompletedTask;

                _instance = instance;

                return UniTask.CompletedTask;
            });
        }

        UniTask<AsyncRichResult> ISceneUnloader<TSceneKey>.UnloadAsync(CancellationToken cancellation)
        {
            if (_instance is not { Scene: { isLoaded: true } }) return UniTask.FromResult(AsyncRichResult.FromFailure(new Expected.Failure("Scene wasn't active to be unloaded")));
            if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

            return RoutineAsync(_instance.Value, cancellation)
                .ContinueWith(result => result.Run(() =>
                {
                    _unloadedPublisher.PublishAsync(Expected.None, cancellation)
                        .SuppressCancellationThrow()
                        .Forget();
                }));
        }

        IDisposable ISceneUnloadedEvent<TSceneKey>.Subscribe(Func<None, CancellationToken, UniTask> whenUnloaded)
        {
            return _unloadedSubscriber.Subscribe(whenUnloaded);
        }

        protected abstract UniTask<AsyncRichResult> RoutineAsync(SceneInstance instance, CancellationToken cancellation);

        public virtual void Dispose()
        {
            _unloadedPublisher.Dispose();
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

                return Addressables.UnloadSceneAsync(instance)
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
