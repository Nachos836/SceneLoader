using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Core.SceneStates
{
    internal abstract class Unloaded : IState
    {
        private readonly ValueReference<SceneInstance> _instance;

        private Unloaded(ValueReference<SceneInstance> instance)
        {
            _instance = instance;
        }

        public sealed class Regular : Unloaded, IState.IWithEnterAction
        {
            private readonly PlayerLoopTiming _yieldPoint;

            public Regular(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint) : base(instance)
            {
                _yieldPoint = yieldPoint;
            }

            async UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_instance.TryGetValue(out var scene) is false) return AsyncRichResult.Success;
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

        public sealed class Custom : Unloaded, IState.IWithEnterAction
        {
            private readonly PlayerLoopTiming _yieldPoint;

            public Custom(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint) : base(instance)
            {
                _yieldPoint = yieldPoint;
            }

            async UniTask<AsyncRichResult> IState.IWithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
                if (_instance.TryGetValue(out var scene) is false) return AsyncRichResult.Success;
                if (scene.Scene.isLoaded is false) return AsyncRichResult.Success;

                try
                {
                    var (isCanceled, _) = await UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(scene, autoReleaseHandle: true)
                        .ToUniTask(progress: null!, _yieldPoint, cancellation, cancelImmediately: true, autoReleaseWhenCanceled: true)
                        .SuppressCancellationThrow();

                    if (isCanceled) return AsyncRichResult.Cancel;

                    _instance.Value = null;

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
