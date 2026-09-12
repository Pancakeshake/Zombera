#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    internal static class StreamedCityProxySceneBuilderAdapter
    {
        private static class PropertyNames
        {
            public const string Catalog = "cityCatalog";
            public const string UseProxySwapWhenAvailable = "useProxySwapWhenAvailable";
            public const string DefaultProxySwapDistanceMeters = "defaultProxySwapDistanceMeters";
        }

        internal static bool TryResolveCatalog(
            WorldStreamedCityBuilder builder,
            out StreamedCityCatalog catalog,
            out string warning)
        {
            catalog = null;
            warning = string.Empty;

            if (builder == null)
            {
                warning = "Could not resolve streamed city builder catalog because builder was null.";
                return false;
            }

            var so = new SerializedObject(builder);
            var catalogProperty = so.FindProperty(PropertyNames.Catalog);
            if (catalogProperty == null)
            {
                warning = "Builder '" + builder.name + "' is missing serialized field '" + PropertyNames.Catalog +
                          "'; falling back to default catalog path.";
                return false;
            }

            catalog = catalogProperty.objectReferenceValue as StreamedCityCatalog;
            return true;
        }

        internal static bool TryEnableProxySwapDefaults(
            WorldStreamedCityBuilder builder,
            float defaultSwapDistanceMeters,
            out bool changed,
            out string warning)
        {
            changed = false;
            warning = string.Empty;

            if (builder == null)
            {
                warning = "Cannot apply scene builder proxy defaults because builder reference was null.";
                return false;
            }

            var so = new SerializedObject(builder);
            var useProxyProperty = so.FindProperty(PropertyNames.UseProxySwapWhenAvailable);
            var defaultDistanceProperty = so.FindProperty(PropertyNames.DefaultProxySwapDistanceMeters);
            if (useProxyProperty == null || defaultDistanceProperty == null)
            {
                warning = BuildMissingPropertyWarning(builder, useProxyProperty == null, defaultDistanceProperty == null);
                return false;
            }

            if (!useProxyProperty.boolValue)
            {
                useProxyProperty.boolValue = true;
                changed = true;
            }

            if (!Mathf.Approximately(defaultDistanceProperty.floatValue, defaultSwapDistanceMeters))
            {
                defaultDistanceProperty.floatValue = defaultSwapDistanceMeters;
                changed = true;
            }

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        private static string BuildMissingPropertyWarning(
            WorldStreamedCityBuilder builder,
            bool missingUseProxy,
            bool missingDefaultDistance)
        {
            var missing = string.Empty;
            if (missingUseProxy)
                missing += "'" + PropertyNames.UseProxySwapWhenAvailable + "'";

            if (missingDefaultDistance)
            {
                if (!string.IsNullOrEmpty(missing))
                    missing += ", ";

                missing += "'" + PropertyNames.DefaultProxySwapDistanceMeters + "'";
            }

            return "Builder '" + builder.name + "' is missing serialized field(s): " + missing +
                   ". Proxy scene defaults could not be applied.";
        }
    }
}
#endif
