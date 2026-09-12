using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zombera.World.Enviro;

namespace Zombera.Editor
{
    /// <summary>
    /// Suppresses the fog the world-build scene draws through while a hub run generates the world,
    /// then puts the previous state back.
    /// <para>
    /// Enviro's atmosphere fog is the source that actually hides the terrain. Its module exposes two
    /// density layers and an opacity scale
    /// (<c>EnviroFogModule.Settings</c>: <c>fogDensity</c>, <c>fogDensity2</c>, <c>fogMaxOpacity</c>),
    /// and the authored <c>WorldEnvironmentProfile</c> drives only the first density — so zeroing the
    /// density alone leaves the second layer and a fully opaque effect behind, which is why the scene
    /// stayed foggy. Suppression therefore also turns <c>fogMaxOpacity</c> to 0, the value the fog
    /// shader multiplies the whole effect by.
    /// </para>
    /// <para>
    /// The authored profile is deliberately not modified: its fog density feeds
    /// <c>WorldProfileFingerprints</c>, so editing it for a test run would invalidate persisted world
    /// state. Suppression is transient instead — <see cref="EnviroFogOverride.SuppressFog"/> makes the
    /// Environment stages honour it for the whole run, and <see cref="Restore"/> replays the snapshot.
    /// </para>
    /// </summary>
    internal static class WorldBuildFogSuppressor
    {
        private static readonly string[] EnviroFogBoolMembers = { "fog", "volumetrics" };
        private static readonly string[] EnviroFogFloatMembers = { "fogDensity", "fogDensity2", "fogMaxOpacity" };

        private static Snapshot _snapshot;

        /// <summary>
        /// A recompile mid-run drops this static snapshot while the scene module keeps the suppressed
        /// fog opacity, so put the previous values back before the domain goes away. The Enviro fog
        /// settings are a plain class on the scene component, not an asset, so nothing else would.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void HookAssemblyReload() =>
            AssemblyReloadEvents.beforeAssemblyReload += Restore;

        /// <summary>Suppresses fog and remembers what to put back. Safe to call repeatedly.</summary>
        public static void Apply()
        {
            if (_snapshot != null)
                return;

            var snapshot = new Snapshot
            {
                RenderFog = RenderSettings.fog,
                RenderFogMode = RenderSettings.fogMode,
                RenderFogDensity = RenderSettings.fogDensity,
                RenderFogColor = RenderSettings.fogColor
            };
            CaptureEnviro(snapshot);
            _snapshot = snapshot;

            EnviroFogOverride.SuppressFog = true;
            RenderSettings.fog = false;
            SuppressEnviroFog();
        }

        /// <summary>Restores the fog state captured by <see cref="Apply"/>. No-op when not applied.</summary>
        public static void Restore()
        {
            var snapshot = _snapshot;
            if (snapshot == null)
                return;

            _snapshot = null;
            EnviroFogOverride.SuppressFog = false;
            RenderSettings.fog = snapshot.RenderFog;
            RenderSettings.fogMode = snapshot.RenderFogMode;
            RenderSettings.fogDensity = snapshot.RenderFogDensity;
            RenderSettings.fogColor = snapshot.RenderFogColor;
            RestoreEnviroFog(snapshot);
        }

        private static void CaptureEnviro(Snapshot snapshot)
        {
            var module = ResolveEnviroFogModule();
            if (module == null)
                return;

            snapshot.EnviroFound = true;
            snapshot.EnviroModuleActive = ReadBool(module, "active");
            var settings = ReadMember(module, "Settings");
            if (settings == null)
                return;

            snapshot.EnviroSettingsFound = true;
            snapshot.EnviroBools = new bool[EnviroFogBoolMembers.Length];
            for (var i = 0; i < EnviroFogBoolMembers.Length; i++)
                snapshot.EnviroBools[i] = ReadBool(settings, EnviroFogBoolMembers[i]);

            snapshot.EnviroFloats = new float[EnviroFogFloatMembers.Length];
            for (var i = 0; i < EnviroFogFloatMembers.Length; i++)
                snapshot.EnviroFloats[i] = ReadFloat(settings, EnviroFogFloatMembers[i]);
        }

        private static void SuppressEnviroFog()
        {
            var module = ResolveEnviroFogModule();
            if (module == null)
                return;

            Write(module, "active", false);
            var settings = ReadMember(module, "Settings");
            if (settings == null)
                return;

            for (var i = 0; i < EnviroFogBoolMembers.Length; i++)
                Write(settings, EnviroFogBoolMembers[i], false);

            for (var i = 0; i < EnviroFogFloatMembers.Length; i++)
                Write(settings, EnviroFogFloatMembers[i], 0f);
        }

        private static void RestoreEnviroFog(Snapshot snapshot)
        {
            if (!snapshot.EnviroFound)
                return;

            var module = ResolveEnviroFogModule();
            if (module == null)
                return;

            Write(module, "active", snapshot.EnviroModuleActive);
            var settings = ReadMember(module, "Settings");
            if (settings == null || !snapshot.EnviroSettingsFound)
                return;

            for (var i = 0; i < EnviroFogBoolMembers.Length; i++)
                Write(settings, EnviroFogBoolMembers[i], snapshot.EnviroBools[i]);

            for (var i = 0; i < EnviroFogFloatMembers.Length; i++)
                Write(settings, EnviroFogFloatMembers[i], snapshot.EnviroFloats[i]);
        }

        private static object ResolveEnviroFogModule()
        {
            var managerType = FindType("Enviro.EnviroManager");
            if (managerType == null)
                return null;

            var manager = ReadMember(managerType, null, "instance");
            return manager == null ? null : ReadMember(manager, "Fog");
        }

        private static object ReadMember(object target, string member)
            => target == null ? null : ReadMember(target.GetType(), target, member);

        private static object ReadMember(Type type, object target, string member)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var property = type.GetProperty(member, flags);
            if (property != null && property.CanRead)
                return property.GetValue(target);

            var field = type.GetField(member, flags);
            return field != null ? field.GetValue(target) : null;
        }

        private static bool ReadBool(object target, string member)
            => ReadMember(target, member) is bool value && value;

        private static float ReadFloat(object target, string member)
            => ReadMember(target, member) is float value ? value : 0f;

        private static void Write(object target, string member, object value)
        {
            if (target == null)
                return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = target.GetType();
            var property = type.GetProperty(member, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }

            type.GetField(member, flags)?.SetValue(target, value);
        }

        private static Type FindType(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(name, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private sealed class Snapshot
        {
            public bool RenderFog;
            public FogMode RenderFogMode;
            public float RenderFogDensity;
            public Color RenderFogColor;
            public bool EnviroFound;
            public bool EnviroSettingsFound;
            public bool EnviroModuleActive;
            public bool[] EnviroBools;
            public float[] EnviroFloats;
        }
    }
}
