namespace DockLite.Contracts.Api;

/// <summary>
/// Mã lỗi domain từ service Go (khớp internal/apiresponse).
/// </summary>
public static class DockLiteErrorCodes
{
    public const string Validation = "VALIDATION";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string DockerCli = "DOCKER_CLI";
    public const string DockerUnavailable = "DOCKER_UNAVAILABLE";
    public const string Internal = "INTERNAL";
    public const string BadGateway = "BAD_GATEWAY";

    /// <summary>Lệnh compose / prune thất bại (có thể kèm details).</summary>
    public const string ComposeCommand = "COMPOSE_COMMAND";
    public const string Parse = "PARSE";
    public const string Unknown = "UNKNOWN";
    public const string RateLimit = "RATE_LIMIT";
    public const string Auth = "AUTH";
}
