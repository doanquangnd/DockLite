using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLite.Contracts.Api;
using DockLite.Core;
using DockLite.Core.Services;

namespace DockLite.App.ViewModels;

/// <summary>
/// Quản lý project Docker Compose (đường dẫn Windows được service chuyển sang WSL).
/// </summary>
public partial class ComposeViewModel : ObservableObject
{
    private readonly IDockLiteApiClient _apiClient;

    public ComposeViewModel(IDockLiteApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<ComposeProjectDto> Projects { get; } = new();

    [ObservableProperty]
    private ComposeProjectDto? _selectedProject;

    [ObservableProperty]
    private string _newProjectPath = string.Empty;

    [ObservableProperty]
    private string _commandOutput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            ComposeProjectListApiResponse? res = await _apiClient.GetComposeProjectsAsync().ConfigureAwait(true);
            Projects.Clear();
            if (res?.Items is not null)
            {
                foreach (ComposeProjectDto p in res.Items)
                {
                    Projects.Add(p);
                }
            }

            StatusMessage = res?.Ok == true ? $"Đã tải {Projects.Count} project." : "Phản hồi không hợp lệ.";
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
            var req = new ComposeProjectAddRequest { WindowsPath = path };
            ComposeProjectAddApiResponse? res = await _apiClient.AddComposeProjectAsync(req).ConfigureAwait(true);
            if (res is null)
            {
                StatusMessage = "Không có phản hồi.";
                return;
            }

            if (!res.Ok)
            {
                StatusMessage = res.Error ?? "Thêm thất bại.";
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
            ApiActionResponse? res = await _apiClient.RemoveComposeProjectAsync(SelectedProject.Id).ConfigureAwait(true);
            if (res?.Ok != true)
            {
                StatusMessage = res?.Error ?? "Xóa thất bại.";
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
        await RunComposeAsync(id => _apiClient.ComposeUpAsync(id));
    }

    [RelayCommand]
    private async Task ComposeDownAsync()
    {
        await RunComposeAsync(id => _apiClient.ComposeDownAsync(id));
    }

    [RelayCommand]
    private async Task ComposePsAsync()
    {
        await RunComposeAsync(id => _apiClient.ComposePsAsync(id));
    }

    private async Task RunComposeAsync(Func<string, Task<ComposeCommandResponse?>> call)
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
            ComposeCommandResponse? res = await call(SelectedProject.Id).ConfigureAwait(true);
            if (res is null)
            {
                CommandOutput = "(không có phản hồi)";
                return;
            }

            CommandOutput = res.Output ?? string.Empty;
            if (!string.IsNullOrEmpty(res.Error))
            {
                CommandOutput = string.IsNullOrEmpty(CommandOutput) ? res.Error : CommandOutput + "\n---\n" + res.Error;
            }

            StatusMessage = res.Ok ? "Xong." : (res.Error ?? "Lỗi.");
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
}
