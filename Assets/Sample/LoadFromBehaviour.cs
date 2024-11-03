using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;
using UnityEngine;

namespace SceneLoader.Sample
{
    using Core;

    internal sealed class LoadFromBehaviour : MonoBehaviour
    {
        [SerializeField] private SceneRecord _scene = default!;

        [UsedImplicitly] // ReSharper disable once Unity.IncorrectMethodSignature
        private async UniTaskVoid OnEnable()
        {
            var result = AsyncRichResult.Success;

            result = result
                .Combine(await _scene.PrefetchAsync(destroyCancellationToken))
                .Combine(await _scene.LoadAsync(destroyCancellationToken));

            await UniTask.Delay(1000);

            result = result
                .Combine(await _scene.UnloadAsync(destroyCancellationToken));

            await UniTask.Delay(1000);

            result = result
                .Combine(await _scene.CompletelyUnloadAsync(destroyCancellationToken));

            result.Match
            (
                success: _ => Debug.Log("Success"),
                cancellation: () => Debug.Log("Canceled"),
                failure: error => Debug.LogError(error),
                error: Debug.LogError,
                destroyCancellationToken
            );
        }
    }
}
