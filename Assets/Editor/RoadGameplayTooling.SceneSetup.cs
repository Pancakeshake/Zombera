#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class RoadGameplayTooling
    {
        private static GameObject EnsureRootObject(Scene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                var createdOutsideScene = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(createdOutsideScene, "Create " + objectName);
                return createdOutsideScene;
            }

            var existing = FindGameObjectInSceneByName(scene, objectName);
            if (existing != null) return existing;

            var created = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(created, "Create " + objectName);
            if (created.scene != scene) SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static GameObject EnsureChildObject(Transform parent, string objectName, Scene scene)
        {
            if (parent == null) return EnsureRootObject(scene, objectName);

            var child = parent.Find(objectName);
            if (child != null) return child.gameObject;

            var created = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(created, "Create " + objectName);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject == null) return null;

            var existing = gameObject.GetComponent<T>();
            if (existing != null) return existing;

            return Undo.AddComponent<T>(gameObject);
        }

        private static GameObject FindGameObjectInSceneByName(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(name)) return null;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null) continue;

                var match = FindByNameRecursive(root.transform, name);
                if (match != null) return match.gameObject;
            }

            return null;
        }

        private static Transform FindByNameRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (string.Equals(parent.name, name, StringComparison.Ordinal)) return parent;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var match = FindByNameRecursive(child, name);
                if (match != null) return match;
            }

            return null;
        }

        private static T FindFirstInScene<T>(Scene scene) where T : Component
        {
            var objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < objects.Length; i++)
            {
                var candidate = objects[i];
                if (candidate == null || candidate.gameObject.scene != scene) continue;
                return candidate;
            }

            return null;
        }
    }
}
#endif
