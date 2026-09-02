using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor
{
    internal sealed class SceneReadOnlyScope : IDisposable
    {
        private readonly SceneSetup[] _originalSetup;
        private readonly bool _ownsOpenedScene;
        private Scene _scene;
        private bool _disposed;

        private SceneReadOnlyScope(
            string assetPath,
            SceneSetup[] originalSetup,
            Scene scene,
            bool ownsOpenedScene)
        {
            AssetPath = assetPath;
            _originalSetup = originalSetup ?? Array.Empty<SceneSetup>();
            _scene = scene;
            _ownsOpenedScene = ownsOpenedScene;
        }

        public string AssetPath { get; }
        public Scene Scene => _scene;

        public static SceneReadOnlyScope Open(string assetPath)
        {
            var normalizedPath = NormalizeAssetPath(assetPath);
            ValidateScenePath(normalizedPath);
            EnsureNoDirtyLoadedScenes();

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var existingScene = SceneManager.GetSceneByPath(normalizedPath);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                return new SceneReadOnlyScope(
                    normalizedPath,
                    originalSetup,
                    existingScene,
                    ownsOpenedScene: false);
            }

            try
            {
                var openedScene = EditorSceneManager.OpenScene(
                    normalizedPath,
                    OpenSceneMode.Additive);
                if (!openedScene.IsValid() || !openedScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        "Unity could not load Scene at '" + normalizedPath + "'.");
                }

                return new SceneReadOnlyScope(
                    normalizedPath,
                    originalSetup,
                    openedScene,
                    ownsOpenedScene: true);
            }
            catch
            {
                RestoreSetup(originalSetup);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Exception cleanupError = null;
            try
            {
                if (_ownsOpenedScene &&
                    _scene.IsValid() &&
                    _scene.isLoaded)
                {
                    // 强制关闭临时 Scene，绝不保存扫描期间产生的脏状态。
                    if (!EditorSceneManager.CloseScene(_scene, true))
                    {
                        throw new InvalidOperationException(
                            "Unity could not close Scene at '" + AssetPath + "'.");
                    }
                }
            }
            catch (Exception exception)
            {
                cleanupError = exception;
            }
            finally
            {
                try
                {
                    RestoreSetup(_originalSetup);
                }
                catch (Exception restoreException)
                {
                    if (cleanupError == null)
                    {
                        cleanupError = restoreException;
                    }
                }

                _disposed = true;
                _scene = default(Scene);
            }

            if (cleanupError != null)
            {
                throw cleanupError;
            }
        }

        private static void EnsureNoDirtyLoadedScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Scene scanning is blocked while an open Scene has unsaved changes.");
                }
            }
        }

        private static void RestoreSetup(SceneSetup[] setup)
        {
            if (setup == null || setup.Length == 0)
            {
                return;
            }

            var hasSavedScene = false;
            for (var index = 0; index < setup.Length; index++)
            {
                if (!string.IsNullOrEmpty(setup[index].path))
                {
                    hasSavedScene = true;
                    break;
                }
            }

            if (!hasSavedScene)
            {
                return;
            }

            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        private static void ValidateScenePath(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(
                    Path.GetExtension(assetPath),
                    ".unity",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Scene path must be an existing .unity asset below Assets.",
                    nameof(assetPath));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) == null)
            {
                throw new ArgumentException(
                    "Scene path does not exist: " + assetPath,
                    nameof(assetPath));
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
        }
    }
}
