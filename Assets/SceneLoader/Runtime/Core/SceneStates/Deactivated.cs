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
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Core.SceneStates
{
    using Abstract;

    internal abstract class Deactivated : IState
    {
        private readonly ValueReference<SceneInstance> _instance;

        private Deactivated(ValueReference<SceneInstance> instance)
        {
            _instance = instance;
        }

        public sealed class Regular : Deactivated, IState.IWithEnterAction
        {
            private readonly PlayerLoopTiming _yieldPoint;

            public Regular(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint) : base(instance)
            {
                _yieldPoint = yieldPoint;
            }

            async UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_instance.TryGetValue(out var scene) is false) return new Expected.Failure("Scene is not activated/loaded");
                if (scene.Scene.isLoaded is false) return AsyncRichResult.Success;

                try
                {
                    var (isCanceled, instance) = await UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(scene, autoReleaseHandle: true)
                        .ToUniTask(progress: null!, _yieldPoint, cancellation, cancelImmediately: true, autoReleaseWhenCanceled: true)
                        .SuppressCancellationThrow();

                    if (isCanceled) return AsyncRichResult.Cancel;

                    _instance.Value = instance;

                    return AsyncRichResult.Success;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }
        }

        public sealed class Custom : Deactivated, IState.IWithEnterAction
        {
            private readonly ImmutableArray<ISceneUnloadedDetectionCustom> _customUnloadedCollection;
            private readonly NativeArray<int>.ReadOnly _trivialUnloadedCollection;

            public Custom
            (
                ValueReference<SceneInstance> instance, ImmutableArray<ISceneUnloadedDetectionCustom> customUnloadedCollection,
                NativeArray<int>.ReadOnly trivialUnloadedCollection
            )
                : base(instance)
            {
                _customUnloadedCollection = customUnloadedCollection;
                _trivialUnloadedCollection = trivialUnloadedCollection;
            }

            UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);
                if (_instance.TryGetValue(out var scene) is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene is activated/loaded"));
                if (scene.Scene.isLoaded is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene should be activated in regular way in order to activate custom flow"));

                if (_customUnloadedCollection.IsDefaultOrEmpty) goto TrivialFlow;
                foreach (ref readonly var candidate in _customUnloadedCollection.AsSpan())
                {
                    if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

                    var result = candidate.Execute();

                    if (result.IsFailure) return UniTask.FromResult(result.Match
                    (
                        success: static () => AsyncRichResult.FromException(Unexpected.Impossible),
                        error: static exception => AsyncRichResult.FromException(exception)
                    ));
                }

                TrivialFlow: if (_trivialUnloadedCollection.Length > 0)
                {
                    GameObject.SetGameObjectsActive(_trivialUnloadedCollection, active: false);
                }

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }
    }
}
