using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;

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

    public static async Task<List<Models.WorkloadInfo>> GetWorkloads(Models.BootstrapperInfo bootstrapperInfo)
    {
        var url =
            $"https://learn.microsoft.com/zh-cn/visualstudio/install/workload-component-id-{Path.GetFileNameWithoutExtension(bootstrapperInfo.DownloaderUrl).Replace("_", "-")}?view=visualstudio&viewFallbackFrom=vs-{GetVsVersion(bootstrapperInfo)}&preserve-view=true";
        var htmlWeb = new HtmlWeb();
        htmlWeb.OverrideEncoding = Encoding.UTF8;
        var doc = await htmlWeb.LoadFromWebAsync(url);
        var root = doc.DocumentNode.SelectSingleNode("//div[@data-moniker=\"visualstudio\"]");
        var headers = root.SelectNodes("h2");
        var workloads = new List<Models.WorkloadInfo>();
        foreach (var header in headers)
        {
            var title = header.InnerText;
            var next = header.NextSibling;
            while (string.IsNullOrWhiteSpace(next.InnerText))
            {
                next = next.NextSibling;
            }

            var id = next.InnerText.Replace("ID：", "").Trim();
            var workload = new Models.WorkloadInfo(id, title, bootstrapperInfo);
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
                workload.Components.Add(new Models.ComponentInfo(componentId, componentName, isRequired == "必填",
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

    public static async Task<List<Models.LanguageInfo>> GetLanguages()
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

        var languages = new List<Models.LanguageInfo>();
        var rows = next.SelectNodes("tbody/tr");
        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            languages.Add(new Models.LanguageInfo(cells[0].InnerText.Trim(), cells[1].InnerText.Trim()));
        }

        return languages;
    }

    private static string GetVsVersion(Models.BootstrapperInfo bootstrapperInfo)
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
        Models.BootstrapperInfo selectedBootstrapperInfo, List<Models.LanguageInfo> languages)
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

    public static List<Models.BootstrapperInfo> LoadBootstrapperInfos()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = $"{typeof(VSHelper).Namespace}.Resources.BootstrapperInfos";

        using var stream = assembly.GetManifestResourceStream(resourcePath)!;
        //using MemoryStream ms = new MemoryStream();
        //stream.CopyTo(ms);

        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var originContent = reader.ReadToEnd();
        return JsonSerializer.Deserialize<List<Models.BootstrapperInfo>>(originContent,
            Models.JsonGenerationContext.Default.ListBootstrapperInfo)!;
        ;
    }
}