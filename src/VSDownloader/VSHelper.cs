using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.InternalCheat;
using HtmlAgilityPack;
using ReactiveUI;
using Russkyc.Messaging;

namespace VSDownloader;

// ReSharper disable once InconsistentNaming
internal class VSHelper
{
    public static async Task DownloadVsBootstrapper(string url, string outputFile)
    {
        using var client = new HttpClient();
        var response = await client.GetStreamAsync(url);
        await using var fileStream = new FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None,
            4 * 1024, true);
        await response.CopyToAsync(fileStream);
    }

    public static async Task<List<WorkloadInfo>> GetWorkloads(BootstrapperInfo bootstrapperInfo)
    {
        var url =
            $"https://learn.microsoft.com/zh-cn/visualstudio/install/workload-component-id-{Path.GetFileNameWithoutExtension(bootstrapperInfo.DownloaderUrl).Replace("_", "-")}?view=visualstudio&viewFallbackFrom=vs-{GetVsVersion(bootstrapperInfo)}&preserve-view=true";
        var htmlWeb = new HtmlWeb();
        htmlWeb.OverrideEncoding = Encoding.UTF8;
        var doc = await htmlWeb.LoadFromWebAsync(url);
        var root = doc.DocumentNode.SelectSingleNode("//div[@data-moniker=\"visualstudio\"]");
        var headers = root.SelectNodes("h2");
        var workloads = new List<WorkloadInfo>();
        foreach (var header in headers)
        {
            var title = header.InnerText;
            var next = header.NextSibling;
            while (string.IsNullOrWhiteSpace(next.InnerText))
            {
                next = next.NextSibling;
            }

            var id = next.InnerText.Replace("ID：", "").Trim();
            var workload = new WorkloadInfo(id, title, bootstrapperInfo);
            while (next.Name != "table")
            {
                next = next.NextSibling;
            }

            var rows = next.SelectNodes("tbody/tr");
            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                var componentId = cells[0].InnerText.Trim();
                var componentName = cells[1].InnerText.Trim();
                var isRequired = cells.Count < 4 ? "自选" : cells[3].InnerText.Trim();
                workload.Components.Add(new ComponentInfo(componentId, componentName, isRequired == "必填",
                    isRequired == "推荐", workload));
            }

            if (workload.IsRequired)
            {
                workload.IsSelected = workload.IsRequired;
                if (workload.IsSelected)
                {
                    workload.Components.ForEach(x =>
                    {
                        x.IsSelected = true;
                        x.IsEnabled = false;
                    });
                }
            }

            workloads.Add(workload);
        }

        return workloads;
    }

    public static async Task<List<LanguageInfo>> GetLanguages()
    {
        var web = new HtmlWeb();
        web.OverrideEncoding = Encoding.UTF8;
        var doc = await web.LoadFromWebAsync(
            "https://learn.microsoft.com/zh-cn/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=visualstudio#list-of-language-locales");
        var node = doc.GetElementbyId("list-of-language-locales");
        var next = node.NextSibling;
        while (next.Name != "table")
        {
            next = next.NextSibling;
        }

        var languages = new List<LanguageInfo>();
        var rows = next.SelectNodes("tbody/tr");
        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            languages.Add(new LanguageInfo(cells[0].InnerText.Trim(), cells[1].InnerText.Trim()));
        }

        return languages;
    }

    private static string GetVsVersion(BootstrapperInfo bootstrapperInfo)
    {
        if (bootstrapperInfo.DownloaderUrl.Contains("/18/"))
        {
            return "2026";
        }

        if (bootstrapperInfo.DownloaderUrl.Contains("/17/"))
        {
            return "2022";
        }

        if (bootstrapperInfo.DownloaderUrl.Contains("/16/"))
        {
            return "2019";
        }

        throw new NotSupportedException("不支持的 Visual Studio 版本");
    }

    public static async Task DownloadVs(string bootstrapperFileName, string outputFolderPath,
        BootstrapperInfo selectedBootstrapperInfo, List<LanguageInfo> languages)
    {
        var builder = new StringBuilder();
        builder.Append($" --layout \"{outputFolderPath}\" --includeRecommended");

        foreach (var workload in selectedBootstrapperInfo.Workloads.Where(workload => workload.IsSelected)
                     .Select(x => x.Id))
        {
            builder.Append($" --add {workload}");
        }

        foreach (var component in selectedBootstrapperInfo.Components.Where(x => !x.IsRequired && x.IsSelected)
                     .Select(x => x.Id).Distinct())
        {
            builder.Append($" --add {component}");
        }

        foreach (var language in languages)
        {
            if (language.IsSelected)
            {
                builder.Append($" --lang {language.Id}");
            }
        }

        var processStartInfo = new ProcessStartInfo(bootstrapperFileName)
        {
            Arguments = builder.ToString(),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        await process!.WaitForExitAsync();
        await CleanOldLayout(bootstrapperFileName, outputFolderPath);
    }

    public static async Task CleanOldLayout(string bootstrapperFileName, string outputFolderPath)
    {
        var dir = Path.Combine(outputFolderPath, "Archive");
        if (Directory.Exists(dir))
        {
            var catalogFiles = new DirectoryInfo(dir).GetFiles("Catalog.json", SearchOption.AllDirectories)
                .OrderBy(x => x.CreationTime).ToList();
            foreach (var catalogFile in catalogFiles)
            {
                var processStartInfo = new ProcessStartInfo(bootstrapperFileName)
                {
                    Arguments = $"--layout {outputFolderPath} --clean {catalogFile.FullName} --passive",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(processStartInfo);
                await process!.WaitForExitAsync();
            }

            Directory.Delete(dir, true);
        }
    }

    public static List<BootstrapperInfo> LoadBootstrapperInfos()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = $"{typeof(VSHelper).Namespace}.Resources.BootstrapperInfos";

        using var stream = assembly.GetManifestResourceStream(resourcePath)!;
        //using MemoryStream ms = new MemoryStream();
        //stream.CopyTo(ms);

        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var originContent = reader.ReadToEnd();
        return JsonSerializer.Deserialize<List<BootstrapperInfo>>(originContent,
            JsonGenerationContext.Default.ListBootstrapperInfo)!;
        ;
    }
}

public partial class BootstrapperInfo(string title, string downloaderUrl) : ReactiveObject
{
    public string Title { get; } = title;
    public string DownloaderUrl { get; } = downloaderUrl;

    [ObservableProperty] public partial List<WorkloadInfo> Workloads { get; set; } = new();

    public List<ComponentInfo> Components => Workloads.SelectMany(x => x.Components).ToList();
}

public partial class WorkloadInfo(string id, string title, BootstrapperInfo bootstrapperInfo) : ReactiveObject
{
    public List<ComponentInfo> Components { get; } = new();
    public string Id { get; } = id;
    public string Title { get; } = title;

    public bool IsRequired => Components.All(x => x.IsRequired);

    public bool IsEnabled => !IsRequired;

    [ObservableProperty] public partial bool IsSelected { get; set; } = false;
    public BootstrapperInfo BootstrapperInfo { get; } = bootstrapperInfo;

    partial void OnIsSelectedChanged()
    {
        WeakReferenceMessenger.Default.Send(this);
    }
}

public partial class ComponentInfo(string id, string title, bool isRequired, bool isSuggester, WorkloadInfo owner)
    : ReactiveObject
{
    public string Id { get; init; } = id;
    public string Title { get; init; } = title;
    public bool IsRequired { get; init; } = isRequired;

    public bool IsEnabled { get; set; } = true;
    public bool IsSuggester { get; set; } = isSuggester;
    public WorkloadInfo Owner { get; } = owner;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged()
    {
        WeakReferenceMessenger.Default.Send(this);
    }
}

public partial class LanguageInfo(string id, string title) : ReactiveObject
{
    public string Id { get; } = id;
    public string Title { get; } = title;

    [ObservableProperty] public partial bool IsSelected { get; set; } = false;
}

[JsonSerializable(typeof(List<BootstrapperInfo>))]
[JsonSerializable(typeof(LanguageInfo))]
[JsonSerializable(typeof(LayoutConfig))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
internal partial class JsonGenerationContext : JsonSerializerContext
{
}

public class LayoutConfig
{
    public string? Version { get; set; }
    public List<string> Components { get; set; } = new();
    public List<string> Extensions { get; set; } = new();
    public List<string> Languages { get; set; } = new();
}