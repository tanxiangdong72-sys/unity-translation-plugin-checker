using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor
{
    // 只解析并选择报告中的资源，不修改或保存 Scene、Prefab 和资产内容。
    internal static class AuditIssueLocator
    {
        public static AuditLocationResult Locate(AuditIssueLocation location)
        {
            try
            {
                var validation = ValidateAssetPath(location?.AssetPath);
                if (!validation.Succeeded)
                {
                    return validation;
                }

                var objectPath = location.ObjectPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(objectPath))
                {
                    return AuditLocationResult.Failure(
                        AuditLocationResultStatus.InvalidLocation,
                        "ObjectPath is required.",
                        validation.AssetPath);
                }

                AuditLocationResult result;
                if (Path.GetExtension(validation.AssetPath).Equals(
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    result = LocatePrefab(validation.AssetPath, location);
                }
                else
                {
                    result = LocateLoadedScene(validation.AssetPath, location);
                }

                if (result.Succeeded && result.Target != null)
                {
                    Selection.activeObject = result.Target;
                    EditorGUIUtility.PingObject(result.Target);
                }

                return result;
            }
            catch (Exception exception)
            {
                return AuditLocationResult.Failure(
                    AuditLocationResultStatus.ResolutionFailed,
                    "Could not locate issue: " + exception.Message);
            }
        }

        public static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
        }

        public static bool CanLocate(AuditIssueLocation location)
        {
            return location != null &&
                   !string.IsNullOrWhiteSpace(location.ObjectPath) &&
                   ValidateAssetPath(location.AssetPath).Succeeded;
        }

        public static AuditLocationResult ValidateAssetPath(string assetPath)
        {
            var normalizedPath = NormalizeAssetPath(assetPath);
            if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                (Path.GetExtension(normalizedPath).Equals(
                     ".prefab",
                     StringComparison.OrdinalIgnoreCase) == false &&
                 Path.GetExtension(normalizedPath).Equals(
                     ".unity",
                     StringComparison.OrdinalIgnoreCase) == false))
            {
                return AuditLocationResult.Failure(
                    AuditLocationResultStatus.InvalidLocation,
                    "AssetPath must be an existing .prefab or .unity asset below Assets.",
                    normalizedPath);
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(normalizedPath)))
            {
                return AuditLocationResult.Failure(
                    AuditLocationResultStatus.AssetNotFound,
                    "Asset does not exist: " + normalizedPath,
                    normalizedPath);
            }

            return AuditLocationResult.Success(
                null,
                normalizedPath,
                string.Empty);
        }

        private static AuditLocationResult LocatePrefab(
            string assetPath,
            AuditIssueLocation location)
        {
            var originalStage = PrefabStageUtility.GetCurrentPrefabStage();
            var originalStageAssetPath = originalStage == null
                ? string.Empty
                : originalStage.assetPath;
            var isAlreadyInTargetStage = string.Equals(
                originalStageAssetPath,
                assetPath,
                StringComparison.Ordinal);
            var succeeded = false;

            try
            {
                var stage = isAlreadyInTargetStage
                    ? originalStage
                    : PrefabStageUtility.OpenPrefab(assetPath);
                if (stage == null || stage.prefabContentsRoot == null)
                {
                    return AuditLocationResult.Failure(
                        AuditLocationResultStatus.ResolutionFailed,
                        "Prefab could not be opened for locating: " + assetPath,
                        assetPath,
                        location.ObjectPath);
                }

                var gameObject = FindPrefabObject(
                    stage.prefabContentsRoot,
                    location.ObjectPath);
                var result = ResolveTarget(gameObject, assetPath, location);
                succeeded = result.Succeeded;
                return result;
            }
            finally
            {
                if (!succeeded &&
                    !isAlreadyInTargetStage &&
                    IsCurrentTargetPrefabStage(assetPath, originalStageAssetPath))
                {
                    StageUtility.GoBackToPreviousStage();
                    if (!string.IsNullOrEmpty(originalStageAssetPath))
                    {
                        PrefabStageUtility.OpenPrefab(originalStageAssetPath);
                    }
                }
            }
        }

        private static bool IsCurrentTargetPrefabStage(
            string targetAssetPath,
            string originalStageAssetPath)
        {
            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            return currentStage != null &&
                   string.Equals(
                       currentStage.assetPath,
                       targetAssetPath,
                       StringComparison.Ordinal) &&
                   !string.Equals(
                       currentStage.assetPath,
                       originalStageAssetPath,
                       StringComparison.Ordinal);
        }

        private static AuditLocationResult LocateLoadedScene(
            string assetPath,
            AuditIssueLocation location)
        {
            var scene = SceneManager.GetSceneByPath(assetPath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return AuditLocationResult.Failure(
                    AuditLocationResultStatus.SceneNotLoaded,
                    "Scene is not loaded: " + assetPath,
                    assetPath,
                    location.ObjectPath);
            }

            var gameObject = FindSceneObject(scene, location.ObjectPath);
            return ResolveTarget(gameObject, assetPath, location);
        }

        private static GameObject FindPrefabObject(GameObject root, string objectPath)
        {
            if (string.Equals(root.name, objectPath, StringComparison.Ordinal))
            {
                return root;
            }

            var rootPrefix = root.name + "/";
            if (!objectPath.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var transform = root.transform.Find(objectPath.Substring(rootPrefix.Length));
            return transform == null ? null : transform.gameObject;
        }

        private static GameObject FindSceneObject(Scene scene, string objectPath)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindPrefabObject(root, objectPath);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static AuditLocationResult ResolveTarget(
            GameObject gameObject,
            string assetPath,
            AuditIssueLocation location)
        {
            if (gameObject == null)
            {
                return AuditLocationResult.Failure(
                    AuditLocationResultStatus.ObjectNotFound,
                    "Object was not found: " + location.ObjectPath,
                    assetPath,
                    location.ObjectPath);
            }

            var component = FindComponent(gameObject, location.ComponentType);
            if (!string.IsNullOrEmpty(location.ComponentType) && component == null)
            {
                return AuditLocationResult.Failure(
                    AuditLocationResultStatus.ComponentNotFound,
                    "Component was not found: " + location.ComponentType,
                    assetPath,
                    location.ObjectPath);
            }

            if (!string.IsNullOrEmpty(location.PropertyPath))
            {
                if (component == null)
                {
                    return AuditLocationResult.Failure(
                        AuditLocationResultStatus.ComponentNotFound,
                        "PropertyPath requires a component.",
                        assetPath,
                        location.ObjectPath);
                }

                try
                {
                    using (var serializedObject = new SerializedObject(component))
                    {
                        if (serializedObject.FindProperty(location.PropertyPath) == null)
                        {
                            return AuditLocationResult.Failure(
                                AuditLocationResultStatus.PropertyNotFound,
                                "Property was not found: " + location.PropertyPath,
                                assetPath,
                                location.ObjectPath);
                        }
                    }
                }
                catch (Exception exception)
                {
                    return AuditLocationResult.Failure(
                        AuditLocationResultStatus.PropertyNotFound,
                        "Property could not be resolved: " + exception.Message,
                        assetPath,
                        location.ObjectPath);
                }
            }

            return AuditLocationResult.Success(
                component == null ? gameObject : component,
                assetPath,
                location.ObjectPath);
        }

        private static Component FindComponent(
            GameObject gameObject,
            string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
            {
                return null;
            }

            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                if (string.Equals(type.FullName, componentType, StringComparison.Ordinal) ||
                    string.Equals(type.Name, componentType, StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }
    }
}
