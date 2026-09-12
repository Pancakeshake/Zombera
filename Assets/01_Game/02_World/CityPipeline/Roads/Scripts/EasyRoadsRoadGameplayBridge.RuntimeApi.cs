using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed partial class EasyRoadsRoadGameplayBridge
    {
        private static class EasyRoadsRuntimeApi
        {
            private static bool _resolved;
            private static Type _modularBaseType;
            private static Type _roadType;
            private static Type _roadNetworkType;
            private static ConstructorInfo _roadNetworkCtor;
            private static MethodInfo _getRoadsMethod;
            private static MethodInfo _getSplinePointsCenterMethod;
            private static MethodInfo _getWidthMethod;

            public static bool TryCollectRoadCenterLines(List<EasyRoadLineData> output)
            {
                if (output == null) return false;
                output.Clear();

                if (!ResolveApi()) return false;
                if (!HasAnyRoadRuntimeObject()) return false;

                object roadNetwork;
                try
                {
                    roadNetwork = _roadNetworkCtor != null
                        ? _roadNetworkCtor.Invoke(null)
                        : Activator.CreateInstance(_roadNetworkType);
                }
                catch
                {
                    return false;
                }

                if (roadNetwork == null) return false;

                object roadsRaw;
                try
                {
                    roadsRaw = _getRoadsMethod.IsStatic
                        ? _getRoadsMethod.Invoke(null, null)
                        : _getRoadsMethod.Invoke(roadNetwork, null);
                }
                catch
                {
                    return false;
                }

                if (roadsRaw is not IEnumerable roadEnumerable) return false;

                foreach (var road in roadEnumerable)
                {
                    if (road == null) continue;

                    object rawPoints;
                    try
                    {
                        rawPoints = _getSplinePointsCenterMethod.Invoke(road, null);
                    }
                    catch
                    {
                        continue;
                    }

                    var points = ConvertPoints(rawPoints);
                    if (points == null || points.Count < 2) continue;

                    var roadName = ResolveRoadName(road);
                    var roadWidth = ResolveRoadWidth(road);
                    output.Add(new EasyRoadLineData(roadName, points, roadWidth));
                }

                return output.Count > 0;
            }

            private static bool ResolveApi()
            {
                if (_resolved)
                    return _roadNetworkType is not null
                           && _roadType is not null
                           && _getRoadsMethod is not null
                           && _getSplinePointsCenterMethod is not null;

                _resolved = true;

                _modularBaseType = FindType("EasyRoads3Dv3.ERModularBase");
                _roadType = FindType("EasyRoads3Dv3.ERRoad");
                _roadNetworkType = FindType("EasyRoads3Dv3.ERRoadNetwork");

                if (_roadType == null || _roadNetworkType == null)
                    return false;

                _roadNetworkCtor = _roadNetworkType.GetConstructor(Type.EmptyTypes);
                _getRoadsMethod = _roadNetworkType.GetMethod(
                    "GetRoads",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);

                _getSplinePointsCenterMethod = _roadType.GetMethod(
                    "GetSplinePointsCenter",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                _getWidthMethod = _roadType.GetMethod(
                    "GetWidth",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                return _getRoadsMethod != null && _getSplinePointsCenterMethod != null;
            }

            private static bool HasAnyRoadRuntimeObject()
            {
                if (_modularBaseType == null)
                {
                    var roads = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);

                    foreach (var candidate in roads)
                    {
                        if (candidate == null) continue;
                        if (_roadType.IsInstanceOfType(candidate)) return true;
                    }

                    return false;
                }

                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

                foreach (var candidate in behaviours)
                {
                    if (candidate == null) continue;
                    if (_modularBaseType.IsInstanceOfType(candidate)) return true;
                }

                return false;
            }

            private static List<Vector3> ConvertPoints(object rawPoints)
            {
                if (rawPoints == null) return null;

                if (rawPoints is Vector3[] pointArray)
                {
                    var converted = new List<Vector3>(pointArray.Length);
                    foreach (var pt in pointArray)
                        converted.Add(pt);

                    return converted;
                }

                if (rawPoints is IEnumerable enumerable)
                {
                    var converted = new List<Vector3>(32);
                    foreach (var element in enumerable)
                        if (element is Vector3 p)
                            converted.Add(p);

                    return converted;
                }

                return null;
            }

            private static string ResolveRoadName(object road)
            {
                if (road is UnityEngine.Object unityObject && unityObject != null)
                    return unityObject.name;

                return road?.ToString() ?? string.Empty;
            }

            private static float ResolveRoadWidth(object road)
            {
                if (road == null || _getWidthMethod == null) return 0f;

                try
                {
                    var value = _getWidthMethod.Invoke(road, null);
                    if (value is float widthFloat) return widthFloat;
                    if (value is double widthDouble) return (float)widthDouble;
                    if (value is int widthInt) return widthInt;
                }
                catch
                {
                    return 0f;
                }

                return 0f;
            }

            private static Type FindType(string fullTypeName)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var candidate = assembly.GetType(fullTypeName, false);
                    if (candidate != null) return candidate;
                }

                return null;
            }
        }
    }
}
