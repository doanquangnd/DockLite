using DockLite.Contracts.Api;

namespace DockLite.App.Services;

/// <summary>
/// Kiểm tra đầu vào tối thiểu trước khi gọi <see cref="IDockLiteApiClient"/> — tránh request vô nghĩa và lỗi server khó đọc.
/// </summary>
internal static class ScreenApiInputValidation
{
    public static ApiErrorBody? ContainerIdError(string? containerId)
    {
        if (!string.IsNullOrWhiteSpace(containerId))
        {
            return null;
        }

        return new ApiErrorBody
        {
            Code = "validation_empty_id",
            Message = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Validation_ScreenApi_ContainerIdEmpty",
                "ID container không được để trống."),
        };
    }

    public static ApiErrorBody? ImageIdError(string? imageId)
    {
        if (!string.IsNullOrWhiteSpace(imageId))
        {
            return null;
        }

        return new ApiErrorBody
        {
            Code = "validation_empty_id",
            Message = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Validation_ScreenApi_ImageIdEmpty",
                "ID image không được để trống."),
        };
    }

    public static ApiErrorBody? ProjectIdError(string? projectId)
    {
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        return new ApiErrorBody
        {
            Code = "validation_empty_id",
            Message = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Validation_ScreenApi_ProjectIdEmpty",
                "ID project Compose không được để trống."),
        };
    }

    public static ApiErrorBody? ComposeServiceRequestError(ComposeServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Service))
        {
            return new ApiErrorBody
            {
                Code = "validation_compose_service",
                Message = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Validation_ScreenApi_ComposeServiceFields",
                    "Project và tên service Compose không được để trống."),
            };
        }

        return null;
    }

    public static ApiErrorBody? ComposeServiceLogsRequestError(ComposeServiceLogsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Service))
        {
            return new ApiErrorBody
            {
                Code = "validation_compose_service",
                Message = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Validation_ScreenApi_ComposeServiceFields",
                    "Project và tên service Compose không được để trống."),
            };
        }

        return null;
    }

    public static ApiErrorBody? ComposeServiceExecRequestError(ComposeServiceExecRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Service))
        {
            return new ApiErrorBody
            {
                Code = "validation_compose_service",
                Message = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Validation_ScreenApi_ComposeServiceFields",
                    "Project và tên service Compose không được để trống."),
            };
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return new ApiErrorBody
            {
                Code = "validation_exec_command",
                Message = UiLanguageManager.TryLocalizeCurrent(
                    "Ui_Validation_ScreenApi_ComposeExecCommandEmpty",
                    "Lệnh exec không được để trống."),
            };
        }

        return null;
    }

    public static ApiErrorBody? ImagePullRequestError(ImagePullRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Reference))
        {
            return null;
        }

        return new ApiErrorBody
        {
            Code = "validation_image_reference",
            Message = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Validation_ScreenApi_ImagePullReferenceEmpty",
                "Tham chiếu image (pull) không được để trống."),
        };
    }

    public static ApiErrorBody? ImageRemoveRequestError(ImageRemoveRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            return null;
        }

        return new ApiErrorBody
        {
            Code = "validation_empty_id",
            Message = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Validation_ScreenApi_ImageRemoveIdEmpty",
                "ID image cần xóa không được để trống."),
        };
    }
}
