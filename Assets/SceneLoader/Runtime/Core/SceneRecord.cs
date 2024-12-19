#nullable enable

using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace SceneLoader.Core
{
    using Abstract;
    using SceneStates;

    [UsedImplicitly] internal sealed record Bootstrap;
    [UsedImplicitly] internal sealed record Prefetch;
    [UsedImplicitly] internal sealed record Activate;
    [UsedImplicitly] internal sealed record Deactivate;
    [UsedImplicitly] internal sealed record Unload;

    [CreateAssetMenu(menuName = "Scene/Scene Record")]
    public sealed class SceneRecord : ScriptableObject
    {
        private readonly ValueReference<SceneInstance> _sceneInstanceReference = new ();
        private readonly MPMCEventBus _loadedEvent = new ();
        private readonly MPMCEventBus _unloadedEvent = new ();

        [FormerlySerializedAs("customFlowNeeded")] [SerializeField, HideInInspector]
        private bool _customFlowNeeded;

        [field: SerializeField, Range(0, 100)] public ushort Priority { get; private set; } = 100;
        [field: SerializeField] public AssetReferenceScene Target { get; private set; } = default!;
        [field: SerializeField] public PlayerLoopTiming YieldPoint { get; private set; } = PlayerLoopTiming.Initialization;

        private StateMachine.Frozen? _stateMachineFrozen;
        private StateMachine.Mutable? _stateMachineMutable;

        private Unloaded.Custom _unloaded = default!;
        private Prefetched.Custom _prefetched = default!;
        private Activated.Custom _activated = default!;
        private Deactivated.Custom _deactivated = default!;

        [Pure] public IDisposable LoadedSubscribe(Action whenLoaded) => _loadedEvent.Subscribe(whenLoaded);
        [Pure] public IDisposable UnloadedSubscribe(Action whenUnloaded) => _unloadedEvent.Subscribe(whenUnloaded);

        public SceneCodeBindings<TSceneKey> CreateCodeBindings<TSceneKey>() where TSceneKey : class, ISceneKey
        {
            return new SceneCodeBindings<TSceneKey>(this);
        }

        public async UniTask<AsyncRichResult> PrefetchAsync(CancellationToken cancellation)
        {
            LastOperation = await BootstrapAsync(cancellation);
            if (LastOperation.IsSuccessful is not true) return LastOperation;

            if (_stateMachineFrozen is not null)
            {
                LastOperation = LastOperation.Combine(await _stateMachineFrozen.TransitAsync<Prefetch>(cancellation));
                if (LastOperation.IsSuccessful is not true) return LastOperation;

                SceneManager.sceneUnloaded -= CleanSceneRecordState;
                SceneManager.sceneUnloaded += CleanSceneRecordState;

                return LastOperation;
            }
            else
            {
                LastOperation = LastOperation.Combine(await _stateMachineMutable!.TransitAsync<Prefetch>(cancellation));
                if (LastOperation.IsSuccessful is not true) return LastOperation;

                SceneManager.sceneUnloaded -= CleanSceneRecordState;
                SceneManager.sceneUnloaded += CleanSceneRecordState;

                _activated = new Activated.Custom(_sceneInstanceReference, _prefetched.ActivatedCollection);
                _deactivated = new Deactivated.Custom(_sceneInstanceReference, _prefetched.UnloadedCollection);

                _stateMachineFrozen =_stateMachineMutable
                    .AddTransition<Activate>(from: _prefetched, to: _activated)
                    .AddTransition<Deactivate>(from: _activated, to: _deactivated)
                    .AddTransition<Activate>(from: _deactivated, to: _activated)
                    .AddTransition<Unload>(from: _deactivated, to: _unloaded)
                    .AddTransition<Unload>(from: _activated, to: _unloaded)
                    .ToFrozen();

                _stateMachineMutable = null;

                return LastOperation;
            }

            void CleanSceneRecordState(Scene toUnload)
            {
                if (_sceneInstanceReference.TryGetValue(out var loaded) && loaded.Scene == toUnload)
                {
                    _sceneInstanceReference.Value = null;
                }

                SceneManager.sceneUnloaded -= CleanSceneRecordState;
            }
        }

        public async UniTask<AsyncRichResult> LoadAsync(CancellationToken cancellation)
        {
            LastOperation = LastOperation.Combine(await _stateMachineFrozen!.TransitAsync<Activate>(cancellation));

            if (LastOperation.IsSuccessful)
            {
                _loadedEvent.RaiseEvent();
            }
            return LastOperation;
        }


        public async UniTask<AsyncRichResult> UnloadAsync(CancellationToken cancellation)
        {
            LastOperation = LastOperation.Combine(await _stateMachineFrozen!.TransitAsync<Deactivate>(cancellation));

            if (LastOperation.IsSuccessful)
            {
                _unloadedEvent.RaiseEvent();
            }
            return LastOperation;
        }

        public async UniTask<AsyncRichResult> CompletelyUnloadAsync(CancellationToken cancellation)
        {
            return LastOperation = LastOperation.Combine(await _stateMachineFrozen!.TransitAsync<Unload>(cancellation));
        }

        /// <summary>
        /// This section refers to API, designed to use in Unity Editor with UnityEvent
        /// </summary>
        #region Sync API

        public AsyncRichResult LastOperation { get; private set; } = AsyncRichResult.Success;
        public CancellationToken ApplicationLifetime { private get; set; } = CancellationToken.None;

        public void Prefetch()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await PrefetchAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [PREFETCHING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [PREFETCHING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [PREFETCHING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        public void Load()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await LoadAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [LOADING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [LOADING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [LOADING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        public void Unload()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await UnloadAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [UNLOADING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [UNLOADING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [UNLOADING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        public void CompletelyUnload()
        {
            PerformRoutineAsync(ApplicationLifetime)
                .Forget();

            async UniTask PerformRoutineAsync(CancellationToken token = default)
            {
                var result = await CompletelyUnloadAsync(token);
                var sceneName = _sceneInstanceReference.TryGetValue(out var instance)
                    ? instance.Scene.name
                    : "undetermined";

                var exception = result.Match<Exception?>
                (
                    success: static _ => null,
                    cancellation: () => new OperationCanceledException($"Scene: \"{sceneName}\" [COMPLETE UNLOADING] Canceled!"),
                    failure: failure => new InvalidOperationException($"Scene: \"{sceneName}\" [COMPLETE UNLOADING] Not happen: {failure.Message}", failure.AsException()),
                    error: exception => new AggregateException($"Scene: \"{sceneName}\" [COMPLETE UNLOADING] Error occured: ", exception),
                    token
                );

                if (exception is not null) throw exception;
            }
        }

        #endregion

        private async UniTask<AsyncRichResult> BootstrapAsync(CancellationToken cancellation = default)
        {
            CleanUp();

            if (_customFlowNeeded)
            {
                _unloaded = new Unloaded.Custom(_sceneInstanceReference, YieldPoint);
                _prefetched = new Prefetched.Custom(_sceneInstanceReference, Target, YieldPoint, Priority);

                _stateMachineMutable = StateMachine.Mutable.Create<Bootstrap>(startingWith: _unloaded)
                    .AddTransition<Prefetch>(from: _unloaded, to: _prefetched)
                    .AddTransition<Unload>(from: _prefetched, to: _unloaded);

                return await _stateMachineMutable.TransitAsync<Bootstrap>(cancellation);
            }
            else
            {
                var unloaded = new Unloaded.Regular(_sceneInstanceReference, YieldPoint);
                var prefetched = new Prefetched.Regular(_sceneInstanceReference, Target, YieldPoint, Priority);
                var activated = new Activated.Regular(_sceneInstanceReference, YieldPoint);
                var deactivated = new Deactivated.Regular(_sceneInstanceReference, YieldPoint);

                _stateMachineFrozen = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<Bootstrap>(to: unloaded)
                    .WithTransition<Prefetch>(from: unloaded, to: prefetched)
                    .WithTransition<Activate>(from: prefetched, to: activated)
                    .WithTransition<Deactivate>(from: activated, to: deactivated)
                    .WithTransition<Activate>(from: deactivated, to: activated)
                    .WithTransition<Unload>(from: deactivated, to: unloaded)
                    .WithTransition<Unload>(from: activated, to: unloaded)
                    .WithTransition<Unload>(from: prefetched, to: unloaded)
                    .Build()
                    .ToFrozen();

                return await _stateMachineFrozen.TransitAsync<Bootstrap>(cancellation);
            }
        }

        private void CleanUp()
        {
            _sceneInstanceReference.Value = null;
            _stateMachineMutable = null;
            _stateMachineFrozen = null;
            _unloaded = default!;
            _prefetched = default!;
            _activated = default!;
            _deactivated = default!;

            _loadedEvent.Dispose();
            _unloadedEvent.Dispose();
        }

#if UNITY_EDITOR

        private void Configure()
        {
            _customFlowNeeded = AssetReferenceScene.CheckRequiredCustomFlow(Target);
        }

        [UnityEditor.CustomEditor(typeof(SceneRecord))]
        internal sealed class SceneRecordEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var sceneRecord = (SceneRecord) target;

                UnityEditor.EditorGUILayout.LabelField("Custom Flow Needed", sceneRecord._customFlowNeeded.ToString());
                if (GUILayout.Button("Check if custom flow needed") is false) return;

                sceneRecord.Configure();

                UnityEditor.EditorUtility.SetDirty(sceneRecord);
            }
        }

        internal sealed class SceneModificationPostprocessor : UnityEditor.AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                var sceneAssetPaths = importedAssets.Concat(deletedAssets)
                    .Concat(movedAssets)
                    .Concat(movedFromAssetPaths)
                    .Where(assetPath => assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));

                foreach (var assetPath in sceneAssetPaths)
                {
                    RevalidateSceneRecordsForAsset(assetPath);
                }
            }

            private static void RevalidateSceneRecordsForAsset(string assetPath)
            {
                var sceneRecords = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(SceneRecord))
                    .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<SceneRecord>);

                foreach (var record in sceneRecords.Where(record => record.Target.AssetGUID == UnityEditor.AssetDatabase.AssetPathToGUID(assetPath)))
                {
                    record.Configure();

                    UnityEditor.EditorUtility.SetDirty(record);
                }
            }
        }

#endif
    }
}
