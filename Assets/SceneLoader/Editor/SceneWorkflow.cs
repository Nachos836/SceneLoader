#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using static UnityEditor.EditorApplication;
using static UnityEditor.SceneManagement.EditorSceneManager;

using Scene = UnityEngine.SceneManagement.Scene;

namespace SceneLoader.Editor
{
    using Abstract.Internal;

    [InitializeOnLoad]
    public static class SceneWorkflow
    {
        private const string EditModeScenesKey = nameof(SceneWorkflow) + "." + nameof(EditModeScenes);

        private static readonly SceneOpenedCallback BeforeSceneEditedCallback = (scene, _) => BeforeSceneEdited(scene);
        private static readonly SceneSavedCallback BeforeSceneSavedCallback = BeforeSceneEdited;
        private static readonly SceneSavingCallback AfterSceneEditedCallback = AfterSceneEdited;
        private static readonly Action<PlayModeStateChange> EnterAndExitEditorSwitchingCallback = EnterAndExitEditorSwitching;

        private static IEnumerable<string> EditModeScenes
        {
            set => EditorPrefs.SetString(EditModeScenesKey, string.Join("|", value));
            get => EditorPrefs.GetString(EditModeScenesKey, string.Empty).Split('|');
        }

        static SceneWorkflow()
        {
            sceneOpened -= BeforeSceneEditedCallback;
            sceneSaved -= BeforeSceneSavedCallback;
            sceneSaving -= AfterSceneEditedCallback;
            playModeStateChanged -= EnterAndExitEditorSwitchingCallback;

            playModeStateChanged += EnterAndExitEditorSwitchingCallback;
            sceneSaving += AfterSceneEditedCallback;
            sceneSaved += BeforeSceneSavedCallback;
            sceneOpened += BeforeSceneEditedCallback;
        }

        private static void EnterAndExitEditorSwitching(PlayModeStateChange state)
        {
            var scenes = EditorSceneManagerUtility.GetAllScenes()
                .Where(static scene => scene.isLoaded)
                .ToArray();

            if (scenes.Length <= 1 && scenes.First().buildIndex < 0) return;

            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                {
                    SaveEditModeScenes(scenes);

                    return;
                }
                case PlayModeStateChange.EnteredEditMode:
                {
                    RestoreEditModeScenes();

                    return;
                }
                case PlayModeStateChange.EnteredPlayMode: return;
                case PlayModeStateChange.ExitingPlayMode: return;
                default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            static void SaveEditModeScenes(Scene[] scenes)
            {
                EditModeScenes = scenes.Select(static scene => scene.path);

                if (SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First().path);
                }
                else
                {
                    isPlaying = false;
                }
            }

            static void RestoreEditModeScenes()
            {
                var scenes = EditModeScenes.ToArray();
                OpenScene(scenes.First(), OpenSceneMode.Single);

                foreach (var scene in scenes.Skip(1))
                {
                    OpenScene(scene, OpenSceneMode.Additive);
                }
            }
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
                .ToImmutableArray();
            if (trivialRoots.IsDefaultOrEmpty) return;

            GameObject.SetGameObjectsActive(trivialRoots.AsSpan(), active: true);
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
                .ToImmutableArray();
            if (trivialRoots.Length == 0) return;

            GameObject.SetGameObjectsActive(trivialRoots.AsSpan(), active: false);
        }
    }
}
