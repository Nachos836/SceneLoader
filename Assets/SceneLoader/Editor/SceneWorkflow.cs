#nullable enable

using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;

using static UnityEditor.EnterPlayModeOptions;
using static UnityEditor.SceneManagement.EditorSceneManager;

using Debug = UnityEngine.Debug;
using Scene = UnityEngine.SceneManagement.Scene;

namespace SceneLoader.Editor
{
    using Abstract.Internal;

    [InitializeOnLoad]
    internal static class SceneWorkflow
    {
        private static readonly SceneOpenedCallback BeforeSceneEditedCallback = static (scene, _) => BeforeSceneEdited(scene);
        private static readonly SceneSavedCallback BeforeSceneSavedCallback = BeforeSceneEdited;
        private static readonly SceneSavingCallback AfterSceneEditedCallback = AfterSceneEdited;

        private static bool DomainReloadEnabled
        {
            get
            {
                if (EditorSettings.enterPlayModeOptionsEnabled is false) return false;

                return (EditorSettings.enterPlayModeOptions & DisableDomainReload) == 0;
            }
        }

        static SceneWorkflow()
        {
            if (DomainReloadEnabled)
            {
                sceneOpened -= BeforeSceneEditedCallback;
                sceneSaved -= BeforeSceneSavedCallback;
                sceneSaving -= AfterSceneEditedCallback;
            }

            sceneSaving += AfterSceneEditedCallback;
            sceneSaved += BeforeSceneSavedCallback;
            sceneOpened += BeforeSceneEditedCallback;
        }

        private static void BeforeSceneEdited(Scene scene)
        {
            if (BuildPipeline.isBuildingPlayer) return;
            if (scene.buildIndex != -1) return; // scene should not be in a build list

            var roots = scene.GetRootGameObjects();
            var customRootsCount = 0;
            var customRoots = roots
                .Where(static root => root.TryGetComponent<IBeforeSceneEditedCustom>(out _))
                .Select(static root => root.GetComponent<IBeforeSceneEditedCustom>());

            foreach (var candidate in customRoots)
            {
                candidate.ExecuteInEditor().Match
                (
                    success: static () => {},
                    error: static exception => Debug.LogError(exception)
                );

                ++customRootsCount;
            }
            if (customRootsCount == roots.Length) return;

            var trivialRoots = roots
                .Where(static root => root.TryGetComponent<IBeforeSceneEdited>(out _))
                .Select(static root => root.GetComponent<IBeforeSceneEdited>().IdForEditor)
                .ToImmutableArray()
                .AsSpan();
            if (trivialRoots.IsEmpty) return;

            GameObject.SetGameObjectsActive(trivialRoots, active: true);
        }

        private static void AfterSceneEdited(Scene scene, string __)
        {
            if (BuildPipeline.isBuildingPlayer) return;
            if (scene.buildIndex != -1) return; // scene should not be in a build list

            var roots = scene.GetRootGameObjects();
            var customRootsCount = 0;
            var customRoots = roots
                .Where(static root => root.TryGetComponent<IAfterSceneEditedCustom>(out _))
                .Select(static root => root.GetComponent<IAfterSceneEditedCustom>());

            foreach (var candidate in customRoots)
            {
                candidate.ExecuteInEditor().Match
                (
                    success: static () => {},
                    error: static exception => Debug.LogError(exception)
                );

                ++customRootsCount;
            }
            if (customRootsCount == roots.Length) return;

            var trivialRoots = roots
                .Where(static root => root.TryGetComponent<IAfterSceneEdited>(out _))
                .Select(static root => root.GetComponent<IAfterSceneEdited>().IdForEditor)
                .ToImmutableArray()
                .AsSpan();
            if (trivialRoots.IsEmpty) return;

            GameObject.SetGameObjectsActive(trivialRoots, active: false);
        }
    }
}
