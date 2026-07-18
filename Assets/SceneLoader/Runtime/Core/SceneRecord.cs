#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

namespace SceneLoader.Core
{
    using Abstract;

    [PublicAPI]
    [CreateAssetMenu(menuName = "Scene/Scene Record")]
    public sealed partial class SceneRecord : ScriptableObject
    {
        [SerializeField] [HideInInspector] internal bool _customFlowNeeded;
        [SerializeField, Range(0, 100)] private ushort _priority = 100;
        [SerializeField] private AssetReferenceScene _target = default!;
        [SerializeField] private PlayerLoopTiming _yieldPoint = PlayerLoopTiming.Initialization;

        [MustUseReturnValue]
        public CodeBindings<TSceneKey> CreateCodeBindings<TSceneKey>() where TSceneKey : class, ISceneKey
        {
            return _customFlowNeeded
                ? CodeBindings<TSceneKey>.BuildCustom(_target, _priority, _yieldPoint)
                : CodeBindings<TSceneKey>.BuildTrivial(_target, _priority, _yieldPoint);
        }
    }
}
