#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace SceneLoader.Core.Components
{
    [AddComponentMenu("Scene Loader/Scene's Custom Flow Components/Scene Events Behaviour Listener")]
    internal sealed class SceneRecordListener : MonoBehaviour
    {
        [SerializeField] private SceneRecord _scene = default!;
        [SerializeField] private UnityEvent _prefetched = new ();
        [SerializeField] private UnityEvent _loaded = new ();
        [SerializeField] private UnityEvent _unloaded = new ();
        [SerializeField] private UnityEvent _completelyUnloaded = new ();

        private CancellationTokenSource? _lifetime;

        private void OnEnable()
        {
            _lifetime?.Dispose();
            _lifetime ??= CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, CancellationToken.None);

            _scene._prefetched.AddListener(_prefetched.Invoke);
            _scene._loaded.AddListener(_loaded.Invoke);
            _scene._unloaded.AddListener(_unloaded.Invoke);
            _scene._completelyUnloaded.AddListener(_completelyUnloaded.Invoke);
        }

        private void OnDisable()
        {
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;

            _scene._completelyUnloaded.RemoveListener(_completelyUnloaded.Invoke);
            _scene._unloaded.RemoveListener(_unloaded.Invoke);
            _scene._loaded.RemoveListener(_loaded.Invoke);
            _scene._prefetched.RemoveListener(_prefetched.Invoke);
        }

        public void Prefetch()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.PrefetchAsync(_lifetime!.Token)
                    .Forget();
            }
        }

        public void Load()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.LoadAsync(_lifetime!.Token)
                    .Forget();
            }
        }

        public void Unload()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.UnloadAsync(_lifetime!.Token)
                    .Forget();
            }
        }

        public void CompletelyUnload()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.CompletelyUnloadAsync(_lifetime!.Token)
                    .Forget();
            }
        }
    }

# if UNITY_EDITOR

    [UnityEditor.CustomEditor(typeof(SceneRecordListener))]
    internal sealed class SceneRecordListenerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var listener = (SceneRecordListener) target;

            if (GUILayout.Button("Prefetch"))
            {
                listener.Prefetch();
            }

            if (GUILayout.Button("Load"))
            {
                listener.Load();
            }

            if (GUILayout.Button("Unload"))
            {
                listener.Unload();
            }

            if (GUILayout.Button("Completely Unload"))
            {
                listener.CompletelyUnload();
            }
        }
    }

# endif

}
