#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SceneLoader.Editor
{
    [SuppressMessage("ReSharper", "AccessToStaticMemberViaDerivedType")]
    internal static class EditorSceneManagerUtility
    {
        public static IEnumerable<Scene> GetAllScenes()
        {
            for (var index = 0; index < EditorSceneManager.sceneCount; index++)
            {
                yield return EditorSceneManager.GetSceneAt(index);
            }
        }
    }
}
