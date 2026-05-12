using Avalonia.InternalCheat;
using DialogHostAvalonia;
using SkiaSharp;

namespace VSDownloader.ViewModels;

internal partial class MessageBoxViewModel(string title, string content) : ViewModelBase
{
    public string Title { get; } = title;
    public string Content { get; } = content;

    public bool ShowCancelButton { get; set; } = true;

    public bool ShowOkButton { get; set; } = true;

    public string OkButtonText { get; set; } = "确定";

    public string CancelButtonText { get; set; } = "取消";

    [ReactiveCommand]
    private void Ok()
    {
        DialogHost.Close(null, true);
    }

    [ReactiveCommand]
    private void Cancel()
    {
        DialogHost.Close(null, false);
    }

    public static Task<object?> Show(string title, string content, bool showOkButton = true, string okButtonText = "", bool showCancelButton = true, string cancelButtonText = "")
    {
        var viewModel = new MessageBoxViewModel(title, content)
        {
            ShowOkButton = showOkButton,
            ShowCancelButton = showCancelButton
        };
        if (!string.IsNullOrWhiteSpace(okButtonText))
        {
            viewModel.OkButtonText = okButtonText;
        }
        if (!string.IsNullOrWhiteSpace(cancelButtonText))
        {
            viewModel.CancelButtonText = cancelButtonText;
        }
        return DialogHost.Show(viewModel);
    }

    public static Task<object?> Show(string content, bool showOkButton = true, string okButtonText = "", bool showCancelButton = true, string cancelButtonText = "")
    {
        return Show("提示", content, showOkButton, okButtonText, showCancelButton, cancelButtonText);
    }
}