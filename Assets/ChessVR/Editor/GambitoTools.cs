using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessVR.Editor
{
    public static class GambitoTools
    {
        private const string ScenePath = "Assets/ChessVR/Scenes/Gambito.unity";

        [InitializeOnLoadMethod]
        private static void PrepareEditor()
        {
            if (Application.isBatchMode)
                return;

            EditorApplication.delayCall += OpenDefaultScene;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += OpenDefaultScene;
        }

        private static void OpenDefaultScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && string.IsNullOrEmpty(scene.path) && !scene.isDirty &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Gambito/Abrir sala")]
        public static void OpenRoom()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Gambito/Gerar build para Windows x64")]
        public static void BuildWindows()
        {
            const string location = "Builds/Windows/GambitoVR.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(location));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = location,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build encerrada com {report.summary.result}.");

            Debug.Log($"Gambito VR gerado em {Path.GetFullPath(location)}.");
        }
    }
}
