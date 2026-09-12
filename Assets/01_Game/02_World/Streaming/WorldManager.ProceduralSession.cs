using System;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Simulation;
using Random = UnityEngine.Random;

namespace Zombera.World
{
    public partial class WorldManager
    {
        /// <summary>Restores deterministic procedural session data and chunk deltas from a save slot.</summary>
        public void ApplyLoadedProceduralWorld(ProceduralWorldSaveData data)
        {
            if (data is not { hasData: true }) return;
            if (IsPreparedProceduralWorld(data)) return;
            if (!TryPrepareLoadedProceduralWorld(data, out var error))
                throw new InvalidOperationException(error);
            if (IsSimulationActive && data.worldState is not { hasData: true })
                BeginLegacyProceduralSession(data);
        }

        public bool TryPrepareLoadedProceduralWorld(ProceduralWorldSaveData data, out string error)
        {
            error = string.Empty;
            if (data is not { hasData: true }) return true;

            ResolveRuntimeReferencesIfNeeded(force: true);
            ApplyProceduralSessionMetadata(data);

            var graphVersion = string.IsNullOrEmpty(data.graphVersion) ? string.Empty : data.graphVersion;
            if (!TryPrepareWorldStatePackage(data, graphVersion, out error))
                return false;

            StreamedWorldChunkState.ImportFromSave(data);
            return true;
        }

        private void ApplyProceduralSessionMetadata(ProceduralWorldSaveData data)
        {
            worldSeed = data.worldSeed;
            _pendingMapSizeTier = data.mapSizeTier;
            _hasPendingSessionRequest = true;
            _pendingSavedPlanFingerprint = data.planFingerprint;
            _hasPendingSavedPlanFingerprint = data.planFingerprint != 0UL;
        }

        private bool TryPrepareWorldStatePackage(
            ProceduralWorldSaveData data,
            string graphVersion,
            out string error)
        {
            error = string.Empty;
            var payload = data.worldState;
            if (payload is not { hasData: true })
            {
                QueueLegacyWorldStateModeIfNeeded(graphVersion);
                _preparedWorldStateHash = string.Empty;
                _preparedLegacyNoWorldState = IsFirstPartyGraph(graphVersion);
                return true;
            }

            if (!ValidatePayloadEnvelope(payload, out error))
                return false;
            var decode = WorldStatePayloadCodec.Decode(
                payload.payloadBase64,
                null,
                payload.canonicalHash);
            if (!decode.IsValid)
            {
                error = "WorldState payload is invalid: " + string.Join("; ", decode.report.Errors);
                return false;
            }

            return QueueDecodedWorldState(data, graphVersion, payload, decode, out error);
        }

        private bool QueueDecodedWorldState(
            ProceduralWorldSaveData data,
            string graphVersion,
            WorldStatePayloadSaveData payload,
            WorldStatePayloadDecodeResult decode,
            out string error)
        {
            error = string.Empty;
            var service = WorldGenerationBackend as WorldBuilderService;
            if (service == null)
            {
                error = "WorldState payload requires a first-party WorldBuilderService backend.";
                return false;
            }

            if (!ValidateWorldStateCompatibility(data, graphVersion, payload, decode.state, service, out error))
                return false;

            service.QueueLoadedWorldState(new WorldStateLoadPackage(
                decode.state,
                decode.hashSha256,
                data.worldSeed,
                data.mapSizeTier,
                data.profileVersion,
                ProceduralWorldSession.FirstPartyGraphVersion,
                payload.profileFingerprint,
                payload.planFingerprint));
            _preparedWorldStateHash = decode.hashSha256;
            _preparedLegacyNoWorldState = false;
            return true;
        }

        private void QueueLegacyWorldStateModeIfNeeded(string graphVersion)
        {
            if (!IsFirstPartyGraph(graphVersion))
                return;

            if (WorldGenerationBackend is WorldBuilderService service)
                service.QueueLegacyNoWorldState("Loaded legacy save without WorldState payload.");
        }

        private static bool ValidatePayloadEnvelope(WorldStatePayloadSaveData payload, out string error)
        {
            error = string.Empty;
            if (!string.Equals(payload.contentEncoding, "gzip+base64", StringComparison.Ordinal))
                return Fail("Unsupported WorldState payload encoding.", out error);
            if (!string.Equals(payload.hashAlgorithm, "sha256", StringComparison.OrdinalIgnoreCase))
                return Fail("Unsupported WorldState hash algorithm.", out error);
            if (payload.schemaVersion != WorldStateSchema.CurrentVersion)
                return Fail("Unsupported WorldState schema version.", out error);
            if (payload.canonicalFormatVersion != WorldStateSchema.CanonicalFormatVersion)
                return Fail("Unsupported WorldState canonical format version.", out error);
            if (payload.idAlgorithmVersion != WorldStateSchema.IdAlgorithmVersion)
                return Fail("Unsupported WorldState ID algorithm version.", out error);
            if (string.IsNullOrWhiteSpace(payload.canonicalHash))
                return Fail("WorldState payload is missing its canonical hash.", out error);
            if (string.IsNullOrWhiteSpace(payload.payloadBase64))
                return Fail("WorldState payload data is empty.", out error);
            return true;
        }

        private static bool ValidateWorldStateCompatibility(
            ProceduralWorldSaveData data,
            string graphVersion,
            WorldStatePayloadSaveData payload,
            WorldState state,
            WorldBuilderService service,
            out string error)
        {
            error = string.Empty;
            var header = state?.header;
            if (header == null)
                return Fail("WorldState header is missing.", out error);
            if (!IsFirstPartyGraph(graphVersion) || !IsFirstPartyGraph(payload.graphVersion))
                return Fail("WorldState payload is not for the first-party WorldBuilder graph.", out error);
            if (header.worldSeed != data.worldSeed || payload.worldSeed != data.worldSeed)
                return Fail("WorldState seed does not match the save session.", out error);
            if (header.mapSizeTier != data.mapSizeTier)
                return Fail("WorldState map tier does not match the save session.", out error);
            if (header.profileVersion != data.profileVersion || payload.profileVersion != data.profileVersion)
                return Fail("WorldState profile version does not match the save session.", out error);
            if (!ValidateProfileFingerprint(payload, header, service, out error))
                return false;
            return ValidatePlanFingerprint(payload, header, data.planFingerprint, out error);
        }

        private static bool ValidateProfileFingerprint(
            WorldStatePayloadSaveData payload,
            WorldStateHeader header,
            WorldBuilderService service,
            out string error)
        {
            error = string.Empty;
            var expected = service.Profile != null ? service.Profile.ComputeFingerprint().ToString("x16") : string.Empty;
            if (string.IsNullOrEmpty(expected))
                return true;
            if (!MatchesIfPresent(payload.profileFingerprint, expected))
                return Fail("WorldState profile fingerprint does not match the active profile.", out error);
            if (!MatchesIfPresent(header.profileFingerprint, expected))
                return Fail("WorldState header profile fingerprint does not match the active profile.", out error);
            return true;
        }

        private static bool ValidatePlanFingerprint(
            WorldStatePayloadSaveData payload,
            WorldStateHeader header,
            ulong savedPlanFingerprint,
            out string error)
        {
            error = string.Empty;
            if (savedPlanFingerprint == 0UL)
                return true;
            var expected = savedPlanFingerprint.ToString("x16");
            if (!MatchesIfPresent(payload.planFingerprint, expected))
                return Fail("WorldState plan fingerprint does not match the save session.", out error);
            if (!MatchesIfPresent(header.planFingerprint, expected))
                return Fail("WorldState header plan fingerprint does not match the save session.", out error);
            return true;
        }

        private bool IsPreparedProceduralWorld(ProceduralWorldSaveData data)
        {
            var payload = data.worldState;
            if (payload is { hasData: true })
                return string.Equals(_preparedWorldStateHash, payload.canonicalHash, StringComparison.OrdinalIgnoreCase);
            return _preparedLegacyNoWorldState && IsFirstPartyGraph(data.graphVersion);
        }

        private static bool MatchesIfPresent(string actual, string expected) =>
            string.IsNullOrEmpty(actual) || string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsFirstPartyGraph(string graphVersion) =>
            string.Equals(graphVersion, ProceduralWorldSession.FirstPartyGraphVersion, StringComparison.Ordinal);

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private void BeginLegacyProceduralSession(ProceduralWorldSaveData data)
        {
            var graphVersion = string.IsNullOrEmpty(data.graphVersion) ? string.Empty : data.graphVersion;
            if (data.formatVersion >= 2)
            {
                ProceduralWorldSession.Begin(
                    data.worldSeed,
                    graphVersion,
                    data.mapSizeTier,
                    data.tilesPerSide,
                    data.originTileX,
                    data.originTileZ,
                    data.profileVersion,
                    data.planFingerprint);
            }

            RefreshProceduralSession(data.worldSeed, graphVersion);
        }


        /// <summary>
        ///     Begins a procedural session and initializes the assigned generation backend when present.
        ///     MapMagic pin/seed/generation lives in Legacy <c>MapMagicWorldGenerationBackend</c>.
        /// </summary>
        public void RefreshProceduralSession(int sessionSeed, string graphVersion)
        {
            ProceduralWorldSession.Begin(sessionSeed, graphVersion);
            chunkGenerator?.SetWorldSeed(sessionSeed);
            worldSimulationManager?.SetSimulationSeed(sessionSeed);

            var backend = WorldGenerationBackend;
            if (backend != null)
            {
                if (backend.TileStream != null)
                    tileStreamBridge = backend.TileStream;

                var session = BuildBackendSession(backend, sessionSeed);
                var scope = WorldBuildScope.FullMap(session.WorldBoundsXZ);
                StartCoroutine(backend.InitializeSession(session, scope));
            }

            Random.InitState(sessionSeed);
        }
    }
}
