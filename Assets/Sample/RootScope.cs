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

            builder.Register(_ => _firstScene.CreateCodeBindings<FirstScene>(), Lifetime.Singleton)
                .AsImplementedInterfaces();
            builder.Register(_ => _secondScene.CreateCodeBindings<SecondScene>(), Lifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}
