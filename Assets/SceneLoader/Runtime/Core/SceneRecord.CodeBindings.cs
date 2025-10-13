#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Generic.Core;
using Generic.Core.FinalStateMachine;
using JetBrains.Annotations;
using SceneLoader.Abstract;
using SceneLoader.Abstract.Explicit;
using SceneLoader.Core.SceneStates;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoader.Core
{
    partial class SceneRecord
    {
        public abstract class CodeBindings<TSceneKey>:
            ISceneExplicitPrefetcher<TSceneKey>,
            ISceneExplicitCompleteUnloader<TSceneKey>,
            ISceneLoader<TSceneKey>,
            ISceneLoadedEvent<TSceneKey>,
            ISceneUnloader<TSceneKey>,
            ISceneUnloadedEvent<TSceneKey>
                where TSceneKey : class, ISceneKey
        {
            [UsedImplicitly] protected sealed record Bootstrap;
            [UsedImplicitly] protected sealed record Prefetch;
            [UsedImplicitly] protected sealed record Activate;
            [UsedImplicitly] protected sealed record Deactivate;
            [UsedImplicitly] protected sealed record Unload;

            protected abstract StateMachine StateMachine { get; }
            private ValueReference<SceneInstance> SceneInstanceReference { get; } = new ();

            private event Action? WhenLoaded;
            private event Action? WhenUnloaded;

            internal static CodeBindings<TSceneKey> BuildTrivial(AssetReferenceScene target, ushort priority, PlayerLoopTiming yieldPoint)
            {
                return new Trivial(target, priority, yieldPoint);
            }

            internal static CodeBindings<TSceneKey> BuildCustom(AssetReferenceScene target, ushort priority, PlayerLoopTiming yieldPoint)
            {
                return new Custom(target, priority, yieldPoint);
            }

            private readonly ConcurrentQueue<AsyncLazy<AsyncRichResult>> _jobs = new ();

            async UniTask<AsyncRichResult> ISceneExplicitPrefetcher<TSceneKey>.PrefetchAsync(CancellationToken cancellation)
            {
                while (_jobs.TryDequeue(out var job))
                {
                    var result = await job;
                    if (result.IsSuccessful is not true) return result;
                }

                var pending = StateMachine.TransitAsync<Bootstrap>(cancellation)
                    .ContinueWith(result => result.RunAsync(StateMachine.TransitAsync<Prefetch>, cancellation))
                    .ToAsyncLazy();

                _jobs.Enqueue(pending);

                return await pending;
            }

            async UniTask<AsyncRichResult> ISceneExplicitCompleteUnloader<TSceneKey>.CompletelyUnloadAsync(CancellationToken cancellation)
            {
                while (_jobs.TryDequeue(out var job))
                {
                    var result = await job;
                    if (result.IsSuccessful is not true) return result;
                }

                var pending = StateMachine.TransitAsync<Unload>(cancellation)
                    .ContinueWith(result => result.Run(Cleanup))
                    .ToAsyncLazy();

                _jobs.Enqueue(pending);

                return await pending;
            }

            async UniTask<AsyncRichResult> ISceneLoader<TSceneKey>.LoadAsync(CancellationToken cancellation)
            {
                while (_jobs.TryDequeue(out var job))
                {
                    var result = await job;
                    if (result.IsSuccessful is not true) return result;
                }

                AsyncLazy<AsyncRichResult> pending;

                if (WhenLoaded is null)
                {
                    pending = StateMachine.TransitAsync<Activate>(cancellation)
                        .ToAsyncLazy();
                }
                else
                {
                    pending = StateMachine.TransitAsync<Activate>(cancellation)
                        .ContinueWith(result => result.Run(() => WhenLoaded?.Invoke()))
                        .ToAsyncLazy();
                }

                _jobs.Enqueue(pending);

                return await pending;
            }

            async UniTask<AsyncRichResult> ISceneUnloader<TSceneKey>.UnloadAsync(CancellationToken cancellation)
            {
                while (_jobs.TryDequeue(out var job))
                {
                    var result = await job;

                    if (result.IsCancellation)
                    {
                        _jobs.Clear();
                        break;
                    }
                    if (result.IsSuccessful is not true) return result;
                }

                AsyncLazy<AsyncRichResult> pending;

                if (WhenUnloaded is null)
                {
                    pending = StateMachine.TransitAsync<Deactivate>(cancellation)
                        .ToAsyncLazy();
                }
                else
                {
                    pending = StateMachine.TransitAsync<Deactivate>(cancellation)
                        .ContinueWith(result => result.Run(() => WhenUnloaded?.Invoke()))
                        .ToAsyncLazy();
                }

                _jobs.Enqueue(pending);

                return await pending;
            }

            [MustDisposeResource] [MustUseReturnValue]
            IDisposable ISceneUnloadedEvent<TSceneKey>.Subscribe(Action whenUnloaded)
            {
                WhenUnloaded += whenUnloaded;

                return Disposable.Create(() => WhenUnloaded -= whenUnloaded);
            }

            [MustDisposeResource] [MustUseReturnValue]
            IDisposable ISceneLoadedEvent<TSceneKey>.Subscribe(Action whenLoaded)
            {
                WhenLoaded += whenLoaded;

                return Disposable.Create(() => WhenLoaded -= whenLoaded);
            }

            protected virtual void Cleanup()
            {
                SceneInstanceReference.Value = null;
            }

            private sealed class Trivial: CodeBindings<TSceneKey>
            {
                public Trivial(AssetReferenceScene target, ushort priority, PlayerLoopTiming yieldPoint)
                {
                    var prefetched = new Prefetched.Regular(SceneInstanceReference, target, yieldPoint, priority);
                    var activated = new Activated.Regular(SceneInstanceReference, yieldPoint);
                    var deactivated = new Deactivated.Regular(SceneInstanceReference, yieldPoint);
                    var unloaded = new Unloaded(SceneInstanceReference, yieldPoint);

                    StateMachine = StateMachine.Immutable.CreateBuilder()
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
                }

                protected override StateMachine StateMachine { get; }
            }

            private sealed class Custom : CodeBindings<TSceneKey>
            {
                private readonly IDisposable _disposable;

                public Custom(AssetReferenceScene target, ushort priority, PlayerLoopTiming yieldPoint)
                {
                    var prefetchedState = new Prefetched.Custom(SceneInstanceReference, target, yieldPoint, priority);
                    var activatedState = new Activated.Custom(SceneInstanceReference, prefetchedState);
                    var deactivatedState = new Deactivated.Custom(SceneInstanceReference, prefetchedState);
                    var unloadedState = new Unloaded(SceneInstanceReference, yieldPoint);

                    StateMachine = StateMachine.Immutable.CreateBuilder()
                        .WithInitialTransition<Bootstrap>(to: unloadedState)
                        .WithTransition<Prefetch>(from: unloadedState, to: prefetchedState)
                        .WithTransition<Activate>(from: prefetchedState, to: activatedState)
                        .WithTransition<Activate>(from: deactivatedState, to: activatedState)
                        .WithTransition<Deactivate>(from: activatedState, to: deactivatedState)
                        .WithTransition<Unload>(from: prefetchedState, to: unloadedState)
                        .WithTransition<Unload>(from: deactivatedState, to: unloadedState)
                        .WithTransition<Unload>(from: activatedState, to: unloadedState)
                        .Build()
                        .ToFrozen();

                    _disposable = prefetchedState;
                }

                protected override StateMachine StateMachine { get; }

                protected override void Cleanup()
                {
                    base.Cleanup();

                    _disposable.Dispose();
                }
            }
        }
    }
}
