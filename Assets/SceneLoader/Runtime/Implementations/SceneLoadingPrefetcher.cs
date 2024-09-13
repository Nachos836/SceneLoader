using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace SceneLoader.Implementations
{
    public readonly ref struct SceneLoadingPrefetcher
    {
        private readonly AssetReference _target;
        private readonly bool _isSceneWithInactiveRoots;
        private readonly PlayerLoopTiming _yieldPoint;
        private readonly ushort _priority;

        public SceneLoadingPrefetcher
        (
            AssetReference target,
            bool isSceneWithInactiveRoots,
            PlayerLoopTiming yieldPoint = PlayerLoopTiming.Initialization,
            ushort priority = 100
        ) {
            _target = target;
            _isSceneWithInactiveRoots = isSceneWithInactiveRoots;
            _yieldPoint = yieldPoint;
            _priority = priority;
        }

        public ActivationHandler PrefetchAsync(LifetimeScope parent, IInstaller arguments, CancellationToken cancellation = default)
        {
            var target = _target;
            var priority = _priority;
            var yieldPoint = _yieldPoint;

            using (LifetimeScope.EnqueueParent(parent))
            {
                using (LifetimeScope.Enqueue(arguments))
                {
                    return new ActivationHandler
                    (
                        continuation: LoadWithWorkaroundDelayAsync
                        (
                            target,
                            yieldPoint,
                            activateOnLoad: _isSceneWithInactiveRoots,
                            priority,
                            cancellation
                        ).ToAsyncLazy(),
                        _yieldPoint,
                        _isSceneWithInactiveRoots
                    );
                }
            }
        }

        public ActivationHandler PrefetchAsync(CancellationToken cancellation = default)
        {
            var target = _target;
            var priority = _priority;
            var yieldPoint = _yieldPoint;

            return new ActivationHandler
            (
                continuation: LoadWithWorkaroundDelayAsync
                (
                    target,
                    yieldPoint,
                    activateOnLoad: _isSceneWithInactiveRoots,
                    priority,
                    cancellation
                ).ToAsyncLazy(),
                _yieldPoint,
                _isSceneWithInactiveRoots
            );
        }

        /// <summary>
        /// Workaround for https://issuetracker.unity3d.com/issues/loadsceneasync-allowsceneactivation-flag-is-ignored-in-awake
        /// This workaround could be removed if versions of packages will be
        /// <code>
        /// "com.unity.addressables": "1.8.5"
        /// "com.unity.scriptablebuildpipeline": "1.7.3"
        /// </code>
        /// Thus compatibility and builds reliability are not guaranteed
        /// </summary>
        private static async UniTask<SceneInstance> LoadWithWorkaroundDelayAsync
        (
            AssetReference target,
            PlayerLoopTiming yieldPoint,
            bool activateOnLoad,
            int priority,
            CancellationToken token = default
        ) {
            await UniTask.Yield(yieldPoint, token)
                .SuppressCancellationThrow();

            return await target
                .LoadSceneAsync(loadMode: LoadSceneMode.Additive, activateOnLoad, priority)
                .ToUniTask(timing: yieldPoint, cancellationToken: token, cancelImmediately: true)
                .SuppressCancellationThrow()
                .ContinueWith(static tuple => tuple.Result);
        }

        public readonly struct ActivationHandler
        {
            private readonly AsyncLazy<SceneInstance> _continuation;
            private readonly PlayerLoopTiming _yieldContext;
            private readonly bool _customActivationNeeded;

            public ActivationHandler(AsyncLazy<SceneInstance> continuation, PlayerLoopTiming yieldContext, bool customActivationNeeded)
            {
                _continuation = continuation;
                _yieldContext = yieldContext;
                _customActivationNeeded = customActivationNeeded;
            }

            public UniTask<AsyncResult<SceneInstance>> ActivateAsync(CancellationToken cancellation = default)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult(AsyncResult<SceneInstance>.Cancel);

                return _customActivationNeeded
                    ? CustomAsyncActivation(_continuation, cancellation)
                    : RegularAsyncActivation(_continuation, _yieldContext, cancellation);

                static async UniTask<AsyncResult<SceneInstance>> RegularAsyncActivation(AsyncLazy<SceneInstance> continuation, PlayerLoopTiming yieldContext, CancellationToken cancellation)
                {
                    var sceneInstance = await continuation.Task;

                    var activationWasCanceled = await sceneInstance.ActivateAsync()
                        .ToUniTask(timing: yieldContext, cancellationToken: cancellation, cancelImmediately: true)
                        .SuppressCancellationThrow();

                    return activationWasCanceled is not true
                        ? AsyncResult<SceneInstance>.FromResult(sceneInstance)
                        : AsyncResult<SceneInstance>.Cancel;
                }

                static async UniTask<AsyncResult<SceneInstance>> CustomAsyncActivation(AsyncLazy<SceneInstance> continuation, CancellationToken cancellation)
                {
                    var sceneInstance = await continuation.Task;

                    var candidates = sceneInstance.Scene.GetRootGameObjects();
                    var scope = candidates.SingleOrDefault(static candidate =>
                    {
                        if (candidate.TryGetComponent(out LifetimeScope scope) is not true) return false;

                        scope.Build();

                        return true;
                    });

                    foreach (var candidate in candidates.Where(candidate => candidate != scope))
                    {
                        candidate.SetActive(true);
                    }

                    return cancellation.IsCancellationRequested is not true
                        ? AsyncResult<SceneInstance>.FromResult(sceneInstance)
                        : AsyncResult<SceneInstance>.Cancel;
                }
            }
        }
    }
}
