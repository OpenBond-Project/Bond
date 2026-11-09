using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bond.ViewModels;

/// <summary>
/// task-153：设置页「个人资料」与「账户与安全 · 修改密码」，外加「界面缩放」选项。
/// <para>写操作全部有 loading / 成功 / 失败三态；失败直接展示后端 message 原文，不静默。</para>
/// </summary>
public partial class SettingsPageViewModel
{
    // ── 个人资料 ─────────────────────────────────────────────
    [ObservableProperty]
    private string _profileNickname = "";

    [ObservableProperty]
    private bool _isSavingProfile;

    /// <summary>昵称默认只读展示，点旁边的编辑图标才进入输入态。</summary>
    [ObservableProperty]
    private bool _isNicknameEditing;

    [ObservableProperty]
    private string _profileHint = "";

    [ObservableProperty]
    private bool _profileHintIsError;

    public bool HasProfileHint => !string.IsNullOrEmpty(ProfileHint);

    public string SaveProfileButtonText => IsSavingProfile ? "保存中…" : "保存昵称";

    public bool CanSaveProfile => !IsSavingProfile && !string.IsNullOrWhiteSpace(ProfileNickname);

    partial void OnIsSavingProfileChanged(bool value)
    {
        OnPropertyChanged(nameof(SaveProfileButtonText));
        OnPropertyChanged(nameof(CanSaveProfile));
        SaveProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnProfileNicknameChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveProfile));
        SaveProfileCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void BeginEditNickname()
    {
        ProfileNickname = AccountName;
        IsNicknameEditing = true;
        SetProfileHint("", isError: false);
    }

    [RelayCommand]
    private void CancelEditNickname()
    {
        ProfileNickname = AccountName;
        IsNicknameEditing = false;
        SetProfileHint("", isError: false);
    }

    /// <summary>头像位图（由 store 下载后提供）；为空时界面用首字母占位。</summary>
    public Avalonia.Media.Imaging.Bitmap? AvatarSource => _session.AvatarSource as Avalonia.Media.Imaging.Bitmap;

    public bool HasAvatar => AvatarSource is not null;

    /// <summary>从 /api/auth/me 读取昵称/邮箱等资料（设置页打开时调用）。</summary>
    public async Task RefreshProfileAsync()
    {
        try
        {
            var me = await _api.GetAsync<Bond.Services.Api.UserDto>("/api/auth/me");
            if (me?.IsOk != true || me.Data is null) return;

            var nickname = string.IsNullOrWhiteSpace(me.Data.Nickname)
                ? me.Data.Username ?? ""
                : me.Data.Nickname!;
            ProfileNickname = nickname;
            OnPropertyChanged(nameof(AccountName));

            if (string.IsNullOrWhiteSpace(EmailDisplay) || EmailDisplay == "—")
                EmailDisplay = string.IsNullOrWhiteSpace(me.Data.Email) ? "未设置" : me.Data.Email!;

            // 头像由 store 异步下载，资料刷新完成后再通知一次，避免首屏停在首字母占位。
            OnPropertyChanged(nameof(AvatarSource));
            OnPropertyChanged(nameof(HasAvatar));
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Bond] 读取个人资料失败：" + ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private async Task SaveProfileAsync()
    {
        var nickname = (ProfileNickname ?? "").Trim();
        if (nickname.Length == 0)
        {
            SetProfileHint("昵称不能为空", isError: true);
            return;
        }

        IsSavingProfile = true;
        SetProfileHint("", isError: false);
        try
        {
            var result = await _api.UpdateProfileAsync(nickname, null);
            if (result is null)
            {
                SetProfileHint("无法连接服务器，请检查网络后重试", isError: true);
                return;
            }

            if (!result.IsOk)
            {
                SetProfileHint(DescribeAuthError(result, "修改昵称失败"), isError: true);
                return;
            }

            var saved = string.IsNullOrWhiteSpace(result.Data?.Nickname)
                ? nickname
                : result.Data!.Nickname!;

            if (_session is Bond.Services.HttpBondStore store)
                store.ApplyProfile(saved, result.Data?.AvatarUrl);

            OnPropertyChanged(nameof(AccountName));
            OnPropertyChanged(nameof(AccountInitial));
            IsNicknameEditing = false;
            SetProfileHint("昵称已保存", isError: false);
            _notify.Notify("昵称已更新", Bond.Services.NoticeKind.Success);
        }
        catch (Exception ex)
        {
            SetProfileHint("修改昵称失败：" + ex.Message, isError: true);
        }
        finally
        {
            IsSavingProfile = false;
        }
    }

    private void SetProfileHint(string text, bool isError)
    {
        ProfileHintIsError = isError;
        ProfileHint = text;
        OnPropertyChanged(nameof(HasProfileHint));
    }

    /// <summary>task-155：文件选择器本身失败时的提示（平台不支持 / 权限问题）。</summary>
    public void ReportAvatarPickerFailed(string message)
    {
        AvatarRetryVisible = false;
        SetAvatarHint("无法打开图片选择器：" + message, isError: true);
    }

    public void ReportDeviceCodeCopied(bool ok)
        => _notify.Notify(ok ? "设备码已复制" : "复制设备码失败", ok ? Bond.Services.NoticeKind.Success : Bond.Services.NoticeKind.Warning);

    // ── 账户与安全：修改密码 ─────────────────────────────────
    [ObservableProperty]
    private string _passwordOld = "";

    [ObservableProperty]
    private string _passwordNew = "";

    [ObservableProperty]
    private string _passwordConfirm = "";

    [ObservableProperty]
    private bool _isChangingPassword;

    [ObservableProperty]
    private string _passwordHint = "";

    [ObservableProperty]
    private bool _passwordHintIsError;

    public bool HasPasswordHint => !string.IsNullOrEmpty(PasswordHint);

    public string ChangePasswordButtonText => IsChangingPassword ? "修改中…" : "修改密码";

    public bool CanChangePassword
        => !IsChangingPassword
           && !string.IsNullOrEmpty(PasswordOld)
           && !string.IsNullOrWhiteSpace(PasswordNew);

    partial void OnIsChangingPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(ChangePasswordButtonText));
        OnPropertyChanged(nameof(CanChangePassword));
        ChangePasswordCommand.NotifyCanExecuteChanged();
    }

    partial void OnPasswordOldChanged(string value) => RefreshPasswordCommandState();
    partial void OnPasswordNewChanged(string value) => RefreshPasswordCommandState();
    partial void OnPasswordConfirmChanged(string value) => RefreshPasswordCommandState();

    private void RefreshPasswordCommandState()
    {
        OnPropertyChanged(nameof(CanChangePassword));
        ChangePasswordCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePasswordAsync()
    {
        var oldPassword = PasswordOld ?? "";
        var newPassword = PasswordNew ?? "";
        var confirm = PasswordConfirm ?? "";

        if (string.IsNullOrEmpty(oldPassword))
        {
            SetPasswordHint("请输入当前密码", isError: true);
            return;
        }

        if (newPassword.Length < 6)
        {
            SetPasswordHint("新密码长度不能少于 6 位", isError: true);
            return;
        }

        if (!string.Equals(newPassword, confirm, StringComparison.Ordinal))
        {
            SetPasswordHint("两次输入的新密码不一致", isError: true);
            return;
        }

        IsChangingPassword = true;
        SetPasswordHint("", isError: false);
        try
        {
            var result = await _api.ChangePasswordAsync(oldPassword, newPassword);
            if (result is null)
            {
                SetPasswordHint("无法连接服务器，请检查网络后重试", isError: true);
                return;
            }

            if (!result.IsOk)
            {
                SetPasswordHint(DescribeAuthError(result, "修改密码失败"), isError: true);
                return;
            }

            // 后端不返回新 token，也不强制下线；这里只清空输入并给成功提示。
            PasswordOld = "";
            PasswordNew = "";
            PasswordConfirm = "";
            SetPasswordHint("密码已修改，下次登录请使用新密码", isError: false);
            _notify.Notify("密码已修改", Bond.Services.NoticeKind.Success);
        }
        catch (Exception ex)
        {
            SetPasswordHint("修改密码失败：" + ex.Message, isError: true);
        }
        finally
        {
            IsChangingPassword = false;
        }
    }

    private void SetPasswordHint(string text, bool isError)
    {
        PasswordHintIsError = isError;
        PasswordHint = text;
        OnPropertyChanged(nameof(HasPasswordHint));
    }

    /// <summary>后端 message 原文优先；网络层错误已在上层判空。</summary>
    private static string DescribeAuthError(Bond.Services.Api.ApiResult result, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(result.Message) && result.Message != "success")
            return result.Message;

        return result.Code switch
        {
            400 => fallback + "：请求参数不正确",
            401 => "登录状态已失效，请重新登录",
            403 => "没有权限执行该操作",
            404 => "服务端尚未开放该接口",
            429 => "操作太频繁，请稍后再试",
            _ => fallback + $"（HTTP {result.Code}）",
        };
    }

    // ── 头像上传 / 移除（task-155）──────────────────────────
    [ObservableProperty]
    private bool _isUploadingAvatar;

    [ObservableProperty]
    private string _avatarHint = "";

    [ObservableProperty]
    private bool _avatarHintIsError;

    [ObservableProperty]
    private bool _avatarRetryVisible;

    /// <summary>上一次头像操作的对象（本地文件路径），用于失败后的「重试」。</summary>
    private string? _lastAvatarPath;

    public bool HasAvatarHint => !string.IsNullOrEmpty(AvatarHint);

    public bool CanEditAvatar => !IsUploadingAvatar;

    public string AvatarButtonText => IsUploadingAvatar ? "上传中…" : "更换头像";

    partial void OnIsUploadingAvatarChanged(bool value)
    {
        OnPropertyChanged(nameof(AvatarButtonText));
        OnPropertyChanged(nameof(CanEditAvatar));
    }

    private static string? ResolveAvatarContentType(string localPath)
        => System.IO.Path.GetExtension(localPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => null,
        };

    private void SetAvatarHint(string text, bool isError)
    {
        AvatarHintIsError = isError;
        AvatarHint = text;
        OnPropertyChanged(nameof(HasAvatarHint));
    }

    /// <summary>上传并立即刷新头像（不整页刷新）。前端先挡扩展名与 2MB，失败展示后端 message 原文。</summary>
    public async Task UploadAvatarAsync(string localPath)
    {
        if (IsUploadingAvatar) return;

        var contentType = ResolveAvatarContentType(localPath);
        if (contentType is null)
        {
            AvatarRetryVisible = false;
            SetAvatarHint("只支持 PNG / JPEG / WebP 图片", isError: true);
            return;
        }

        long size;
        try
        {
            size = new System.IO.FileInfo(localPath).Length;
        }
        catch (Exception ex)
        {
            AvatarRetryVisible = false;
            SetAvatarHint("读取图片失败：" + ex.Message, isError: true);
            return;
        }

        if (size > 2 * 1024 * 1024)
        {
            AvatarRetryVisible = false;
            SetAvatarHint($"图片不能超过 2 MB（当前 {size / 1024.0 / 1024.0:0.0} MB）", isError: true);
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await System.IO.File.ReadAllBytesAsync(localPath);
        }
        catch (Exception ex)
        {
            AvatarRetryVisible = false;
            SetAvatarHint("读取图片失败：" + ex.Message, isError: true);
            return;
        }

        IsUploadingAvatar = true;
        AvatarRetryVisible = false;
        _lastAvatarPath = localPath;
        SetAvatarHint("正在上传头像…", isError: false);

        try
        {
            var result = await _api.UploadAvatarAsync(bytes, System.IO.Path.GetFileName(localPath), contentType);
            if (result is null)
            {
                // 网络层失败：RetryPolicy.UserAction —— 不自动重试，给用户「重试」。
                SetAvatarHint("无法连接服务器，请检查网络后重试", isError: true);
                AvatarRetryVisible = true;
                return;
            }

            if (!result.IsOk)
            {
                // 400 / 413 等：后端 message 原文直接给用户。
                SetAvatarHint(DescribeAuthError(result, "上传头像失败"), isError: true);
                AvatarRetryVisible = false;
                return;
            }

            if (_session is Bond.Services.HttpBondStore store)
                await store.RefreshAvatarAsync(result.Data?.AvatarUrl);

            RefreshAvatarBindings();
            RefreshTopBarSession();
            SetAvatarHint("头像已更新", isError: false);
            _notify.Notify("头像已更新", Bond.Services.NoticeKind.Success);
        }
        catch (Exception ex)
        {
            SetAvatarHint("上传头像失败：" + ex.Message, isError: true);
            AvatarRetryVisible = true;
        }
        finally
        {
            IsUploadingAvatar = false;
        }
    }

    /// <summary>失败后的「重试」：重跑上一次上传（不自动重试，符合 RetryPolicy.UserAction）。</summary>
    [RelayCommand]
    private async Task RetryAvatarAsync()
    {
        AvatarRetryVisible = false;
        if (!string.IsNullOrEmpty(_lastAvatarPath))
            await UploadAvatarAsync(_lastAvatarPath!);
    }

    private void RefreshAvatarBindings()
    {
        OnPropertyChanged(nameof(AvatarSource));
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(AccountInitial));
        OnPropertyChanged(nameof(AccountName));
    }

    /// <summary>顶栏右上角迷你头像绑的是 MainWindowViewModel → 那里主动刷新一次。</summary>
    private static void RefreshTopBarSession()
    {
        if (Bond.App.Services?.GetService(typeof(MainWindowViewModel)) is MainWindowViewModel shell)
            shell.RefreshSession();
    }

    // ── 界面缩放 ─────────────────────────────────────────────
    /// <summary>0=跟随系统、1=100%、2=110%、3=125%、4=150%。</summary>
    [ObservableProperty]
    private int _uiScaleIndex;

    public IReadOnlyList<string> UiScaleOptions { get; } = ["跟随系统", "100%", "110%", "125%", "150%"];

    public string UiScaleHint => UiScaleIndex switch
    {
        1 => "按 100% 渲染（窗口更紧凑）",
        2 => "按 110% 渲染",
        3 => "按 125% 渲染",
        4 => "按 150% 渲染（内容变大，可用滚动查看）",
        _ => "跟随 Windows 的显示缩放",
    };

    partial void OnUiScaleIndexChanged(int value)
    {
        var stored = value switch { 1 => "1", 2 => "1.1", 3 => "1.25", 4 => "1.5", _ => "auto" };
        _prefs.UiScale = stored;
        _prefs.Save();
        OnPropertyChanged(nameof(UiScaleHint));
        Bond.App.ApplyUiScale(Bond.App.ResolveUiScale(stored));
    }

    /// <summary>初始化控件状态（设置页打开时调用）。</summary>
    public void InitProfileUi()
    {
        UiScaleIndex = Bond.App.ParseUiScaleIndex(_prefs.UiScale);
        ProfileNickname = _session.AccountName;
        IsNicknameEditing = false;
        OnPropertyChanged(nameof(AvatarSource));
        OnPropertyChanged(nameof(HasAvatar));
    }
}
