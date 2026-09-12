using System;
using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed partial class EasyRoadsRoadGameplayBridge
    {
        private GameplayRoadType ResolveRoadTypeFromName(string roadName)
        {
            if (ContainsAnyToken(roadName, "highway", "motorway", "freeway")) return GameplayRoadType.Highway;
            if (ContainsAnyToken(roadName, "arterial", "avenue", "boulevard", "main")) return GameplayRoadType.Arterial;
            if (ContainsAnyToken(roadName, "service", "alley", "access")) return GameplayRoadType.ServiceRoad;
            if (ContainsAnyToken(roadName, "path", "walk", "foot")) return GameplayRoadType.Footpath;
            if (ContainsAnyToken(roadName, "dirt", "gravel", "offroad", "trail", "track"))
                return GameplayRoadType.DirtTrack;

            return defaultRoadType;
        }

        private RoadCityZone ResolveCityZoneFromName(
            string roadName,
            GameplayRoadType roadType,
            bool forceCityCoreByGraphName)
        {
            if (forceCityCoreByGraphName)
                return RoadCityZone.CityCore;

            if (ContainsAnyToken(roadName, cityCoreNameTokens)) return RoadCityZone.CityCore;
            if (ContainsAnyToken(roadName, residentialNameTokens)) return RoadCityZone.Residential;
            if (ContainsAnyToken(roadName, commercialNameTokens)) return RoadCityZone.Commercial;
            if (ContainsAnyToken(roadName, industrialNameTokens)) return RoadCityZone.Industrial;
            if (ContainsAnyToken(roadName, slumNameTokens)) return RoadCityZone.Slum;

            return roadType switch
            {
                GameplayRoadType.Highway => RoadCityZone.Mixed,
                GameplayRoadType.Arterial => RoadCityZone.Mixed,
                GameplayRoadType.Local => RoadCityZone.Residential,
                GameplayRoadType.ServiceRoad => RoadCityZone.Service,
                GameplayRoadType.Footpath => RoadCityZone.Mixed,
                GameplayRoadType.DirtTrack => RoadCityZone.Slum,
                _ => RoadCityZone.Mixed
            };
        }

        private bool ShouldForceCityCoreFromMapGraphName()
        {
            // MapMagic graph-name city-core heuristic lives in Legacy; first-party path skips it.
            if (!useMapMagicGraphNameForCityCoreZone) return false;
            if (cityCoreGraphNameTokens == null || cityCoreGraphNameTokens.Length == 0) return false;
            return false;
        }

        private static float ResolveSpeedModifier(GameplayRoadType type)
        {
            return type switch
            {
                GameplayRoadType.Highway => 1.2f,
                GameplayRoadType.Arterial => 1.1f,
                GameplayRoadType.DirtTrack => 0.85f,
                GameplayRoadType.Footpath => 0.7f,
                _ => 1f
            };
        }

        private static float ResolveNoiseLevel(GameplayRoadType type)
        {
            return type switch
            {
                GameplayRoadType.Highway => 0.35f,
                GameplayRoadType.Arterial => 0.22f,
                GameplayRoadType.DirtTrack => 0.12f,
                GameplayRoadType.Footpath => 0.05f,
                _ => 0.16f
            };
        }

        private static bool ContainsAnyToken(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source) || tokens == null || tokens.Length == 0) return false;

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }
    }
}
