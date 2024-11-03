using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace SceneLoader.Core.SceneStates
{
    using Abstract;

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

        public sealed class Regular : Prefetched, IState.IWithEnterAction
        {
            public Regular(ValueReference<SceneInstance> sceneInstanceReference, AssetReferenceScene target, PlayerLoopTiming yieldPoint, ushort priority) : base(sceneInstanceReference, target, yieldPoint, priority, customFlowNeeded: false)
            {
            }

            async UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
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

        public sealed class Custom : Prefetched, IState.IWithEnterAction
        {
            public ImmutableArray<ISceneActivated> ActivatedCollection { get; private set; } = ImmutableArray<ISceneActivated>.Empty;
            public ImmutableArray<ISceneUnloaded> UnloadedCollection { get; private set; } = ImmutableArray<ISceneUnloaded>.Empty;

            public Custom(ValueReference<SceneInstance> sceneInstanceReference, AssetReferenceScene target, PlayerLoopTiming yieldPoint, ushort priority) : base(sceneInstanceReference, target, yieldPoint, priority, customFlowNeeded: true)
            {
            }

            async UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
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

                    var roots = instance.Scene.GetRootGameObjects();
                    ActivatedCollection = roots
                        .Where(static current => current.TryGetComponent<ISceneActivated>(out _))
                        .Select(static current => current.GetComponent<ISceneActivated>())
                        .ToImmutableArray();
                    UnloadedCollection = roots
                        .Where(static current => current.TryGetComponent<ISceneUnloaded>(out _))
                        .Select(static current => current.GetComponent<ISceneUnloaded>())
                        .ToImmutableArray();

                    return AsyncRichResult.Success;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }
        }
    }
}
