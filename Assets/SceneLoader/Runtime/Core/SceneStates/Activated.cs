using System;
using System.Collections.Immutable;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Core.SceneStates
{
    using Abstract;

    internal abstract class Activated : IState
    {
        private readonly ValueReference<SceneInstance> _instance;

        private Activated(ValueReference<SceneInstance> instance)
        {
            _instance = instance;
        }

        public sealed class Regular : Activated, IState.IWithEnterAction
        {
            private readonly PlayerLoopTiming _yieldPoint;

            public Regular(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint) : base(instance)
            {
                _yieldPoint = yieldPoint;
            }

            async UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_instance.TryGetValue(out var scene) is false) return new Expected.Failure("Scene is not prefetched");
                if (scene.Scene.isLoaded) return new Expected.Failure("Scene is already activated");

                try
                {
                    var doesCanceled = await scene.ActivateAsync()
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

        public sealed class Custom : Activated, IState.IWithEnterAction
        {
            private readonly ImmutableArray<ISceneActivated> _activatedCollection;

            public Custom(ValueReference<SceneInstance> instance, ImmutableArray<ISceneActivated> activatedCollection) : base(instance)
            {
                _activatedCollection = activatedCollection;
            }

            UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);
                if (_instance.TryGetValue(out var scene) is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene is not prefetched"));
                if (scene.Scene.isLoaded is false) return UniTask.FromResult<AsyncRichResult>(new Expected.Failure("Scene should be activated in regular way in order to activate custom flow"));

                foreach (ref readonly var candidate in _activatedCollection.AsSpan())
                {
                    if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncRichResult.Cancel);

                    var result = candidate.Execute();

                    if (result.IsFailure) return UniTask.FromResult(result.Match
                    (
                        success: static () => AsyncRichResult.FromException(Unexpected.Impossible),
                        error: static exception => AsyncRichResult.FromException(exception)
                    ));
                }

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }
    }
}
