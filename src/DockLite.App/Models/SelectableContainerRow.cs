using CommunityToolkit.Mvvm.ComponentModel;
using DockLite.Contracts.Api;

namespace DockLite.App.Models;

/// <summary>
/// Một dòng container trong grid kèm ô chọn cho thao tác hàng loạt.
/// </summary>
public partial class SelectableContainerRow : ObservableObject
{
    public ContainerSummaryDto Model { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectableContainerRow(ContainerSummaryDto model)
    {
        Model = model;
    }
}
