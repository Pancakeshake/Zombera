using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombera.Core
{
    /// <summary>
    ///     Resolves UMA library components without serialized cross-scene references.
    ///     Prefers scene-local context/generator and falls back to globally loaded UMA roots.
    /// </summary>
    public static class UmaGlobalLibraryService
    {
        private static readonly Dictionary<int, UMAContextBase> ContextBySceneHandle = new();
        private static readonly Dictionary<int, UMAGeneratorBase> GeneratorBySceneHandle = new();

        private static UMAContextBase _globalContext;
        private static UMAGeneratorBase _globalGenerator;
        private static int _cachedSceneCount = -1;

        public static void Initialize()
        {
            RefreshCache(force: true);
        }

        public static void RefreshCache(bool force = false)
        {
            if (!force && _cachedSceneCount == SceneManager.sceneCount) return;

            ContextBySceneHandle.Clear();
            GeneratorBySceneHandle.Clear();
            _globalContext = null;
            _globalGenerator = null;

            var contexts = Object.FindObjectsByType<UMAContextBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < contexts.Length; i++)
            {
                var context = contexts[i];
                if (context == null) continue;

                var contextScene = context.gameObject.scene;
                if (contextScene.IsValid() && contextScene.isLoaded)
                {
                    if (!ContextBySceneHandle.ContainsKey(contextScene.handle))
                        ContextBySceneHandle[contextScene.handle] = context;
                }

                if (_globalContext == null || context.gameObject.activeInHierarchy)
                    _globalContext = context;
            }

            var generators = Object.FindObjectsByType<UMAGeneratorBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < generators.Length; i++)
            {
                var generator = generators[i];
                if (generator == null) continue;

                var generatorScene = generator.gameObject.scene;
                if (generatorScene.IsValid() && generatorScene.isLoaded)
                {
                    if (!GeneratorBySceneHandle.ContainsKey(generatorScene.handle))
                        GeneratorBySceneHandle[generatorScene.handle] = generator;
                }

                if (_globalGenerator == null || generator.gameObject.activeInHierarchy)
                    _globalGenerator = generator;
            }

            if (_globalContext == null)
                _globalContext = UMAContextBase.Instance;

            if (_globalGenerator == null)
                _globalGenerator = Object.FindFirstObjectByType<UMAGeneratorBase>(FindObjectsInactive.Include);

            _cachedSceneCount = SceneManager.sceneCount;
        }

        public static bool TryResolveLibraryForScene(
            Scene scene,
            out UMAContextBase context,
            out UMAGeneratorBase generator,
            bool allowGlobalFallback = true)
        {
            EnsureCache();

            context = null;
            generator = null;

            if (scene.IsValid() && scene.isLoaded)
            {
                ContextBySceneHandle.TryGetValue(scene.handle, out context);
                GeneratorBySceneHandle.TryGetValue(scene.handle, out generator);
            }

            if (!allowGlobalFallback)
                return context != null || generator != null;

            if (context == null) context = _globalContext;
            if (generator == null) generator = _globalGenerator;

            return context != null || generator != null;
        }

        public static bool TryBindAvatarLibrary(DynamicCharacterAvatar avatar, bool allowGlobalFallback = true)
        {
            if (avatar == null) return false;

            var avatarScene = avatar.gameObject.scene;
            if (!TryResolveLibraryForScene(avatarScene, out var context, out var generator, allowGlobalFallback))
                return false;

            var changed = false;

            if (context != null && avatar.context != context)
            {
                avatar.context = context;
                changed = true;
            }

            if (generator != null && avatar.umaGenerator != generator)
            {
                avatar.umaGenerator = generator;
                changed = true;
            }

            if (UmaAnimationControllerUtility.EnsureAnimationController(avatar))
                changed = true;

            return changed;
        }

        public static bool ClearCrossSceneReferences(DynamicCharacterAvatar avatar)
        {
            if (avatar == null) return false;

            var avatarScene = avatar.gameObject.scene;
            var changed = false;

            if (avatar.context != null && avatar.context.gameObject.scene != avatarScene)
            {
                avatar.context = null;
                changed = true;
            }

            if (avatar.umaGenerator != null && avatar.umaGenerator.gameObject.scene != avatarScene)
            {
                avatar.umaGenerator = null;
                changed = true;
            }

            return changed;
        }

        public static void AutoBindAllAvatarsInActiveScenes()
        {
            RefreshCache(force: true);
            var avatars = Object.FindObjectsByType<DynamicCharacterAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var avatar in avatars)
            {
                ClearCrossSceneReferences(avatar);
                TryBindAvatarLibrary(avatar);
            }
        }

        private static void EnsureCache()
        {
            if (_cachedSceneCount != SceneManager.sceneCount)
            {
                RefreshCache(force: true);
                return;
            }

            if (_globalContext != null && _globalGenerator != null) return;
            RefreshCache(force: true);
        }
    }
}
