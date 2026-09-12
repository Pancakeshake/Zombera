using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStatePayloadCodec
    {
        public const int MaxDecodedBytes = 128 * 1024 * 1024;

        public static WorldStatePayload Encode(WorldState state, WorldValidationContext context = null)
        {
            var canonical = WorldStateCanonicalizer.CanonicalizeCopy(state, out var report);
            report.Merge(WorldStateValidator.Validate(canonical, WorldValidationMode.Canonical, context));
            if (!report.IsValid)
                throw new InvalidOperationException("Cannot encode invalid WorldState: " + string.Join("; ", report.Errors));

            var hash = WorldStateHasher.ComputeHash(canonical);
            var stateJson = JsonUtility.ToJson(canonical);
            var envelope = new WorldStatePayloadEnvelope
            {
                schemaVersion = WorldStateSchema.CurrentVersion,
                canonicalFormatVersion = WorldStateSchema.CanonicalFormatVersion,
                hashSha256 = hash,
                json = stateJson
            };

            var payloadBase64 = CompressToBase64(
                JsonUtility.ToJson(envelope),
                out var uncompressedByteCount,
                out var compressedByteCount);

            return new WorldStatePayload
            {
                schemaVersion = envelope.schemaVersion,
                canonicalFormatVersion = envelope.canonicalFormatVersion,
                hashSha256 = hash,
                payloadBase64 = payloadBase64,
                uncompressedByteCount = uncompressedByteCount,
                compressedByteCount = compressedByteCount
            };
        }

        public static string EncodeToBase64(WorldState state, WorldValidationContext context = null) =>
            Encode(state, context).payloadBase64;

        public static WorldStatePayloadDecodeResult Decode(
            string payloadBase64,
            WorldValidationContext context = null,
            string expectedHashSha256 = null)
        {
            var report = new WorldValidationReport();
            if (string.IsNullOrWhiteSpace(payloadBase64))
            {
                AddCodecError(report, "$", "Payload is empty.");
                return WorldStatePayloadDecodeResult.Failed(report);
            }

            if (!TryDecodeEnvelope(payloadBase64, report, out var envelope))
                return WorldStatePayloadDecodeResult.Failed(report);

            if (!TryDecodeState(envelope, report, out var state))
                return WorldStatePayloadDecodeResult.Failed(report);

            report.Merge(WorldStateCanonicalizer.Canonicalize(state));
            report.Merge(WorldStateValidator.Validate(state, WorldValidationMode.Canonical, context));
            if (!report.IsValid)
                return new WorldStatePayloadDecodeResult(state, envelope.hashSha256, report);

            var actualHash = WorldStateHasher.ComputeHash(state);
            ValidateHash(envelope.hashSha256, actualHash, "$.hashSha256", report);
            if (!string.IsNullOrWhiteSpace(expectedHashSha256))
                ValidateHash(expectedHashSha256, actualHash, "expectedHashSha256", report);

            return new WorldStatePayloadDecodeResult(state, actualHash, report);
        }

        private static bool TryDecodeEnvelope(
            string payloadBase64,
            WorldValidationReport report,
            out WorldStatePayloadEnvelope envelope)
        {
            envelope = null;
            try
            {
                var compressed = Convert.FromBase64String(payloadBase64);
                var json = Encoding.UTF8.GetString(Decompress(compressed, MaxDecodedBytes));
                envelope = JsonUtility.FromJson<WorldStatePayloadEnvelope>(json);
            }
            catch (Exception ex)
            {
                AddCodecError(report, "$", "Payload could not be decoded.", ex.Message);
                return false;
            }

            if (envelope == null || string.IsNullOrEmpty(envelope.json))
            {
                AddCodecError(report, "$", "Payload envelope is missing state JSON.");
                return false;
            }

            ValidateEnvelopeVersions(envelope, report);
            return report.IsValid;
        }

        private static bool TryDecodeState(
            WorldStatePayloadEnvelope envelope,
            WorldValidationReport report,
            out WorldState state)
        {
            state = null;
            try
            {
                state = JsonUtility.FromJson<WorldState>(envelope.json);
            }
            catch (Exception ex)
            {
                AddCodecError(report, "$.json", "WorldState JSON could not be decoded.", ex.Message);
                return false;
            }

            if (state != null)
                return true;

            AddCodecError(report, "$.json", "WorldState JSON produced a null state.");
            return false;
        }

        private static void ValidateEnvelopeVersions(WorldStatePayloadEnvelope envelope, WorldValidationReport report)
        {
            if (envelope.schemaVersion != WorldStateSchema.CurrentVersion)
                report.AddIssue(WorldValidationSeverity.Error, WorldStateValidator.SchemaUnsupported, default, "$.schemaVersion", "Unsupported payload schema version.", WorldStateSchema.CurrentVersion.ToString(), envelope.schemaVersion.ToString());
            if (envelope.canonicalFormatVersion != WorldStateSchema.CanonicalFormatVersion)
                report.AddIssue(WorldValidationSeverity.Error, WorldStateValidator.SchemaUnsupported, default, "$.canonicalFormatVersion", "Unsupported payload canonical format version.", WorldStateSchema.CanonicalFormatVersion.ToString(), envelope.canonicalFormatVersion.ToString());
        }

        private static string CompressToBase64(
            string json,
            out int uncompressedByteCount,
            out int compressedByteCount)
        {
            var bytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
            uncompressedByteCount = bytes.Length;
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                gzip.Write(bytes, 0, bytes.Length);
            var compressed = output.ToArray();
            compressedByteCount = compressed.Length;
            return Convert.ToBase64String(compressed);
        }

        private static byte[] Decompress(byte[] compressed, int maxBytes)
        {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var read = gzip.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                if (output.Length + read > maxBytes)
                    throw new InvalidOperationException("Decoded WorldState payload exceeds 128MiB.");
                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }

        private static void ValidateHash(string expected, string actual, string path, WorldValidationReport report)
        {
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                AddCodecError(report, path, "WorldState payload hash mismatch.", expected ?? string.Empty, actual ?? string.Empty);
        }

        private static void AddCodecError(WorldValidationReport report, string path, string message, string expected = "", string actual = "")
        {
            report.AddIssue(WorldValidationSeverity.Error, WorldStateValidator.HeaderInvalid, default, path, message, expected, actual);
        }
    }

    [Serializable]
    public sealed class WorldStatePayload
    {
        public int schemaVersion;
        public int canonicalFormatVersion;
        public string hashSha256 = string.Empty;
        public string payloadBase64 = string.Empty;
        public int uncompressedByteCount;
        public int compressedByteCount;
    }

    [Serializable]
    public sealed class WorldStatePayloadDecodeResult
    {
        public WorldState state;
        public string hashSha256;
        public WorldValidationReport report;

        public bool IsValid => report != null && report.IsValid;

        public WorldStatePayloadDecodeResult(WorldState state, string hashSha256, WorldValidationReport report)
        {
            this.state = state;
            this.hashSha256 = hashSha256 ?? string.Empty;
            this.report = report ?? new WorldValidationReport();
        }

        public static WorldStatePayloadDecodeResult Failed(WorldValidationReport report) =>
            new(null, string.Empty, report);
    }

    [Serializable]
    internal sealed class WorldStatePayloadEnvelope
    {
        public int schemaVersion;
        public int canonicalFormatVersion;
        public string hashSha256 = string.Empty;
        public string json = string.Empty;
    }
}
