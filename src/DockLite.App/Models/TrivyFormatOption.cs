namespace DockLite.App.Models;

/// <summary>
/// Một lựa chọn định dạng đầu ra Trivy trong ComboBox (table/json).
/// </summary>
public sealed class TrivyFormatOption
{
    public string Value { get; init; } = "table";

    public string Label { get; init; } = string.Empty;
}
