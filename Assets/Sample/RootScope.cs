using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using SceneLoader.Abstract;
using SceneLoader.Abstract.Explicit;
using UnityEngine;
using VContainer;
using VContainer.Unity;

using SceneLoader.Core;

namespace SceneLoader.Sample
{
    internal sealed class RootScope : LifetimeScope
    {
        [SerializeField] private SceneRecord _firstScene = default!;
        [SerializeField] private SceneRecord _secondScene = default!;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterEntryPoint<SceneLoadingEntry>();

            builder.Register(_ => VContainerSceneHandler<FirstScene>.Build(_firstScene, destroyCancellationToken), Lifetime.Singleton)
                .AsImplementedInterfaces();
            builder.Register(_ => VContainerSceneHandler<SecondScene>.Build(_secondScene, destroyCancellationToken), Lifetime.Singleton)
                .AsImplementedInterfaces();
        }

        private sealed class VContainerSceneHandler<TSceneKey>
            :   IInitializable,
                ISceneLoader<TSceneKey>,
                ISceneLoadedEvent<TSceneKey>,
                ISceneUnloader<TSceneKey>,
                ISceneUnloadedEvent<TSceneKey>,
                IDisposable
            where TSceneKey : class, ISceneKey
        {
            private readonly SceneRecord.CodeBindings<TSceneKey> _bindings;
            private readonly CancellationToken _lifetime;

            private VContainerSceneHandler(SceneRecord.CodeBindings<TSceneKey> bindings, CancellationToken lifetime)
            {
                _bindings = bindings;
                _lifetime = lifetime;
            }

            public static VContainerSceneHandler<TSceneKey> Build(SceneRecord sceneRecord, CancellationToken lifetime = default)
            {
                var bindings = sceneRecord.CreateCodeBindings<TSceneKey>();

                return new VContainerSceneHandler<TSceneKey>(bindings, lifetime);
            }

            void IInitializable.Initialize()
            {
                ((ISceneExplicitPrefetcher<TSceneKey>) _bindings)
                    .PrefetchAsync(_lifetime)
                    .Forget();
            }

            [DebuggerHidden]
            UniTask<AsyncRichResult> ISceneLoader<TSceneKey>.LoadAsync(CancellationToken cancellation)
            {
                return ((ISceneLoader<TSceneKey>) _bindings).LoadAsync(cancellation);
            }

            [DebuggerHidden]
            IDisposable ISceneLoadedEvent<TSceneKey>.Subscribe(Action whenLoaded)
            {
                return ((ISceneLoadedEvent<TSceneKey>) _bindings).Subscribe(whenLoaded);
            }

            [DebuggerHidden]
            UniTask<AsyncRichResult> ISceneUnloader<TSceneKey>.UnloadAsync(CancellationToken cancellation)
            {
                return ((ISceneUnloader<TSceneKey>) _bindings).UnloadAsync(cancellation);
            }

            [DebuggerHidden]
            IDisposable ISceneUnloadedEvent<TSceneKey>.Subscribe(Action whenUnloaded)
            {
                return ((ISceneUnloadedEvent<TSceneKey>) _bindings).Subscribe(whenUnloaded);
            }

            void IDisposable.Dispose()
            {
                ((ISceneExplicitCompleteUnloader<TSceneKey>) _bindings)
                    .CompletelyUnloadAsync(CancellationToken.None)
                    .Forget();
            }
        }
    }
}
