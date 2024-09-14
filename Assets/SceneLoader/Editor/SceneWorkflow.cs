#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Scene = UnityEngine.SceneManagement.Scene;

namespace SceneLoader.Editor
{
    using Abstract;

    [InitializeOnLoad]
    public static class SceneWorkflow
    {
        private const string EditModeScenesKey = nameof(SceneWorkflow) + "." + nameof(EditModeScenes);

        private static readonly Func<GameObject, bool> ContainsIgnoreFlag = static candidate => candidate.TryGetComponent(out IIgnoreSceneFlag _);
        private static readonly Func<GameObject,bool> HasNoPreserveFlag = static candidate => candidate.TryGetComponent(out IPreserveGameObjectStateFlag _) is false;

        private static IEnumerable<string> EditModeScenes
        {
            set => EditorPrefs.SetString(EditModeScenesKey, string.Join("|", value));
            get => EditorPrefs.GetString(EditModeScenesKey, string.Empty).Split('|');
        }

        static SceneWorkflow()
        {
            EditorSceneManager.sceneOpened -= EnableRootGameObjects;
            EditorSceneManager.sceneSaved -= EnableRootGameObjects;
            EditorSceneManager.sceneSaving -= DisableRootGameObjects;
            EditorApplication.playModeStateChanged -= SwitchPlayModeScenes;

            EditorApplication.playModeStateChanged += SwitchPlayModeScenes;
            EditorSceneManager.sceneSaving += DisableRootGameObjects;
            EditorSceneManager.sceneSaved += EnableRootGameObjects;
            EditorSceneManager.sceneOpened += EnableRootGameObjects;
        }

        private static void SwitchPlayModeScenes(PlayModeStateChange state)
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

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
                }
                else
                {
                    EditorApplication.isPlaying = false;
                }
            }

            static void RestoreEditModeScenes()
            {
                var scenes = EditModeScenes.ToArray();
                EditorSceneManager.OpenScene(scenes.First(), OpenSceneMode.Single);

                foreach (var scene in scenes.Skip(1))
                {
                    EditorSceneManager.OpenScene(scene, OpenSceneMode.Additive);
                }
            }
        }

        private static void EnableRootGameObjects(Scene scene, OpenSceneMode _) => EnableRootGameObjects(scene);

        private static void EnableRootGameObjects(Scene scene)
        {
            if (BuildPipeline.isBuildingPlayer) return;
            if (scene.buildIndex == 0) return;

            var candidates = scene.GetRootGameObjects();

            if (ReferenceEquals(candidates.SingleOrDefault(ContainsIgnoreFlag), null) is false) return;

            foreach (var candidate in candidates.Where(HasNoPreserveFlag))
            {
                candidate.SetActive(true);
            }
        }

        private static void DisableRootGameObjects(Scene scene, string path)
        {
            if (BuildPipeline.isBuildingPlayer) return;
            if (scene.buildIndex == 0) return;

            var candidates = scene.GetRootGameObjects();

            if (ReferenceEquals(candidates.SingleOrDefault(ContainsIgnoreFlag), null) is false) return;

            foreach (var candidate in candidates.Where(HasNoPreserveFlag))
            {
                candidate.SetActive(false);
            }
        }
    }
}
