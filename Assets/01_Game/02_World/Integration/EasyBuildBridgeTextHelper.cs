using System.Text;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeTextHelper
    {
        internal static string NormalizeKeyText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var value = raw.Trim();
            if (value.Equals("Mouse0", System.StringComparison.OrdinalIgnoreCase)) return "LMB";
            if (value.Equals("Mouse1", System.StringComparison.OrdinalIgnoreCase)) return "RMB";
            if (value.Equals("Mouse2", System.StringComparison.OrdinalIgnoreCase)) return "MMB";
            return value;
        }

        internal static string NormalizeBindingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var value = path.Trim().ToLowerInvariant();
            return value switch
            {
                "<mouse>/leftbutton" => "LMB",
                "<mouse>/rightbutton" => "RMB",
                "<mouse>/middlebutton" => "MMB",
                "<mouse>/scroll/y" => "Mouse Wheel",
                _ when value.StartsWith("<keyboard>/") => value.Substring("<keyboard>/".Length).ToUpperInvariant(),
                _ => path
            };
        }

        internal static string NormalizeDiagnosticToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!char.IsLetterOrDigit(character)) continue;

                sb.Append(char.ToLowerInvariant(character));
            }

            return sb.ToString();
        }
    }
}
