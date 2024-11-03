#nullable enable

using System;
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

        private CancellationTokenSource _lifetime = default!;
        private IDisposable? _loadedSubscription;
        private IDisposable? _unloadedSubscription;

        private void OnEnable()
        {
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, CancellationToken.None);

            _loadedSubscription?.Dispose();
            _loadedSubscription ??= _scene.LoadedSubscribe(_loaded.Invoke);
            _unloadedSubscription?.Dispose();
            _unloadedSubscription ??= _scene.UnloadedSubscribe(_unloaded.Invoke);
        }

        private void OnDisable()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _loadedSubscription?.Dispose();
            _loadedSubscription = null;
            _unloadedSubscription?.Dispose();
            _unloadedSubscription = null;
        }

        public void Prefetch()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.PrefetchAsync(_lifetime.Token)
                    .ContinueWith(result =>
                    {
                        if (result.IsSuccessful)
                        {
                            _prefetched.Invoke();
                        }

                    }).Forget();
            }
        }

        public void Load()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.LoadAsync(_lifetime.Token)
                    .Forget();
            }
        }

        public void Unload()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.UnloadAsync(_lifetime.Token)
                    .Forget();
            }
        }

        public void CompletelyUnload()
        {
            if (_scene.LastOperation.IsSuccessful)
            {
                _scene.CompletelyUnloadAsync(_lifetime.Token)
                    .ContinueWith(result =>
                    {
                        if (result.IsSuccessful)
                        {
                            _completelyUnloaded.Invoke();
                        }

                    }).Forget();
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
