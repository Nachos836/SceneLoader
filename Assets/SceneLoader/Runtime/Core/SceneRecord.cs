#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace SceneLoader.Core
{
    using Abstract;
    using SceneStates;

    [UsedImplicitly] internal sealed record Bootstrap;
    [UsedImplicitly] internal sealed record Prefetch;
    [UsedImplicitly] internal sealed record Activate;
    [UsedImplicitly] internal sealed record Deactivate;
    [UsedImplicitly] internal sealed record Unload;

    [CreateAssetMenu(menuName = "Scene/Scene Record")]
    public sealed partial class SceneRecord : ScriptableObject
    {
        private readonly ValueReference<SceneInstance> _sceneInstanceReference = new ();

        [SerializeField] [HideInInspector] private bool _customFlowNeeded;
        [field: SerializeField, Range(0, 100)] public ushort Priority { get; private set; } = 100;
        [field: SerializeField] public AssetReferenceScene Target { get; private set; } = default!;
        [field: SerializeField] public PlayerLoopTiming YieldPoint { get; private set; } = PlayerLoopTiming.Initialization;
        [SerializeField] internal UnityEvent _prefetched = new ();
        [SerializeField] internal UnityEvent _loaded = new ();
        [SerializeField] internal UnityEvent _unloaded = new ();
        [SerializeField] internal UnityEvent _completelyUnloaded = new ();

        private StateMachine.Frozen? _stateMachineFrozen;
        private StateMachine.Mutable? _stateMachineMutable;

        private Unloaded.Custom _unloadedState = default!;
        private Prefetched.Custom _prefetchedState = default!;
        private Activated.Custom _activatedState = default!;
        private Deactivated.Custom _deactivatedState = default!;

        [MustUseReturnValue] [MustDisposeResource]
        public Subscription LoadedSubscribe(UnityAction whenLoaded)
        {
            _loaded.AddListener(whenLoaded);

            return new Subscription(whenLoaded, _loaded);
        }

        [MustUseReturnValue] [MustDisposeResource]
        public Subscription UnloadedSubscribe(UnityAction whenUnloaded)
        {
            _unloaded.AddListener(whenUnloaded);

            return new Subscription(whenUnloaded, _unloaded);
        }

        [MustUseReturnValue]
        public SceneCodeBindings<TSceneKey> CreateCodeBindings<TSceneKey>() where TSceneKey : class, ISceneKey
        {
            return new SceneCodeBindings<TSceneKey>(this);
        }

        private AutoResetUniTaskCompletionSource? _prefetching;

        public async UniTask<AsyncRichResult> PrefetchAsync(CancellationToken cancellation = default)
        {
            _prefetching ??= AutoResetUniTaskCompletionSource.Create();

            LastOperation = await BootstrapAsync(cancellation);
            if (LastOperation.IsSuccessful is not true)
            {
                _prefetching.TrySetResult();
                return LastOperation;
            }

            if (_stateMachineFrozen is not null)
            {
                LastOperation = LastOperation.Combine(await _stateMachineFrozen.TransitAsync<Prefetch>(cancellation));
                if (LastOperation.IsSuccessful is not true)
                {
                    _prefetching.TrySetResult();
                    return LastOperation;
                }

                SceneManager.sceneUnloaded -= CleanSceneRecordState;
                SceneManager.sceneUnloaded += CleanSceneRecordState;

                if (LastOperation.IsSuccessful)
                {
                    _prefetched.Invoke();
                }

                _prefetching.TrySetResult();
                return LastOperation;
            }
            else
            {
                LastOperation = LastOperation.Combine(await _stateMachineMutable!.TransitAsync<Prefetch>(cancellation));
                if (LastOperation.IsSuccessful is not true)
                {
                    _prefetching.TrySetResult();
                    return LastOperation;
                }

                SceneManager.sceneUnloaded -= CleanSceneRecordState;
                SceneManager.sceneUnloaded += CleanSceneRecordState;

                _activatedState = new Activated.Custom(_sceneInstanceReference, _prefetchedState.CustomLoadedCollection, _prefetchedState.TrivialLoadedCollection);
                _deactivatedState = new Deactivated.Custom(_sceneInstanceReference, _prefetchedState.CustomUnloadedCollection, _prefetchedState.TrivialUnloadedCollection);

                _stateMachineFrozen =_stateMachineMutable
                    .AddTransition<Activate>(from: _prefetchedState, to: _activatedState)
                    .AddTransition<Deactivate>(from: _activatedState, to: _deactivatedState)
                    .AddTransition<Activate>(from: _deactivatedState, to: _activatedState)
                    .AddTransition<Unload>(from: _deactivatedState, to: _unloadedState)
                    .AddTransition<Unload>(from: _activatedState, to: _unloadedState)
                    .ToFrozen();

                _stateMachineMutable = null;

                if (LastOperation.IsSuccessful)
                {
                    _prefetched.Invoke();
                }

                _prefetching.TrySetResult();
                return LastOperation;
            }

            void CleanSceneRecordState(Scene toUnload)
            {
                if (_sceneInstanceReference.TryGetValue(out var loaded) && loaded.Value.Scene == toUnload)
                {
                    _sceneInstanceReference.Value = null;
                }

                SceneManager.sceneUnloaded -= CleanSceneRecordState;
            }
        }

        public async UniTask<AsyncRichResult> LoadAsync(CancellationToken cancellation = default)
        {
            if (_prefetching is null) return new Expected.Failure("Scene is not prefetched");

            await _prefetching.Task;
            _prefetching = null;
            LastOperation = LastOperation.Combine(await _stateMachineFrozen!.TransitAsync<Activate>(cancellation));

            if (LastOperation.IsSuccessful)
            {
                _loaded.Invoke();
            }
            return LastOperation;
        }


        public async UniTask<AsyncRichResult> UnloadAsync(CancellationToken cancellation = default)
        {
            LastOperation = LastOperation.Combine(await _stateMachineFrozen!.TransitAsync<Deactivate>(cancellation));

            if (LastOperation.IsSuccessful)
            {
                _unloaded.Invoke();
            }
            return LastOperation;
        }

        public async UniTask<AsyncRichResult> CompletelyUnloadAsync(CancellationToken cancellation = default)
        {
            LastOperation = LastOperation.Combine(await _stateMachineFrozen!.TransitAsync<Unload>(cancellation));

            if (LastOperation.IsSuccessful)
            {
                _completelyUnloaded.Invoke();
            }
            return LastOperation;
        }

        /// <summary>
        /// This section refers to API, designed to use in Unity Editor with UnityEvent
        /// </summary>
        #region Sync API

        public AsyncRichResult LastOperation { get; private set; } = AsyncRichResult.Success;
        public CancellationToken ApplicationLifetime { private get; set; } = CancellationToken.None;

        public void Prefetch()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await PrefetchAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Value.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [PREFETCHING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [PREFETCHING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [PREFETCHING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        public void Load()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            return;

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await LoadAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Value.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [LOADING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [LOADING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [LOADING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        public void Unload()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            return;

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await UnloadAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Value.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [UNLOADING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [UNLOADING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [UNLOADING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        public void CompletelyUnload()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            return;

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await CompletelyUnloadAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Value.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [COMPLETE UNLOADING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [COMPLETE UNLOADING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [COMPLETE UNLOADING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        #endregion

        private async UniTask<AsyncRichResult> BootstrapAsync(CancellationToken cancellation = default)
        {
            CleanUp();

            if (_customFlowNeeded)
            {
                _unloadedState = new Unloaded.Custom(_sceneInstanceReference, YieldPoint);
                _prefetchedState = new Prefetched.Custom(_sceneInstanceReference, Target, YieldPoint, Priority);

                _stateMachineMutable = StateMachine.Mutable.Create<Bootstrap>(startingWith: _unloadedState)
                    .AddTransition<Prefetch>(from: _unloadedState, to: _prefetchedState)
                    .AddTransition<Unload>(from: _prefetchedState, to: _unloadedState);

                return await _stateMachineMutable.TransitAsync<Bootstrap>(cancellation);
            }
            else
            {
                var unloaded = new Unloaded.Regular(_sceneInstanceReference, YieldPoint);
                var prefetched = new Prefetched.Regular(_sceneInstanceReference, Target, YieldPoint, Priority);
                var activated = new Activated.Regular(_sceneInstanceReference, YieldPoint);
                var deactivated = new Deactivated.Regular(_sceneInstanceReference, YieldPoint);

                _stateMachineFrozen = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<Bootstrap>(to: unloaded)
                    .WithTransition<Prefetch>(from: unloaded, to: prefetched)
                    .WithTransition<Activate>(from: prefetched, to: activated)
                    .WithTransition<Deactivate>(from: activated, to: deactivated)
                    .WithTransition<Activate>(from: deactivated, to: activated)
                    .WithTransition<Unload>(from: deactivated, to: unloaded)
                    .WithTransition<Unload>(from: activated, to: unloaded)
                    .WithTransition<Unload>(from: prefetched, to: unloaded)
                    .Build()
                    .ToFrozen();

                return await _stateMachineFrozen.TransitAsync<Bootstrap>(cancellation);
            }
        }

        // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        private void CleanUp()
        {
            _prefetchedState?.Dispose();

            _sceneInstanceReference.Value = null;
            _stateMachineMutable = null;
            _stateMachineFrozen = null;
            _unloadedState = default!;
            _prefetchedState = default!;
            _activatedState = default!;
            _deactivatedState = default!;
        }

        private void OnDisable()
        {
            _prefetchedState?.Dispose();
            _prefetchedState = null!;
        }
        // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

        private void OnDestroy() => OnDisable();

        public readonly struct Subscription : IDisposable
        {
            private readonly UnityAction _action;
            private readonly UnityEvent _event;

            internal Subscription(UnityAction action, UnityEvent @event)
            {
                _action = action;
                _event = @event;
            }

            public void Dispose()
            {
                _event.RemoveListener(_action);
            }
        }
    }
}
