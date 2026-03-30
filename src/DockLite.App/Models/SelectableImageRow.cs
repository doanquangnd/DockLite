using CommunityToolkit.Mvvm.ComponentModel;
using DockLite.Contracts.Api;

namespace DockLite.App.Models;

/// <summary>
/// Một dòng image trong grid kèm ô chọn cho xóa hàng loạt.
/// </summary>
public partial class SelectableImageRow : ObservableObject
{
    public ImageSummaryDto Model { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectableImageRow(ImageSummaryDto model)
    {
        Model = model;
    }
}
