using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.App.Services;
using DockLite.Contracts.Api;
using DockLite.Core.Compose;
using DockLite.Infrastructure.Wsl;
using Microsoft.Win32;

namespace DockLite.App.ViewModels;

/// <summary>
/// Quản lý project Docker Compose (đường dẫn Windows được service chuyển sang WSL).
/// </summary>
public partial class ComposeViewModel : ObservableObject
{
    private const int ToastMessageMaxChars = 520;

    /// <summary>
    /// Giới hạn độ dài output hiển thị (ký tự) — tránh chuỗi quá lớn trong RAM UI.
    /// </summary>
    private const int MaxCommandOutputChars = 524_288;

    private readonly IComposeScreenApi _composeApi;
    private readonly INotificationService _notificationService;
    private readonly IAppShutdownToken _shutdownToken;
    private readonly AppShellActivityState _shellActivity;

    /// <summary>
    /// Distro WSL từ cài đặt (tùy chọn), dùng trong lệnh gợi ý <c>wsl -d</c>.
    /// </summary>
    private readonly string? _wslDistribution;

    private readonly SemaphoreSlim _loadProjectsGate = new(1, 1);

    public ObservableCollection<ComposeProjectDto> Projects { get; } = new();

    /// <summary>Chưa có project Compose đã lưu (sau khi tải xong).</summary>
    public bool ShowEmptyProjectsHint => !IsBusy && Projects.Count == 0;

    private void NotifyEmptyProjectsHint()
    {
        OnPropertyChanged(nameof(ShowEmptyProjectsHint));
    }

    private void ReportComposeNetworkError(Exception ex)
    {
        StatusMessage = NetworkErrorMessageMapper.FormatForUser(ex);
        _ = ApiErrorUiFeedback.ShowNetworkExceptionToastAsync(_notificationService, ex);
    }

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

    /// <summary>
    /// Mỗi dòng một file compose tương đối (tùy chọn) khi thêm project — map sang <c>-f</c> trên service.
    /// </summary>
    [ObservableProperty]
    private string _newProjectComposeFilesText = string.Empty;

    /// <summary>
    /// Chỉnh danh sách file <c>-f</c> cho project đang chọn; nhấn Lưu để gửi PATCH.
    /// </summary>
    [ObservableProperty]
    private string _composeFilesEditorText = string.Empty;

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

    [ObservableProperty]
    private bool _canSaveComposeFiles;

    public ComposeViewModel(
        IComposeScreenApi composeApi,
        INotificationService notificationService,
        IAppShutdownToken shutdownToken,
        AppShellActivityState shellActivity,
        string? wslDistributionFromSettings)
    {
        _composeApi = composeApi;
        _notificationService = notificationService;
        _shutdownToken = shutdownToken;
        _shellActivity = shellActivity;
        _wslDistribution = string.IsNullOrWhiteSpace(wslDistributionFromSettings)
            ? null
            : wslDistributionFromSettings.Trim();
        UpdateComposeServiceUiState();
    }

    /// <summary>
    /// Một dòng lệnh WSL + bash để chạy <c>docker compose exec -it … sh</c> trong thư mục project (terminal bên ngoài).
    /// </summary>
    public string SuggestedWslTerminalComposeExecLine
    {
        get
        {
            ComposeProjectDto? p = SelectedProject;
            if (p is null || string.IsNullOrWhiteSpace(SelectedService))
            {
                return string.Empty;
            }

            string dir = p.WslPath.Trim();
            if (dir.Length == 0)
            {
                return string.Empty;
            }

            string composeArgs = ComposeComposePaths.BuildComposeFileArgsForDockerCli(p.ComposeFiles);
            string svc = SelectedService.Trim();
            string inner = $"cd {ComposeComposePaths.BashSingleQuote(dir)} && docker compose{composeArgs} exec -it {svc} sh";
            string bashC = ComposeComposePaths.BashSingleQuote(inner);
            if (string.IsNullOrWhiteSpace(_wslDistribution))
            {
                return $"wsl -- bash -c {bashC}";
            }

            return $"wsl -d {ComposeComposePaths.BashSingleQuote(_wslDistribution)} -- bash -c {bashC}";
        }
    }

    /// <summary>
    /// Cho phép nút sao chép khi đã chọn project và service.
    /// </summary>
    public bool CanCopySuggestedTerminalComposeLine => !string.IsNullOrEmpty(SuggestedWslTerminalComposeExecLine);

    /// <summary>
    /// Mở <c>wsl.exe</c> tại thư mục project (chỉ cần chọn project, không cần service).
    /// </summary>
    public bool CanOpenTerminalInProjectFolder =>
        SelectedProject is not null
        && !string.IsNullOrWhiteSpace(SelectedProject.WslPath)
        && !IsBusy;

    /// <summary>
    /// Sao chép <see cref="SuggestedWslTerminalComposeExecLine"/> vào clipboard (Windows).
    /// </summary>
    [RelayCommand]
    private void CopySuggestedTerminalComposeLine()
    {
        string t = SuggestedWslTerminalComposeExecLine;
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        Clipboard.SetText(t);
        StatusMessage = UiLanguageManager.TryLocalizeCurrent(
            "Ui_Compose_Status_CopyTerminalDone",
            "Đã sao chép lệnh docker compose (terminal WSL).");
    }

    /// <summary>
    /// Mở terminal WSL (bash đăng nhập) trong thư mục project — thay cho shell tương tác nhúng trong UI.
    /// </summary>
    [RelayCommand]
    private void OpenTerminalInProjectFolder()
    {
        if (SelectedProject is null)
        {
            return;
        }

        string dir = SelectedProject.WslPath.Trim();
        if (dir.Length == 0)
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            string inner = $"cd {ComposeComposePaths.BashSingleQuote(dir)} && exec bash -l";
            if (string.IsNullOrWhiteSpace(_wslDistribution))
            {
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("bash");
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(inner);
            }
            else
            {
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(_wslDistribution!);
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("bash");
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(inner);
            }

            Process.Start(psi);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_OpenTerminalOk",
                "Đã mở terminal WSL trong thư mục project.");
        }
        catch (Exception ex)
        {
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Compose_Status_OpenTerminalFailedFormat",
                "Không mở được WSL: {0}",
                ex.Message);
        }
    }

    private void NotifyTerminalComposeHints()
    {
        OnPropertyChanged(nameof(SuggestedWslTerminalComposeExecLine));
        OnPropertyChanged(nameof(CanCopySuggestedTerminalComposeLine));
    }

    partial void OnSelectedProjectChanged(ComposeProjectDto? value)
    {
        Services.Clear();
        SelectedService = null;
        ComposeFilesEditorText = ComposeComposePaths.FormatComposeFilesForEditor(value?.ComposeFiles);
        UpdateComposeServiceUiState();
        NotifyTerminalComposeHints();
    }

    partial void OnSelectedServiceChanged(string? value)
    {
        UpdateComposeServiceUiState();
        NotifyTerminalComposeHints();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyEmptyProjectsHint();
        UpdateComposeServiceUiState();
    }

    private void UpdateComposeServiceUiState()
    {
        CanLoadComposeServices = SelectedProject is not null && !IsBusy;
        CanComposeServiceAction = SelectedProject is not null
            && !string.IsNullOrWhiteSpace(SelectedService)
            && !IsBusy;
        CanSaveComposeFiles = SelectedProject is not null && !IsBusy;
        OnPropertyChanged(nameof(CanOpenTerminalInProjectFolder));
    }

    /// <summary>
    /// Cắt bớt chuỗi output trước khi gán vào ô hiển thị.
    /// </summary>
    private static string ClampCommandOutput(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.Length <= MaxCommandOutputChars)
        {
            return text;
        }

        const string suffix = "\n...\n[DockLite: đã cắt bớt output quá dài để giới hạn bộ nhớ UI.]";
        int take = Math.Max(0, MaxCommandOutputChars - suffix.Length);
        return text[..take] + suffix;
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        if (!_shellActivity.ShouldRefreshComposeProjectList)
        {
            return;
        }

        if (!await _loadProjectsGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                ApiResult<ComposeProjectListData> res = await _composeApi.GetComposeProjectsAsync(_shutdownToken.Token).ConfigureAwait(true);
                Projects.Clear();
                if (!res.Success)
                {
                    string err = res.Error?.Message
                        ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_InvalidResponse", "Phản hồi không hợp lệ.");
                    StatusMessage = err;
                    _ = ApiErrorUiFeedback.ShowWarningToastAsync(_notificationService, err);
                    return;
                }

                if (res.Data?.Items is not null)
                {
                    foreach (ComposeProjectDto p in res.Data.Items)
                    {
                        Projects.Add(p);
                    }
                }

                StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                    "Ui_Compose_Status_LoadedProjectsCountFormat",
                    "Đã tải {0} project.",
                    Projects.Count);
            }
            catch (Exception ex)
            {
                ReportComposeNetworkError(ex);
            }
            finally
            {
                IsBusy = false;
                NotifyEmptyProjectsHint();
            }
        }
        finally
        {
            _loadProjectsGate.Release();
        }
    }

    /// <summary>
    /// Với UNC WSL (\\wsl.localhost\ hoặc \\wsl$\), gửi thêm wslPath dạng /home/... để service không phụ thuộc parse lại từ chuỗi đã đổi dấu.
    /// </summary>
    private static ComposeProjectAddRequest CreateComposeAddRequest(string path, IReadOnlyList<string> composeFiles)
    {
        List<string>? filesArg = composeFiles.Count == 0 ? null : composeFiles.ToList();
        if (WslPathNormalizer.TryUnixPathFromWslUnc(path, expectedDistro: null, out string unixPath, out _))
        {
            return new ComposeProjectAddRequest
            {
                WindowsPath = path,
                WslPath = unixPath,
                ComposeFiles = filesArg,
            };
        }

        return new ComposeProjectAddRequest { WindowsPath = path, ComposeFiles = filesArg };
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
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_EnterProjectPath",
                "Nhập đường dẫn thư mục project (Windows hoặc /mnt/...).");
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            List<string> composeFiles = ComposeComposePaths.ParseComposeFileLines(NewProjectComposeFilesText);
            ComposeProjectAddRequest req = CreateComposeAddRequest(path, composeFiles);
            ApiResult<ComposeProjectAddData> res = await _composeApi.AddComposeProjectAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_AddFailed", "Thêm thất bại.");
                return;
            }

            NewProjectPath = string.Empty;
            NewProjectComposeFilesText = string.Empty;
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_ProjectAdded", "Đã thêm project.");
            await LoadProjectsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
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
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_SelectProjectToDelete",
                "Chọn project để xóa.");
            return;
        }

        IsBusy = true;
        try
        {
            ApiResult<EmptyApiPayload> res = await _composeApi.RemoveComposeProjectAsync(SelectedProject.Id, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_DeleteFailed", "Xóa thất bại.");
                return;
            }

            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_DeletedFromList",
                "Đã xóa khỏi danh sách.");
            await LoadProjectsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveComposeFilesAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectProject", "Chọn project.");
            return;
        }

        string id = SelectedProject.Id;
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            List<string> files = ComposeComposePaths.ParseComposeFileLines(ComposeFilesEditorText);
            var req = new ComposeProjectPatchRequest { ComposeFiles = files };
            ApiResult<ComposeProjectPatchData> res = await _composeApi
                .PatchComposeProjectAsync(id, req, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SaveComposeFailed", "Không lưu được.");
                return;
            }

            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_ComposeFilesUpdated",
                "Đã cập nhật file compose (-f).");
            await LoadProjectsAsync().ConfigureAwait(true);
            ComposeProjectDto? match = Projects.FirstOrDefault(p => p.Id == id);
            if (match is not null)
            {
                SelectedProject = match;
            }
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ComposeUpAsync()
    {
        await RunComposeAsync(id => _composeApi.ComposeUpAsync(id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposeDownAsync()
    {
        await RunComposeAsync(id => _composeApi.ComposeDownAsync(id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposePsAsync()
    {
        await RunComposeAsync(id => _composeApi.ComposePsAsync(id, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task LoadComposeServicesAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectProject", "Chọn project.");
            return;
        }

        IsBusy = true;
        CommandOutput = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            ApiResult<ComposeServiceListData> res = await _composeApi
                .ListComposeServicesAsync(SelectedProject.Id, _shutdownToken.Token)
                .ConfigureAwait(true);
            if (!res.Success)
            {
                StatusMessage = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent(
                        "Ui_Compose_Status_LoadServicesFailed",
                        "Không đọc được danh sách service.");
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

            CommandOutput = ClampCommandOutput(res.Data?.Output);
            StatusMessage = UiLanguageManager.TryLocalizeFormatCurrent(
                "Ui_Compose_Status_LoadedServicesCountFormat",
                "Đã tải {0} service.",
                Services.Count);
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ComposeServiceStartAsync()
    {
        await RunComposeServiceAsync(req => _composeApi.ComposeServiceStartAsync(req, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposeServiceStopAsync()
    {
        await RunComposeServiceAsync(req => _composeApi.ComposeServiceStopAsync(req, _shutdownToken.Token));
    }

    [RelayCommand]
    private async Task ComposeServiceLogsAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectProject", "Chọn project.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedService))
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectService", "Chọn service.");
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
            ApiResult<ComposeCommandData> res = await _composeApi.ComposeServiceLogsAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                string part = res.Error?.Details ?? string.Empty;
                string msg = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ErrorShort", "Lỗi.");
                CommandOutput = ClampCommandOutput(string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg);
                StatusMessage = msg;
                return;
            }

            CommandOutput = ClampCommandOutput(res.Data?.Output);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_ServiceLogsLoaded",
                "Đã tải logs service.");
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
            CommandOutput = ClampCommandOutput(ex.ToString());
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
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectProject", "Chọn project.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedService))
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectService", "Chọn service.");
            return;
        }

        string cmd = ComposeExecCommandLine.Trim();
        if (string.IsNullOrEmpty(cmd))
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent(
                "Ui_Compose_Status_EnterExecCommand",
                "Nhập lệnh (ví dụ: uname -a).");
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
            ApiResult<ComposeCommandData> res = await _composeApi.ComposeServiceExecAsync(req, _shutdownToken.Token).ConfigureAwait(true);
            if (!res.Success)
            {
                string part = res.Error?.Details ?? string.Empty;
                string msg = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ErrorShort", "Lỗi.");
                CommandOutput = ClampCommandOutput(string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg);
                StatusMessage = msg;
                NotifyComposeFailure(msg);
                return;
            }

            CommandOutput = ClampCommandOutput(res.Data?.Output);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_ExecRan", "Đã chạy exec (không TTY).");
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
            CommandOutput = ClampCommandOutput(ex.ToString());
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
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectProject", "Chọn project.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedService))
        {
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectService", "Chọn service.");
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
                string msg = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ErrorShort", "Lỗi.");
                CommandOutput = ClampCommandOutput(string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg);
                StatusMessage = msg;
                NotifyComposeFailure(msg);
                return;
            }

            CommandOutput = ClampCommandOutput(res.Data?.Output);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_Done", "Xong.");
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
            CommandOutput = ClampCommandOutput(ex.ToString());
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
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_SelectProject", "Chọn project.");
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
                string msg = res.Error?.Message
                    ?? UiLanguageManager.TryLocalizeCurrent("Ui_Status_Common_ErrorShort", "Lỗi.");
                CommandOutput = ClampCommandOutput(string.IsNullOrEmpty(part) ? msg : part + "\n---\n" + msg);
                StatusMessage = msg;
                NotifyComposeFailure(msg);
                return;
            }

            CommandOutput = ClampCommandOutput(res.Data?.Output);
            StatusMessage = UiLanguageManager.TryLocalizeCurrent("Ui_Compose_Status_Done", "Xong.");
        }
        catch (Exception ex)
        {
            ReportComposeNetworkError(ex);
            CommandOutput = ClampCommandOutput(ex.ToString());
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
