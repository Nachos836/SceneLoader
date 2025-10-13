using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

using Addressable = UnityEngine.AddressableAssets.Addressables;

namespace SceneLoader.Core.SceneStates
{
    internal abstract class Deactivated : IState
    {
        private readonly ValueReference<SceneInstance> _instance;

        private Deactivated(ValueReference<SceneInstance> instance)
        {
            _instance = instance;
        }

        public sealed class Regular : Deactivated, IState.WithEnterAction
        {
            private readonly PlayerLoopTiming _yieldPoint;

            public Regular(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint) : base(instance)
            {
                _yieldPoint = yieldPoint;
            }

            async UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_instance.TryGetValue(out var scene) is false) return new Expected.Failure("Scene is not activated/loaded");
                if (scene.Value.Scene.isLoaded is false) return AsyncRichResult.Success;

                try
                {
                    var (isCanceled, instance) = await Addressable.UnloadSceneAsync(scene.Value, autoReleaseHandle: true)
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

        public sealed class Custom : Deactivated, IState.WithEnterAction
        {
            private readonly Prefetched.Custom _prefetchedState;

            public Custom(ValueReference<SceneInstance> instance, Prefetched.Custom prefetchedState) : base(instance)
            {
                _prefetchedState = prefetchedState;
            }

            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);
                if (_instance.TryGetValue(out var scene) is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene is activated/loaded"));
                if (scene.Value.Scene.isLoaded is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene should be activated in regular way in order to activate custom flow"));

                var customUnloadedCollection = _prefetchedState.CustomUnloadedCollection;
                var trivialUnloadedCollection = _prefetchedState.TrivialUnloadedCollection;
                if (customUnloadedCollection.IsDefaultOrEmpty) goto TrivialFlow;
                foreach (ref readonly var candidate in customUnloadedCollection.AsSpan())
                {
                    if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

                    var result = candidate.Execute();

                    if (result.IsFailure) return UniTask.FromResult(result.Match
                    (
                        success: static () => AsyncRichResult.FromException(Unexpected.Impossible),
                        error: static exception => AsyncRichResult.FromException(exception)
                    ));
                }

                TrivialFlow: if (trivialUnloadedCollection.Length > 0)
                {
                    GameObject.SetGameObjectsActive(trivialUnloadedCollection, active: false);
                }

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }
    }
}
