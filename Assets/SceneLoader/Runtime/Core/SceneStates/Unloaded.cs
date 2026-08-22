#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Core.SceneStates
{
    internal sealed class Unloaded : IState, IState.WithEnterAction
    {
        private readonly ValueReference<SceneInstance> _instance;
        private readonly PlayerLoopTiming _yieldPoint;

        public Unloaded(ValueReference<SceneInstance> instance, PlayerLoopTiming yieldPoint)
        {
            _instance = instance;
            _yieldPoint = yieldPoint;
        }

        async UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;
            if (_instance.TryGetValue(out var scene) is false) return AsyncRichResult.Success;
            if (scene.Value.Scene.isLoaded is false) return AsyncRichResult.Success;

            try
            {
                var (isCanceled, instance) = await Addressables.UnloadSceneAsync(scene.Value, autoReleaseHandle: true)
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
}
