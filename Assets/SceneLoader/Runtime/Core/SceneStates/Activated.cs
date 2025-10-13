using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Core.SceneStates
{
    internal abstract class Activated : IState
    {
        private readonly ValueReference<SceneInstance> _instance;

        private Activated(ValueReference<SceneInstance> instance)
        {
            _instance = instance;
        }

        public sealed class Regular : Activated, IState.WithEnterAction
        {
            private readonly PlayerLoopTiming _yieldPoint;

            public Regular(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint) : base(instance)
            {
                _yieldPoint = yieldPoint;
            }

            async UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_instance.TryGetValue(out var scene) is false) return new Expected.Failure("Scene is not prefetched");
                if (scene.Value.Scene.isLoaded) return new Expected.Failure("Scene is already activated");

                try
                {
                    var doesCanceled = await scene.Value.ActivateAsync()
                        .ToUniTask(progress: null!, _yieldPoint, cancellation, cancelImmediately: true)
                        .SuppressCancellationThrow();

                    return doesCanceled
                        ? AsyncRichResult.Cancel
                        : AsyncRichResult.Success;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }
        }

        public sealed class Custom : Activated, IState.WithEnterAction
        {
            private readonly Prefetched.Custom _prefetchedState;

            public Custom(ValueReference<SceneInstance> instance, Prefetched.Custom prefetchedState) : base(instance)
            {
                _prefetchedState = prefetchedState;
            }

            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);
                if (_instance.TryGetValue(out var scene) is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene is not prefetched"));
                if (scene.Value.Scene.isLoaded is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene should be activated in regular way in order to activate custom flow"));

                var trivialActivatedCollection = _prefetchedState.TrivialLoadedCollection;
                var customActivatedCollection = _prefetchedState.CustomLoadedCollection;
                if (customActivatedCollection.IsDefaultOrEmpty) goto TrivialFlow;
                foreach (ref readonly var candidate in customActivatedCollection.AsSpan())
                {
                    if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

                    var result = candidate.Execute();

                    if (result.IsFailure) return UniTask.FromResult(result.Match
                    (
                        success: static () => AsyncRichResult.FromException(Unexpected.Impossible),
                        error: static exception => AsyncRichResult.FromException(exception)
                    ));
                }

                TrivialFlow: if (trivialActivatedCollection.Length > 0)
                {
                    GameObject.SetGameObjectsActive(trivialActivatedCollection, active: true);
                }

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }
    }
}
