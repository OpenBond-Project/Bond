using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Bond.ViewModels;

namespace Bond.Views.Pages;

/// <summary>
/// 「我的」页：个人资料、会员/存储概览与常用入口。
/// <para>资料编辑复用 <see cref="Settings.ProfileSection"/> 与 <see cref="SettingsPageViewModel"/>，
/// 避免设置页与「我的」页各维护一套头像/昵称逻辑。</para>
/// </summary>
public partial class MyPage : UserControl
{
    private bool _loaded;

    public MyPage()
    {
        InitializeComponent();

        // DataContext 由 MainWindow 的 DataContext 绑定注入，AttachedToVisualTree 时可能还是 null；
        // 三个入口都尝试一次，保证首次进入「我的」就能拿到昵称/邮箱/会员信息。
        AttachedToVisualTree += (_, _) => TryLoad();
        DataContextChanged += (_, _) => TryLoad();
        Loaded += (_, _) => TryLoad();
    }

    private void TryLoad()
    {
        if (_loaded || DataContext is not SettingsPageViewModel vm) return;
        _loaded = true;

        vm.InitProfileUi();
        _ = vm.RefreshProfileAsync();
        _ = vm.RefreshEmailAsync();
        _ = vm.LoadRedeemSectionAsync();
    }

    private void OnOpenAccountSettings(object? sender, RoutedEventArgs e) => OpenSettings("account");

    private void OnOpenRedeemSettings(object? sender, RoutedEventArgs e) => OpenSettings("redeem");

    private void OnOpenAppearanceSettings(object? sender, RoutedEventArgs e) => OpenSettings("appearance");

    private void OnOpenAboutSettings(object? sender, RoutedEventArgs e) => OpenSettings("about");

    private void OpenSettings(string section)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window)
            window.OpenSettingsSection(section);
    }

    private async void OnCopyDeviceCodeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm) return;

        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            vm.ReportDeviceCodeCopied(false);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(vm.DeviceCode ?? string.Empty);
            vm.ReportDeviceCodeCopied(true);
        }
        catch (Exception)
        {
            vm.ReportDeviceCodeCopied(false);
        }
    }
}
