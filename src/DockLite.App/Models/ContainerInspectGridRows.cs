namespace DockLite.App.Models;

/// <summary>
/// Một dòng mount từ JSON inspect Docker (bảng chi tiết).
/// </summary>
public sealed class InspectMountRow
{
    public string Type { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;
}

/// <summary>
/// Một dòng ánh xạ cổng (container ↔ host).
/// </summary>
public sealed class InspectPortRow
{
    public string ContainerPort { get; init; } = string.Empty;

    public string HostIp { get; init; } = string.Empty;

    public string HostPort { get; init; } = string.Empty;
}

/// <summary>
/// Một biến môi trường (KEY=VALUE).
/// </summary>
public sealed class InspectEnvRow
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}

/// <summary>
/// Một nhãn Docker (Config.Labels).
/// </summary>
public sealed class InspectLabelRow
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}

/// <summary>
/// Một mạng nối (NetworkSettings.Networks).
/// </summary>
public sealed class InspectNetworkRow
{
    public string Name { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public string Gateway { get; init; } = string.Empty;

    public string MacAddress { get; init; } = string.Empty;
}
