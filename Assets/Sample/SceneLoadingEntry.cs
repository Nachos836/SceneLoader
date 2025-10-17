using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Functional.Async;
using UnityEngine;
using VContainer.Unity;

using SceneLoader.Abstract;
using VContainer;

namespace SceneLoader.Sample
{
    internal sealed class SceneLoadingEntry : IAsyncStartable
    {
        private readonly ISceneLoader<FirstScene> _firstSceneLoader;
        private readonly ISceneLoader<SecondScene> _secondSceneLoader;
        private readonly ISceneUnloader<FirstScene> _firstSceneUnloader;
        private readonly ISceneUnloader<SecondScene> _secondSceneUnloader;

        [Inject]
        public SceneLoadingEntry
        (
            ISceneLoader<FirstScene> firstSceneLoader,
            ISceneLoader<SecondScene> secondSceneLoader,
            ISceneUnloader<FirstScene> firstSceneUnloader,
            ISceneUnloader<SecondScene> secondSceneUnloader
        ) {

            _firstSceneLoader = firstSceneLoader;
            _secondSceneLoader = secondSceneLoader;
            _firstSceneUnloader = firstSceneUnloader;
            _secondSceneUnloader = secondSceneUnloader;
        }

        async UniTask IAsyncStartable.StartAsync(CancellationToken cancellation)
        {
            Debug.Log("[SceneLoadingEntry] Started!");

            var canceled = await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cancellation)
                .SuppressCancellationThrow();
            if (canceled)
            {
                Debug.LogWarning("[SceneLoadingEntry] cancelled at: Initial Delay");
                return;
            }

            Debug.Log("[SceneLoadingEntry] Loading Scenes...");

            int loadedCount;
            (canceled, loadedCount) = await UniTask.WhenEach(tasks: new []
            {
                _firstSceneLoader.LoadAsync(cancellation)
                    .ContinueWith(static result => UniTask.FromResult(result.AsAsyncResult().Attach(nameof(FirstScene)))),
                _secondSceneLoader.LoadAsync(cancellation)
                    .ContinueWith(static result => UniTask.FromResult(result.AsAsyncResult().Attach(nameof(SecondScene))))

            }).CountAsync(static loading =>
            {
                return loading.Result.Match
                (
                    success: static sceneName =>
                    {
                        Debug.LogFormat("[SceneLoadingEntry] Scene \"{0}\" loading finished!", sceneName);

                        return AsyncResult.Success;
                    },
                    cancellation: static () =>
                    {
                        Debug.LogWarningFormat("[SceneLoadingEntry] Scene loading was cancelled!");

                        return AsyncResult.Cancel;
                    },
                    error: static exception =>
                    {
                        Debug.LogErrorFormat("[SceneLoadingEntry] Scene loading failed due to error \"{0}\" !", exception);

                        return AsyncResult.FromException(exception);
                    }
                ).IsSuccessful;
            }, cancellationToken: cancellation)
                .SuppressCancellationThrow();

            if (canceled)
            {
                Debug.LogWarning("[SceneLoadingEntry] cancelled at: Scenes Loading");
                return;
            }

            if (loadedCount is 2)
            {
                Debug.Log("[SceneLoadingEntry] All scenes was loaded");
            }
            else
            {
                Debug.LogError("[SceneLoadingEntry] Failed to load all scenes");
                return;
            }

            canceled = await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: cancellation)
                .SuppressCancellationThrow();

            if (canceled)
            {
                Debug.LogWarning("[SceneLoadingEntry] cancelled at: Delay after scenes loading");
                return;
            }

            Debug.Log("[SceneLoadingEntry] Unloading Scenes...");

            int unloadedCount;
            (canceled, unloadedCount)  = await UniTask.WhenEach(tasks: new[]
            {
                _firstSceneUnloader.UnloadAsync(cancellation)
                    .ContinueWith(static result => UniTask.FromResult(result.AsAsyncResult().Attach(nameof(FirstScene)))),
                _secondSceneUnloader.UnloadAsync(cancellation)
                    .ContinueWith(static result => UniTask.FromResult(result.AsAsyncResult().Attach(nameof(SecondScene))))
            }).CountAsync(static unloading =>
            {
                return unloading.Result.Match
                (
                    success: static sceneName =>
                    {
                        Debug.LogFormat("[SceneLoadingEntry] Scene \"{0}\" unloading finished!", sceneName);

                        return AsyncResult.Success;
                    },
                    cancellation: static () =>
                    {
                        Debug.LogWarningFormat("[SceneLoadingEntry] Scene unloading was cancelled!");

                        return AsyncResult.Cancel;
                    },
                    error: static exception =>
                    {
                        Debug.LogErrorFormat("[SceneLoadingEntry] Scene unloading failed due to error \"{0}\" !", exception);

                        return AsyncResult.FromException(exception);
                    }
                ).IsSuccessful;
            }, cancellationToken: cancellation)
                .SuppressCancellationThrow();

            if (canceled)
            {
                Debug.LogWarning("[SceneLoadingEntry] cancelled at: Scenes Unloading");
                return;
            }

            if (unloadedCount is 2)
            {
                Debug.Log("[SceneLoadingEntry] All scenes was unloaded");
            }
            else
            {
                Debug.LogError("[SceneLoadingEntry] Failed to unload all scenes!");
            }
        }
    }
}
