using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Bond.ViewModels;

namespace Bond.Views.Pages.Settings;

/// <summary>task-142：设置页「挂载与网络」分组视图。</summary>
public partial class NetworkSection : UserControl
{
    public NetworkSection()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => TryLoad();
        Loaded += (_, _) => TryLoad();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 首次进入设置页时 DataContext 可能还没继承下来，等布局完成再拉一次。
        Dispatcher.UIThread.Post(TryLoad, DispatcherPriority.Loaded);
    }

    private void TryLoad()
    {
        if (DataContext is SettingsPageViewModel vm)
            _ = vm.LoadDavSectionAsync();
    }
}
