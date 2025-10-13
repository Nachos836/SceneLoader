using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Functional.Async;
using UnityEngine;
using VContainer.Unity;

using SceneLoader.Abstract;

namespace SceneLoader.Sample
{
    internal sealed class SceneLoadingEntry : IAsyncStartable
    {
        private readonly ISceneLoader<FirstScene> _firstSceneLoader;
        private readonly ISceneLoader<SecondScene> _secondSceneLoader;
        private readonly ISceneUnloader<FirstScene> _firstSceneUnloader;
        private readonly ISceneUnloader<SecondScene> _secondSceneUnloader;

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
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cancellation);

            Debug.Log("Scene loading started");

            var loadedCount = await UniTask.WhenEach(tasks: new []
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
                        Debug.LogFormat("Scene \"{0}\" loading finished!", sceneName);

                        return AsyncResult.Success;
                    },
                    cancellation: static () =>
                    {
                        Debug.LogWarningFormat("Scene loading was cancelled!");

                        return AsyncResult.Cancel;
                    },
                    error: static exception =>
                    {
                        Debug.LogErrorFormat("Scene loading failed due to error \"{0}\" !", exception);

                        return AsyncResult.FromException(exception);
                    }
                ).IsSuccessful;
            }, cancellationToken: cancellation);

            if (loadedCount is 2)
            {
                Debug.Log("All scenes was loaded");
            }

            await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: cancellation);

            var unloadedCount = await UniTask.WhenEach(tasks: new[]
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
                        Debug.LogFormat("Scene \"{0}\" unloading finished!", sceneName);

                        return AsyncResult.Success;
                    },
                    cancellation: static () =>
                    {
                        Debug.LogWarningFormat("Scene unloading was cancelled!");

                        return AsyncResult.Cancel;
                    },
                    error: static exception =>
                    {
                        Debug.LogErrorFormat("Scene unloading failed due to error \"{0}\" !", exception);

                        return AsyncResult.FromException(exception);
                    }
                ).IsSuccessful;
            }, cancellationToken: cancellation);

            if (unloadedCount is 2)
            {
                Debug.Log("All scenes was unloaded");
            }
        }
    }
}
