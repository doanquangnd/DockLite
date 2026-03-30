using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;
using DockLite.Infrastructure.Wsl;
using Microsoft.Win32;

namespace DockLite.App.ViewModels;

/// <summary>
/// Quản lý project Docker Compose (đường dẫn Windows được service chuyển sang WSL).
/// </summary>
public partial class ComposeViewModel : ObservableObject
{
    private const int ToastMessageMaxChars = 520;

    private readonly IDockLiteApiClient _apiClient;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;

    public ObservableCollection<ComposeProjectDto> Projects { get; } = new();

    /// <summary>
    /// Tên service trong file compose (sau khi tải bằng «Tải danh sách service»).
    /// </summary>
    public ObservableCollection<string> Services { get; } = new();

    [ObservableProperty]
    private ComposeProjectDto? _selectedProject;

    [ObservableProperty]
    private string? _selectedService;

    /// <summary>
    /// Số dòng tail cho lệnh compose logs theo service (1–10000, mặc định 200).
    /// </summary>
    [ObservableProperty]
    private int _composeServiceLogsTail = 200;

    /// <summary>
    /// Lệnh gửi tới <c>docker compose exec -T</c> (các từ cách nhau bằng khoảng trắng).
    /// </summary>
    [ObservableProperty]
    private string _composeExecCommandLine = "uname -a";

    [ObservableProperty]
    private string _newProjectPath = string.Empty;

    [ObservableProperty]
    private string _commandOutput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canLoadComposeServices;

    [ObservableProperty]
    private bool _canComposeServiceAction;

    public ComposeViewModel(IDockLiteApiClient apiClient, INotificationService notificationService, IAppShutdownToken shutdownToken)
    {
        _apiClient = apiClient;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        UpdateComposeServiceUiState();
    }

    partial void OnSelectedProjectChanged(ComposeProjectDto? value)
    {
        Services.Clear();
        SelectedService = null;
        UpdateComposeServiceUiState();
    }

    partial void OnSelectedServiceChanged(string? value)
    {
        UpdateComposeServiceUiState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateComposeServiceUiState();
    }

    private void UpdateComposeServiceUiState()
    {
        CanLoadComposeServices = SelectedProject is not null && !IsBusy;
        CanComposeServiceAction = SelectedProject is not null
            && !string.IsNullOrWhiteSpace(SelectedService)
            && !IsBusy;
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ComposeProjectListData> res = await _apiClient.GetComposeProjectsAsync(_shutdownToken.Token).ConfigureAwait(true);
            Projects.Clear();
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Phản hồi không hợp lệ.";
                return;
            }

            if (res.Data?.Items is not null)
            {
                foreach (ComposeProjectDto p in res.Data.Items)
                {
                    Projects.Add(p);
                }
            }

            StatusMessage = $"Đã tải {Projects.Count} project.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Với UNC WSL (\\wsl.localhost\ hoặc \\wsl$\), gửi thêm wslPath dạng /home/... để service không phụ thuộc parse lại từ chuỗi đã đổi dấu.
    /// </summary>
    private static ComposeProjectAddRequest CreateComposeAddRequest(string path)
    {
        if (WslPathNormalizer.TryUnixPathFromWslUnc(path, expectedDistro: null, out string unixPath, out _))
        {
            return new ComposeProjectAddRequest
            {
                WindowsPath = path,
                WslPath = unixPath,
            };
        }

        return new ComposeProjectAddRequest { WindowsPath = path };
    }

    [RelayCommand]
    private void BrowseProjectFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Chọn thư mục chứa docker-compose (đường dẫn Windows)",
        };
        string t = NewProjectPath.Trim();
        try
        {
            if (!string.IsNullOrEmpty(t) && Directory.Exists(t))
            {
                dlg.InitialDirectory = t;
            }
        }
        catch
        {
            // Bỏ qua: InitialDirectory không hợp lệ.
        }

        if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.FolderName))
        {
            NewProjectPath = dlg.FolderName;
        }
    }

    [RelayCommand]
    private async Task AddProjectAsync()
    {
        string path = NewProjectPath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            StatusMessage = "Nhập đường dẫn thư mục project (Windows hoặc /mnt/...).";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ComposeProjectAddRequest req = CreateComposeAddRequest(path);
            ApiResult<ComposeProjectAddData> res = await _apiClient.AddComposeProjectAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Thêm thất bại.";
                return;
            }

            NewProjectPath = string.Empty;
            StatusMessage = "Đã thêm project.";
            await LoadProjectsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveProjectAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Chọn project để xóa.";
            return;
        }

        IsBusy = true;
        try
        {
            ApiResult<EmptyApiPayload> res = await _apiClient.RemoveComposeProjectAsync(SelectedProject.Id, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Xóa thất bại.";
                return;
            }

            StatusMessage = "Đã xóa khỏi danh sách.";
            await LoadProjectsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ComposeUpAsync()
    {
        await RunComposeAsync(id => _apiClient.ComposeUpAsync(id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposeDownAsync()
    {
        await RunComposeAsync(id => _apiClient.ComposeDownAsync(id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposePsAsync()
    {
        await RunComposeAsync(id => _apiClient.ComposePsAsync(id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task LoadComposeServicesAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Chọn project.";
            return;
        }

        IsBusy = true;
        CommandOutput = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ComposeServiceListData> res = await _apiClient
                .ListComposeServicesAsync(SelectedProject.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message ?? "Không đọc được danh sách service.";
                return;
            }

            Services.Clear();
            if (res.Data?.Items is not null)
            {
                foreach (string s in res.Data.Items)
                {
                    Services.Add(s);
                }
            }

            CommandOutput = res.Data?.Output ?? string.Empty;
            StatusMessage = $"Đã tải {Services.Count} service.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ComposeServiceStartAsync()
    {
        await RunComposeServiceAsync(req => _apiClient.ComposeServiceStartAsync(req, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposeServiceStopAsync()
    {
        await RunComposeServiceAsync(req => _apiClient.ComposeServiceStopAsync(req, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposeServiceLogsAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Chọn project.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedService))
        {
            StatusMessage = "Chọn service.";
            return;
        }

        int tail = ComposeServiceLogsTail;
        if (tail < 1)
        {
            tail = 200;
        }

        if (tail > 10000)
        {
            tail = 10000;
        }

        IsBusy = true;
        CommandOutput = string.Empty;
        try
        {
            var req = new ComposeServiceLogsRequest
            {
                Id = SelectedProject.Id,
                Service = SelectedService.Trim(),
                Tail = tail,
            };
            ApiResult<ComposeCommandData> res = await _apiClient.ComposeServiceLogsAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                string part = res.Error?.Details ?? string.Empty;
                string msg = res.Error?.Message ?? "Lỗi.";
                CommandOutput = string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg;
                StatusMessage = msg;
                return;
            }

            CommandOutput = res.Data?.Output ?? string.Empty;
            StatusMessage = "Đã tải logs service.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            CommandOutput = ex.ToString();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ComposeServiceExecAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Chọn project.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedService))
        {
            StatusMessage = "Chọn service.";
            return;
        }

        string cmd = ComposeExecCommandLine.Trim();
        if (string.IsNullOrEmpty(cmd))
        {
            StatusMessage = "Nhập lệnh (ví dụ: uname -a).";
            return;
        }

        IsBusy = true;
        CommandOutput = string.Empty;
        try
        {
            var req = new ComposeServiceExecRequest
            {
                Id = SelectedProject.Id,
                Service = SelectedService.Trim(),
                Command = cmd,
            };
            ApiResult<ComposeCommandData> res = await _apiClient.ComposeServiceExecAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                string part = res.Error?.Details ?? string.Empty;
                string msg = res.Error?.Message ?? "Lỗi.";
                CommandOutput = string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg;
                StatusMessage = msg;
                NotifyComposeFailure(msg);
                return;
            }

            CommandOutput = res.Data?.Output ?? string.Empty;
            StatusMessage = "Đã chạy exec (không TTY).";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            CommandOutput = ex.ToString();
            NotifyComposeFailure(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunComposeServiceAsync(Func<ComposeServiceRequest, Task<ApiResult<ComposeCommandData>>> call)
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Chọn project.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedService))
        {
            StatusMessage = "Chọn service.";
            return;
        }

        IsBusy = true;
        CommandOutput = string.Empty;
        try
        {
            var req = new ComposeServiceRequest
            {
                Id = SelectedProject.Id,
                Service = SelectedService.Trim(),
            };
            ApiResult<ComposeCommandData> res = await call(req).ConfigureAwait(true);
            if (!res.Success)
            {
                string part = res.Error?.Details ?? string.Empty;
                string msg = res.Error?.Message ?? "Lỗi.";
                CommandOutput = string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg;
                StatusMessage = msg;
                NotifyComposeFailure(msg);
                return;
            }

            CommandOutput = res.Data?.Output ?? string.Empty;
            StatusMessage = "Xong.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            CommandOutput = ex.ToString();
            NotifyComposeFailure(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunComposeAsync(Func<string, Task<ApiResult<ComposeCommandData>>> call)
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Chọn project.";
            return;
        }

        IsBusy = true;
        CommandOutput = string.Empty;
        try
        {
            ApiResult<ComposeCommandData> res = await call(SelectedProject.Id).ConfigureAwait(true);
            if (!res.Success)
            {
                string part = res.Error?.Details ?? string.Empty;
                string msg = res.Error?.Message ?? "Lỗi.";
                CommandOutput = string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg;
                StatusMessage = msg;
                NotifyComposeFailure(msg);
                return;
            }

            CommandOutput = res.Data?.Output ?? string.Empty;
            StatusMessage = "Xong.";
        }
        catch (Exception ex)
        {
            StatusMessage = ExceptionMessages.FormatForUser(ex);
            CommandOutput = ex.ToString();
            NotifyComposeFailure(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyComposeFailure(string message)
    {
        string body = TruncateForToast(message, ToastMessageMaxChars);
        _ = _notificationService.ShowAsync(
            "DockLite — Compose",
            body,
            NotificationDisplayKind.Warning,
            CancellationToken.None);
    }

    private static string TruncateForToast(string message, int max)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        message = message.Trim();
        return message.Length <= max ? message : message.Substring(0, max) + "…";
    }
}
