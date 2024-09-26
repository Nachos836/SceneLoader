#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

using static UnityEditor.EditorApplication;
using static UnityEditor.SceneManagement.EditorSceneManager;

using Scene = UnityEngine.SceneManagement.Scene;

namespace SceneLoader.Editor
{
    using Abstract;

    [InitializeOnLoad]
    public static class SceneWorkflow
    {
        private const string EditModeScenesKey = nameof(SceneWorkflow) + "." + nameof(EditModeScenes);

        private static IEnumerable<string> EditModeScenes
        {
            set => EditorPrefs.SetString(EditModeScenesKey, string.Join("|", value));
            get => EditorPrefs.GetString(EditModeScenesKey, string.Empty).Split('|');
        }

        static SceneWorkflow()
        {
            sceneOpened -= BeforeSceneEdited;
            sceneSaved -= BeforeSceneEdited;
            sceneSaving -= AfterSceneEdited;
            playModeStateChanged -= EnterAndExitEditorSwitching;

            playModeStateChanged += EnterAndExitEditorSwitching;
            sceneSaving += AfterSceneEdited;
            sceneSaved += BeforeSceneEdited;
            sceneOpened += BeforeSceneEdited;
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
                    EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
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

        private static void BeforeSceneEdited(Scene scene, OpenSceneMode _) => BeforeSceneEdited(scene);

        private static void BeforeSceneEdited(Scene scene)
        {
            if (BuildPipeline.isBuildingPlayer) return;
            if (scene.buildIndex == 0) return;

            var candidates = scene.GetRootGameObjects()
                .Select(static root => root.TryGetComponent<IBeforeSceneEdited>(out var candidate)
                    ? candidate
                    : SceneEdited.NoneComponent);

            foreach (var candidate in candidates)
            {
                candidate.Execute();
            }
        }

        private static void AfterSceneEdited(Scene scene, string _)
        {
            if (BuildPipeline.isBuildingPlayer) return;
            if (scene.buildIndex == 0) return;

            var candidates = scene.GetRootGameObjects()
                .Select(static root => root.TryGetComponent<IAfterSceneEdited>(out var candidate)
                    ? candidate
                    : SceneEdited.NoneComponent);

            foreach (var candidate in candidates)
            {
                candidate.Execute();
            }
        }
    }

    internal static class SceneEdited
    {
        public static None NoneComponent { get; } = new ();

        public sealed class None : IAfterSceneEdited, IBeforeSceneEdited
        {
            public void Execute() { }
        }
    }
}
