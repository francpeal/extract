using System.Text;

namespace WinBridgeApi.Extraction;

internal static class OpaqueCursor
{
    internal static bool TryDecode(
        string scope,
        string? cursor,
        out string? value)
    {
        value = null;
        if (cursor is null)
        {
            return true;
        }
        if (cursor.Length is 0 or > 8192)
        {
            return false;
        }
        try
        {
            var encoded = cursor.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var prefix = $"{scope}:";
            if (!payload.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
            value = payload[prefix.Length..];
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static string Encode(string scope, string value)
    {
        var payload = Encoding.UTF8.GetBytes($"{scope}:{value}");
        return Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
