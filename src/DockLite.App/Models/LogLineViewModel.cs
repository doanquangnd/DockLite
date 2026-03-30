namespace DockLite.App.Models;

/// <summary>
/// Một dòng log hiển thị trong danh sách.
/// </summary>
public sealed class LogLineViewModel
{
    public LogLineViewModel(string text, LogSeverity severity)
    {
        Text = text;
        Severity = severity;
    }

    public string Text { get; }

    public LogSeverity Severity { get; }
}
