#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;

namespace SceneLoader.Core
{
    using Abstract;
    using Abstract.Explicit;

    public sealed class SceneCodeBindings<TSceneKey>
        : ISceneExplicitPrefetcher<TSceneKey>,
          ISceneLoader<TSceneKey>,
          ISceneLoadedEvent<TSceneKey>,
          ISceneUnloader<TSceneKey>,
          ISceneExplicitCompleteUnloader<TSceneKey>,
          ISceneUnloadedEvent<TSceneKey>
        where TSceneKey : class, ISceneKey
    {
        private readonly ISceneExplicitPrefetcher<TSceneKey> _prefetcher;
        private readonly ISceneLoader<TSceneKey> _loader;
        private readonly ISceneLoadedEvent<TSceneKey> _loaded;
        private readonly ISceneUnloader<TSceneKey> _unloader;
        private readonly ISceneExplicitCompleteUnloader<TSceneKey> _completeUnloader;
        private readonly ISceneUnloadedEvent<TSceneKey> _unloaded;

        internal SceneCodeBindings(SceneRecord record)
        {
            _prefetcher = new LoadingPrefetcher(record);
            var sceneLoader = new Loader(record);
            (_loader, _loaded) = (sceneLoader, sceneLoader);
            var sceneUnloader = new Unloader(record);
            (_unloader, _completeUnloader, _unloaded) = (sceneUnloader, sceneUnloader, sceneUnloader);
        }

        UniTask<AsyncRichResult> ISceneExplicitPrefetcher<TSceneKey>.PrefetchAsync(CancellationToken cancellation) => _prefetcher.PrefetchAsync(cancellation);
        UniTask<AsyncRichResult> ISceneLoader<TSceneKey>.LoadAsync(CancellationToken cancellation) => _loader.LoadAsync(cancellation);
        IDisposable ISceneLoadedEvent<TSceneKey>.Subscribe(Action whenLoaded) => _loaded.Subscribe(whenLoaded);
        UniTask<AsyncRichResult> ISceneUnloader<TSceneKey>.UnloadAsync(CancellationToken cancellation) => _unloader.UnloadAsync(cancellation);
        UniTask<AsyncRichResult> ISceneExplicitCompleteUnloader<TSceneKey>.CompletelyUnloadAsync(CancellationToken cancellation) => _completeUnloader.CompletelyUnloadAsync(cancellation);
        IDisposable ISceneUnloadedEvent<TSceneKey>.Subscribe(Action whenUnloaded) => _unloaded.Subscribe(whenUnloaded);

        private sealed class LoadingPrefetcher : ISceneExplicitPrefetcher<TSceneKey>
        {
            private readonly SceneRecord _sceneRecord;

            public LoadingPrefetcher(SceneRecord sceneRecord)
            {
                _sceneRecord = sceneRecord;
            }

            UniTask<AsyncRichResult> ISceneExplicitPrefetcher<TSceneKey>.PrefetchAsync(CancellationToken cancellation) => _sceneRecord.PrefetchAsync(cancellation);
        }

        private sealed class Loader : ISceneLoader<TSceneKey>, ISceneLoadedEvent<TSceneKey>
        {
            private readonly SceneRecord _record;

            public Loader(SceneRecord record)
            {
                _record = record;
            }

            UniTask<AsyncRichResult> ISceneLoader<TSceneKey>.LoadAsync(CancellationToken cancellation) => _record.LoadAsync(cancellation);
            IDisposable ISceneLoadedEvent<TSceneKey>.Subscribe(Action whenLoaded) => _record.LoadedSubscribe(whenLoaded.Invoke);
        }

        private sealed class Unloader : ISceneUnloader<TSceneKey>, ISceneExplicitCompleteUnloader<TSceneKey>, ISceneUnloadedEvent<TSceneKey>
        {
            private readonly SceneRecord _sceneRecord;

            public Unloader(SceneRecord sceneRecord)
            {
                _sceneRecord = sceneRecord;
            }

            UniTask<AsyncRichResult> ISceneUnloader<TSceneKey>.UnloadAsync(CancellationToken cancellation) => _sceneRecord.UnloadAsync(cancellation);
            UniTask<AsyncRichResult> ISceneExplicitCompleteUnloader<TSceneKey>.CompletelyUnloadAsync(CancellationToken cancellation) => _sceneRecord.CompletelyUnloadAsync(cancellation);
            IDisposable ISceneUnloadedEvent<TSceneKey>.Subscribe(Action whenUnloaded) => _sceneRecord.UnloadedSubscribe(whenUnloaded.Invoke);
        }
    }
}
