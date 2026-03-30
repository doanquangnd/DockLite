namespace DockLite.App;

/// <summary>
/// Tạo shell ViewModel và session một lần (dễ thay bằng fake khi test).
/// </summary>
public interface IAppShellFactory
{
    ShellCompositionResult Create(string appBaseDirectory);
}
