using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaSourceGenerators;
using ReactiveUI;
using Russkyc.Messaging;
using VSDownloader.Extensions;
using VSDownloader.Models;

namespace VSDownloader.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly IObservable<bool> _canLoad;

    private readonly IObservable<bool> _canExportConfig;

    private readonly string _bootstrapperFileName =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Templates), "vs_download.exe");

    private readonly IObservable<bool> _canDoDownload;

    [UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "<Pending>")]
    public ShellViewModel()
    {
        WeakReferenceMessenger.Default.Register<ShellViewModel, ComponentInfo>(this, HandleComponentSelectedChanged);
        WeakReferenceMessenger.Default.Register<ShellViewModel, WorkloadInfo>(this, HandleWorkloadSelectedChanged);
        BootstrapperInfos = VSHelper.LoadBootstrapperInfos();

        _canLoad = this.WhenAnyValue<ShellViewModel, bool, BootstrapperInfo?>(x => x.SelectedBootstrapperInfo,
            x => x != null);
        _canExportConfig =
            this.WhenAnyValue<ShellViewModel, bool, BootstrapperInfo?>(x => x.SelectedBootstrapperInfo, x => x != null);
        _canDoDownload = this.WhenAnyValue(x => x.SelectedBootstrapperInfo, x => x.OutputFolderPath,
            (info, path) => info != null && !string.IsNullOrEmpty(path));
        SelectedBootstrapperInfo = BootstrapperInfos.FirstOrDefault();
        VSHelper.GetLanguages().ContinueWith(task => { Languages = task.Result; });
    }

    [RaiseAndSetIfChanged] public partial List<LanguageInfo> Languages { get; set; } = new();

    [RaiseAndSetIfChanged] public partial List<BootstrapperInfo> BootstrapperInfos { get; set; }

    [RaiseAndSetIfChanged] public partial BootstrapperInfo? SelectedBootstrapperInfo { get; set; }

    [RaiseAndSetIfChanged] public partial bool IsLoading { get; set; }

    [RaiseAndSetIfChanged] public partial IStorageFolder? OutputFolder { get; set; }

    [RaiseAndSetIfChanged] public partial string OutputFolderPath { get; set; } = string.Empty;

    async partial void OnOutputFolderChanged(IStorageFolder? value)
    {
        OutputFolderPath = value?.TryGetLocalPath() ?? string.Empty;
        if (value != null)
        {
            var configFile = await value.GetFileAsync("Layout.vsconfig");
            if (configFile != null)
            {
                var result = await MessageBoxViewModel.Show("在目录中找到配置文件，需要应用该配置吗？");
                if (result is true)
                {
                    await LoadFromConfigFile(configFile);
                }
            }
        }
    }

    private void HandleWorkloadSelectedChanged(ShellViewModel recipient, WorkloadInfo message)
    {
        if (message.IsSelected)
        {
            message.Components.ForEach(x =>
            {
                if (!x.IsSelected)
                {
                    x.IsSelected = x.IsRequired;
                }
            });
        }
        else
        {
            message.Components.ForEach(x =>
            {
                if (x.IsRequired)
                {
                    x.IsSelected = false;
                }
            });
        }
    }

    private void HandleComponentSelectedChanged(ShellViewModel recipient, ComponentInfo message)
    {
        if (message.IsSelected)
        {
            message.Owner.BootstrapperInfo.Workloads.SelectMany(x => x.Components).Where(x => x.Id == message.Id)
                .ToList().ForEach(x => x.IsSelected = true);
            if (message.Owner.Components.Any(x => x.IsRequired) &&
                message.Owner.Components.Where(x => x.IsRequired).All(x => x.IsSelected))
            {
                message.Owner.IsSelected = true;
            }
        }
        else
        {
            if (message.IsRequired)
            {
                message.Owner.IsSelected = false;
            }
        }
    }

    async partial void OnSelectedBootstrapperInfoChanged(BootstrapperInfo? value)
    {
        if (value != null && !value.Workloads.Any())
        {
            IsLoading = true;
            value.Workloads = await VSHelper.GetWorkloads(value);
            IsLoading = false;
        }
    }

    [ReactiveCommand(CanExecute = nameof(_canLoad))]
    private async Task LoadConfig()
    {
        var files = await this.GetTopLevel().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择导出配置",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Visual Studio导出配置文件")
                {
                    Patterns = new List<string> { "*.vsconfig" }
                },
                new FilePickerFileType("导出配置文件")
                {
                    Patterns = new List<string> { "*.d.vsconfig" }
                }
            ]
        });
        if (files.Any())
        {
            var file = files.First();
            await LoadFromConfigFile(file);
        }
    }

    private async Task LoadFromConfigFile(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var configContent = await reader.ReadToEndAsync();
        var config =
            JsonSerializer.Deserialize<LayoutConfig>(configContent, JsonGenerationContext.Default.LayoutConfig)!;
        var selectedComponents = config.Components;
        foreach (var workload in SelectedBootstrapperInfo!.Workloads.Where(workload => !workload.IsRequired))
        {
            workload.IsSelected = false;
            workload.Components.ForEach(x => x.IsSelected = false);
        }

        foreach (var component in SelectedBootstrapperInfo.Components.Where(component =>
                     selectedComponents.Contains(component.Id)))
        {
            component.IsSelected = true;
        }

        foreach (var language in Languages)
        {
            if (config.Languages.Contains(language.Id))
            {
                language.IsSelected = true;
            }
        }
    }

    [ReactiveCommand(CanExecute = nameof(_canExportConfig))]
    private async Task ExportConfig()
    {
        var file = await this.GetTopLevel().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出配置",
            FileTypeChoices = [new FilePickerFileType("") { Patterns = new List<string> { "*.d.vsconfig" } }]
        });
        if (file != null)
        {
            var data = new LayoutConfig
            {
                Version = "1.0",
                Components = SelectedBootstrapperInfo!.Components.Where(x => x.IsSelected).Select(x => x.Id)
                    .Distinct().ToList(),
                Extensions = new List<string>(),
                Languages = Languages.Where(x => x.IsSelected).Select(x => x.Id).ToList()
            };
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(JsonSerializer.Serialize(data, JsonGenerationContext.Default.LayoutConfig));
            await writer.FlushAsync();
            await MessageBoxViewModel.Show("导出成功", showCancelButton: false);
        }
    }

    [ReactiveCommand(CanExecute = nameof(_canDoDownload))]
    private async Task DoDownload()
    {
        if (File.Exists(_bootstrapperFileName))
        {
            File.Delete(_bootstrapperFileName);
        }

        await VSHelper.DownloadVsBootstrapper(SelectedBootstrapperInfo!.DownloaderUrl, _bootstrapperFileName);

        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            desktopStyleApplicationLifetime)
        {
            desktopStyleApplicationLifetime.MainWindow?.IsVisible = false;
            await VSHelper.DownloadVs(_bootstrapperFileName, OutputFolderPath, SelectedBootstrapperInfo!, Languages);
            desktopStyleApplicationLifetime.MainWindow?.IsVisible = true;
        }
    }

    [ReactiveCommand]
    private async Task SelectFolder()
    {
        var folders = await this.GetTopLevel().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择保存路径"
        });
        if (folders.Any())
        {
            OutputFolder = folders.First();
        }
    }
}