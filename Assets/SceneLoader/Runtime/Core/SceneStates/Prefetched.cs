using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using Unity.Collections;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace SceneLoader.Core.SceneStates
{
    using Abstract;
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
            public NativeArray<int>.ReadOnly TrivialLoadedCollection { get; private set; }
            public ImmutableArray<ISceneUnloadedDetectionCustom> CustomUnloadedCollection { get; private set; }
            public NativeArray<int>.ReadOnly TrivialUnloadedCollection { get; private set; }

            private NativeArray<int>? _trivialLoadedCollection;
            private NativeArray<int>? _trivialUnloadedCollection;

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
                        .ToUniTask(progress: null!, timing: _yieldPoint, cancellation, cancelImmediately: true, autoReleaseWhenCanceled: true)
                        .SuppressCancellationThrow();

                    if (isCanceled) return AsyncRichResult.Cancel;

                    _sceneInstanceReference.Value = instance;

                    var roots = ImmutableCollectionsMarshal.AsImmutableArray(instance.Scene.GetRootGameObjects());
                    var trivialLoadedCollectionCount = 0;
                    var trivialUnloadedCollectionCount = 0;

                    var customLoadedCollectionBuilder = ImmutableArray.CreateBuilder<ISceneLoadedDetectionCustom>(roots.Length);
                    var trivialLoadedCollectionBuilder = new NativeArray<int>(roots.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    var customUnloadedCollectionBuilder = ImmutableArray.CreateBuilder<ISceneUnloadedDetectionCustom>(roots.Length);
                    var trivialUnloadedCollectionBuilder = new NativeArray<int>(roots.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                    foreach (var root in roots)
                    {
                        if (root.TryGetComponent(out ISceneLoadedDetectionCustom loadedCustom))
                        {
                            customLoadedCollectionBuilder.Add(loadedCustom);
                        }
                        else if (root.TryGetComponent(out ISceneLoadedDetection loadedTrivial))
                        {
                            trivialLoadedCollectionBuilder[trivialLoadedCollectionCount] = loadedTrivial.Id;
                            ++trivialLoadedCollectionCount;
                        }

                        if (root.TryGetComponent(out ISceneUnloadedDetectionCustom unloadedCustom))
                        {
                            customUnloadedCollectionBuilder.Add(unloadedCustom);
                        }
                        else if (root.TryGetComponent(out ISceneUnloadedDetection unloadedTrivial))
                        {
                            trivialUnloadedCollectionBuilder[trivialUnloadedCollectionCount] = unloadedTrivial.Id;
                            ++trivialUnloadedCollectionCount;
                        }
                    }

                    CustomLoadedCollection = customLoadedCollectionBuilder.DrainToImmutable();
                    if (trivialLoadedCollectionCount != 0)
                    {
                        _trivialLoadedCollection = trivialLoadedCollectionBuilder;
                        TrivialLoadedCollection = trivialLoadedCollectionBuilder.AsReadOnly(trivialLoadedCollectionCount);
                    }
                    else
                    {
                        trivialLoadedCollectionBuilder.Dispose();
                    }

                    CustomUnloadedCollection = customUnloadedCollectionBuilder.DrainToImmutable();
                    if (trivialUnloadedCollectionCount != 0)
                    {
                        _trivialUnloadedCollection = trivialUnloadedCollectionBuilder;
                        TrivialUnloadedCollection = trivialUnloadedCollectionBuilder.AsReadOnly(trivialUnloadedCollectionCount);
                    }
                    else
                    {
                        trivialUnloadedCollectionBuilder.Dispose();
                    }

                    return AsyncRichResult.Success;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }

            public void Dispose()
            {
                _trivialLoadedCollection?.Dispose();
                _trivialLoadedCollection = null;
                _trivialUnloadedCollection?.Dispose();
                _trivialUnloadedCollection = null;
            }
        }
    }
}
