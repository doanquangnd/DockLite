namespace DockLite.Contracts.Api;

/// <summary>
/// Ánh xạ envelope JSON sang <see cref="ApiResult{T}"/> (dùng chung client và test).
/// </summary>
public static class ApiEnvelopeExtensions
{
    /// <summary>
    /// Chuyển envelope đã deserialize thành kết quả API (cùng quy tắc với <c>DockLiteApiClient.ReadEnvelopeAsync</c> sau khi có JSON).
    /// </summary>
    public static ApiResult<T> ToApiResult<T>(this ApiEnvelope<T>? env)
    {
        if (env is null)
        {
            return ApiResult<T>.Fail(new ApiErrorBody
            {
                Code = DockLiteErrorCodes.Parse,
                Message = "Không parse được envelope.",
            });
        }

        if (!env.Success)
        {
            return ApiResult<T>.Fail(env.Error ?? new ApiErrorBody
            {
                Code = DockLiteErrorCodes.Unknown,
                Message = "Lỗi không xác định.",
            });
        }

        return ApiResult<T>.Ok(env.Data);
    }
}
