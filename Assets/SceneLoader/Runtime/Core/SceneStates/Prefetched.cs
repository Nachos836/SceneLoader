using System;
using System.Collections.Immutable;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace SceneLoader.Core.SceneStates
{
    using Abstract.Internal;
    using Runtime.Core.Internal;

    internal abstract class Prefetched : IState
    {
        private readonly ValueReference<SceneInstance> _sceneInstanceReference;
        private readonly AssetReferenceScene _target;
        private readonly PlayerLoopTiming _yieldPoint;
        private readonly ushort _priority;
        private readonly bool _customFlowNeeded;

        private Prefetched
        (
            ValueReference<SceneInstance> sceneInstanceReference,
            AssetReferenceScene target,
            PlayerLoopTiming yieldPoint,
            ushort priority,
            bool customFlowNeeded
        ) {
            _sceneInstanceReference = sceneInstanceReference;
            _target = target;
            _yieldPoint = yieldPoint;
            _priority = priority;
            _customFlowNeeded = customFlowNeeded;
        }

        public sealed class Regular : Prefetched, IState.WithEnterAction
        {
            public Regular(ValueReference<SceneInstance> sceneInstanceReference, AssetReferenceScene target, PlayerLoopTiming yieldPoint, ushort priority)
                : base(sceneInstanceReference, target, yieldPoint, priority, customFlowNeeded: false)
            {
            }

            async UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_sceneInstanceReference.TryGetValue(out _)) return new Expected.Failure("Scene is already loaded");

                // Workaround for https://issuetracker.unity3d.com/issues/loadsceneasync-allowsceneactivation-flag-is-ignored-in-awake
                // This workaround could be removed if versions of packages will be
                //      "com.unity.addressables": "1.8.5"
                //      "com.unity.scriptablebuildpipeline": "1.7.3"
                // Thus compatibility and builds reliability are not guaranteed
                if (await UniTask.Yield(_yieldPoint, cancellation, cancelImmediately: true).SuppressCancellationThrow())
                    return AsyncRichResult.Cancel;

                try
                {
                    var (isCanceled, sceneInstance) = await _target
                        .LoadSceneAsync(loadMode: LoadSceneMode.Additive, _customFlowNeeded, _priority)
                        .ToUniTask(progress: null!, timing: _yieldPoint, cancellation, cancelImmediately: true, autoReleaseWhenCanceled: true)
                        .SuppressCancellationThrow();

                    if (isCanceled) return AsyncRichResult.Cancel;

                    _sceneInstanceReference.Value = sceneInstance;

                    return AsyncRichResult.Success;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }
        }

        public sealed class Custom : Prefetched, IState.WithEnterAction, IDisposable
        {
            public ImmutableArray<ISceneLoadedDetectionCustom> CustomLoadedCollection { get; private set; }
            public NativeArray<EntityId>.ReadOnly TrivialLoadedCollection { get; private set; }
            public ImmutableArray<ISceneUnloadedDetectionCustom> CustomUnloadedCollection { get; private set; }
            public NativeArray<EntityId>.ReadOnly TrivialUnloadedCollection { get; private set; }

            private NativeArray<EntityId> _trivialLoadedCollection;
            private NativeArray<EntityId> _trivialUnloadedCollection;
            private bool _disposed;

            public Custom(ValueReference<SceneInstance> sceneInstanceReference, AssetReferenceScene target, PlayerLoopTiming yieldPoint, ushort priority)
                : base(sceneInstanceReference, target, yieldPoint, priority, customFlowNeeded: true)
            {
            }

            async UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_sceneInstanceReference.TryGetValue(out _)) return new Expected.Failure("Scene is already loaded");

                // Workaround for https://issuetracker.unity3d.com/issues/loadsceneasync-allowsceneactivation-flag-is-ignored-in-awake
                // This workaround could be removed if versions of packages will be
                //      "com.unity.addressables": "1.8.5"
                //      "com.unity.scriptablebuildpipeline": "1.7.3"
                // Thus compatibility and builds reliability are not guaranteed
                if (await UniTask.Yield(_yieldPoint, cancellation, cancelImmediately: true).SuppressCancellationThrow())
                    return AsyncRichResult.Cancel;

                try
                {
                    var (isCanceled, instance) = await _target
                        .LoadSceneAsync(loadMode: LoadSceneMode.Additive, _customFlowNeeded, _priority)
                        .ToUniTask(progress: null!, timing: _yieldPoint, cancellation, cancelImmediately: true,
                            autoReleaseWhenCanceled: true)
                        .SuppressCancellationThrow();

                    if (isCanceled) return AsyncRichResult.Cancel;

                    cancellation.ThrowIfCancellationRequested();

                    _sceneInstanceReference.Value = instance;

                    using var _ = ListPool<GameObject>.Get(out var roots);

                    instance.Scene.GetRootGameObjects(roots);
                    var trivialLoadedCollectionCount = 0;
                    var trivialUnloadedCollectionCount = 0;

                    var customLoadedCollectionBuilder = ImmutableArray.CreateBuilder<ISceneLoadedDetectionCustom>(roots.Count);
                    _trivialLoadedCollection = new NativeArray<EntityId>(roots.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    var customUnloadedCollectionBuilder = ImmutableArray.CreateBuilder<ISceneUnloadedDetectionCustom>(roots.Count);
                    _trivialUnloadedCollection = new NativeArray<EntityId>(roots.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    foreach (var root in roots)
                    {
                        if (root.TryGetComponent(out ISceneLoadedDetection loadedTrivial))
                        {
                            _trivialLoadedCollection[trivialLoadedCollectionCount] = loadedTrivial.Id;
                            ++trivialLoadedCollectionCount;
                        }
                        else if (root.TryGetComponent(out ISceneLoadedDetectionCustom loadedCustom))
                        {
                            customLoadedCollectionBuilder.Add(loadedCustom);
                        }

                        if (root.TryGetComponent(out ISceneUnloadedDetection unloadedTrivial))
                        {
                            _trivialUnloadedCollection[trivialUnloadedCollectionCount] = unloadedTrivial.Id;
                            ++trivialUnloadedCollectionCount;
                        }
                        else if (root.TryGetComponent(out ISceneUnloadedDetectionCustom unloadedCustom))
                        {
                            customUnloadedCollectionBuilder.Add(unloadedCustom);
                        }
                    }

                    CustomLoadedCollection = customLoadedCollectionBuilder.DrainToImmutable();
                    if (trivialLoadedCollectionCount != 0)
                    {
                        TrivialLoadedCollection = _trivialLoadedCollection.AsReadOnly(trivialLoadedCollectionCount);
                    }
                    else
                    {
                        _trivialLoadedCollection.Dispose();
                    }

                    CustomUnloadedCollection = customUnloadedCollectionBuilder.DrainToImmutable();
                    if (trivialUnloadedCollectionCount != 0)
                    {
                        TrivialUnloadedCollection = _trivialUnloadedCollection.AsReadOnly(trivialUnloadedCollectionCount);
                    }
                    else
                    {
                        _trivialUnloadedCollection.Dispose();
                    }

                    return AsyncRichResult.Success;
                }
                catch (OperationCanceledException)
                {
                    Dispose();

                    return AsyncRichResult.Cancel;
                }
                catch (Exception exception)
                {
                    Dispose();

                    return exception;
                }
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;

                if (_trivialLoadedCollection.IsCreated)
                {
                    _trivialLoadedCollection.Dispose();
                }

                if (_trivialUnloadedCollection.IsCreated)
                {
                    _trivialUnloadedCollection.Dispose();
                }
            }
        }
    }
}
