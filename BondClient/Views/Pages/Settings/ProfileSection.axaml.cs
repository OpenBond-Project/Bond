using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Bond.Views;

namespace Bond.Views.Pages.Settings;

/// <summary>「我的」页个人资料卡：头像 / 昵称 / 会员徽标 / 邮箱；task-155 头像上传入口。</summary>
public partial class ProfileSection : Avalonia.Controls.UserControl
{
    public ProfileSection() => InitializeComponent();

    private void OnAvatarPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        => AvatarOverlay.Opacity = 1;

    private void OnAvatarPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        => AvatarOverlay.Opacity = 0;

    /// <summary>task-155：选图 → 裁切 → 交给 ViewModel 上传（前端先挡扩展名/2MB，失败展示后端原文）。</summary>
    private async void OnChangeAvatarClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Bond.ViewModels.SettingsPageViewModel vm) return;
        if (!vm.CanEditAvatar) return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;

        try
        {
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择头像图片",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("图片（PNG / JPEG / WebP）")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
                        MimeTypes = ["image/png", "image/jpeg", "image/webp"],
                    },
                ],
            });

            if (files.Count == 0) return;

            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                // 非本地文件（如云盘虚拟项）：当前实现只接受本地路径。
                return;
            }

            // 先裁切再上传：裁切窗口输出 512×512 PNG，避免把整张原图传到服务端。
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null)
            {
                await vm.UploadAvatarAsync(path!);
                return;
            }

            var crop = new AvatarCropWindow(path!);
            var confirmed = await crop.ShowDialog<bool>(owner);
            if (!confirmed || string.IsNullOrEmpty(crop.CroppedPath))
                return;

            await vm.UploadAvatarAsync(crop.CroppedPath);
        }
        catch (Exception ex)
        {
            // 选择器本身失败（权限/平台不支持）：给出可读提示，不静默。
            vm.ReportAvatarPickerFailed(ex.Message);
        }
    }
}
