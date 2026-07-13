namespace WinBridgeApi.Extraction;

public sealed record ExtractionPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    string ExtractedAt);
