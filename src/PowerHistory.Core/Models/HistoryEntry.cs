namespace PowerHistory.Core.Models;

/// <summary>
/// A single command line read from a history file.
/// </summary>
/// <param name="Index">Zero-based position of the entry inside the file snapshot it was read from.</param>
/// <param name="Text">Raw command text, preserved verbatim.</param>
public sealed record HistoryEntry(int Index, string Text);
