namespace DockLite.Contracts.Api;

/// <summary>
/// Kết quả gọi API có envelope: thành công kèm dữ liệu hoặc thất bại kèm lỗi domain.
/// </summary>
public readonly record struct ApiResult<T>(bool Success, T? Data, ApiErrorBody? Error)
{
    public static ApiResult<T> Ok(T? data) => new(true, data, null);

    public static ApiResult<T> Fail(ApiErrorBody? error) => new(false, default, error);
}
