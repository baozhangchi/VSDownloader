#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using VSDownloader.Models;
using VSDownloader.Properties;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using ListBox = System.Windows.Controls.ListBox;
using Screen = Stylet.Screen;

#endregion

namespace VSDownloader.ViewModels
{
    internal class ShellViewModel : Screen
    {
        private readonly Regex _regex;
        private readonly IWindowManager _windowManager;
        private ListBox _languagesSelector;
        private string _layoutFile;
        private string _version;

        #region Constructors

        public ShellViewModel(IWindowManager windowManager)
        {
            _windowManager = windowManager;
            DisplayName = "VS 离线下载工具";
#if NET6_0
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;

#else
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3 | (SecurityProtocolType)3072;
#endif
            _regex = new Regex(@"(?<version>vs-\d+)");
            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSDownloader");
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            var configFile = Path.Combine(appDataFolder, "vs.json");
            if (!File.Exists(configFile))
            {
                File.WriteAllBytes(configFile, Resources.vs);
            }

            Languages = JsonConvert.DeserializeObject<List<LanguageItem>>(Encoding.UTF8.GetString(Resources.language));
            VSItems = JsonConvert.DeserializeObject<IList<VSItem>>(File.ReadAllText(configFile));
        }

        #endregion

        #region Properties

        public List<LanguageItem> Languages { get; set; }

        public IList<VSItem> VSItems { get; set; }
        public VSItem SelectedItem { get; set; }

        public bool CanLoadComponentsAsync => SelectedItem != null;

        public ObservableCollection<ComponentItem> Components { get; set; } = new ObservableCollection<ComponentItem>();

        public string DownloadFolder { get; set; }

        public string ArchiveFolder => string.IsNullOrWhiteSpace(DownloadFolder)
            ? string.Empty
            : Path.Combine(DownloadFolder, "Archive");

        public bool CanDownload =>
            SelectedItem != null && !string.IsNullOrWhiteSpace(DownloadFolder) && Components.Count > 0;

        public bool CanUpdate
        {
            get
            {
                if (SelectedItem == null)
                {
                    return false;
                }

                if (Components.Count == 0)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(DownloadFolder))
                {
                    return false;
                }

                if (!Directory.Exists(DownloadFolder))
                {
                    return false;
                }

                if (!File.Exists(Path.Combine(DownloadFolder, "Catalog.json")))
                {
                    return false;
                }

                return true;
            }
        }

        public bool CanClean
        {
            get
            {
                if (SelectedItem == null)
                {
                    return false;
                }

                if (Components.Count == 0)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(DownloadFolder))
                {
                    return false;
                }

                if (!Directory.Exists(DownloadFolder))
                {
                    return false;
                }

                if (!File.Exists(Path.Combine(DownloadFolder, "Catalog.json")))
                {
                    return false;
                }

                if (!Directory.Exists(ArchiveFolder))
                {
                    return false;
                }

                if (Directory.GetDirectories(ArchiveFolder).Length == 0)
                {
                    return false;
                }

                if (Directory.GetFiles(ArchiveFolder, "Catalog.json", SearchOption.AllDirectories).Length == 0)
                {
                    return false;
                }

                return true;
            }
        }

        public bool IsBusy { get; set; }
        public string BusyContent { get; set; }

        #endregion

        #region Methods

        public void LanguagesSelectorLoaded(object sender, EventArgs args)
        {
            _languagesSelector = sender as ListBox;
        }

        public async void LoadComponentsAsync()
        {
            BusyContent = "正在加载……";
            IsBusy = true;
            var htmlContent = await HttpHelper.GetStringAsync(SelectedItem.DetailUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            Components.Clear();
            await Task.Run(() =>
            {
                AnalysisCnComponents(doc);
            }).ContinueWith
            (
                _ =>
                {
                    Execute.OnUIThreadAsync
                    (
                        () =>
                        {
                            IsBusy = false;
                            NotifyOfPropertyChange(nameof(CanClean));
                            NotifyOfPropertyChange(nameof(CanDownload));
                            NotifyOfPropertyChange(nameof(CanUpdate));
                            BusyContent = null;
                        }
                    );
                }
            );
        }

        public void SelectDownloadFolder()
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DownloadFolder = dialog.SelectedPath;
                RemoveCleanedFiles();

                _layoutFile = Path.Combine(DownloadFolder, "Layout.json");
                if (File.Exists(_layoutFile))
                {
                    SetDefaultSelectedComponents();
                }
            }
        }

        private void RemoveCleanedFiles()
        {
            if (Directory.Exists(ArchiveFolder))
            {
                var cleanedFiles =
                    Directory.GetFiles(ArchiveFolder, "Catalog_cleaned.json", SearchOption.AllDirectories);
                foreach (var cleanedFile in cleanedFiles)
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Directory.Delete(Path.GetDirectoryName(cleanedFile), true);
                }
            }
        }

        private void SetDefaultSelectedComponents()
        {
            if (!Components.Any())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_layoutFile) || !File.Exists(_layoutFile))
            {
                return;
            }

            var obj = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_layoutFile));
            var channelUri = obj.Value<string>("channelUri");
            var regex = new Regex(@"aka.ms/vs/(?<version>\d+?)/release");
            var version = regex.Match(channelUri).Groups["version"].Value;
            var selectedVersion = regex.Match(SelectedItem.DownloaderUrl).Groups["version"].Value;
            if (!version.Equals(selectedVersion))
            {
                Ioc.Get<IWindowManager>().ShowMessageBox("下载目录已有版本与选中版本不一致", "提示");
            }

            if (obj.TryGetValue("add", out var addValue))
            {
                var addItems = addValue.ToObject<string[]>();
                foreach (var addItem in addItems)
                {
                    var id = addItem.Split(';').First();
                    var component = Components.SingleOrDefault(x => x.Id == id);
                    if (component != null)
                    {
                        component.IsSelected = true;
                    }
                    else
                    {
                        component = Components.Last(x => x.Children.Any()).Children
                            .SingleOrDefault(x => x.Id == id);
                        if (component != null)
                        {
                            component.IsSelected = true;
                        }
                    }
                }
            }

            if (obj.TryGetValue("addProductLang", out var addProductLangValue))
            {
                var langs = addProductLangValue.ToObject<string[]>();
                Execute.OnUIThread(() =>
                {
                    _languagesSelector.SelectedItems.Clear();
                    foreach (var lang in langs)
                    {
                        _languagesSelector.SelectedItems.Add(Languages.Find(x =>
                            string.Equals(x.Key, lang, StringComparison.CurrentCultureIgnoreCase)));
                    }
                });
            }
        }

        public async void Update()
        {
            var tempFile = await DownloadDownloaderAsync(SelectedItem.DownloaderUrl);
            if (!string.IsNullOrWhiteSpace(tempFile) && File.Exists(tempFile))
            {
                var process = Run(tempFile, $"--layout \"{DownloadFolder}\"");
                await Execute.OnUIThreadAsync(() => WaitProcessExit(process, tempFile));
            }
        }

        private async Task<string> DownloadDownloaderAsync(string downloaderUrl)
        {
            BusyContent = "正在下载引导程序……";
            IsBusy = true;
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            await HttpHelper.GetByteArrayAsync(downloaderUrl).ContinueWith(async task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    tempFile = string.Empty;
                    await Execute.OnUIThreadAsync(() =>
                    {
                        _windowManager.ShowMessageBox(task.Exception.Message, "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
                else
                {
                    var buffer = task.Result;
                    File.WriteAllBytes(tempFile, buffer);
                }
            });
            await Execute.OnUIThreadAsync(() =>
            {
                IsBusy = false;
                BusyContent = string.Empty;
            });
            return tempFile;
        }

        private void WaitProcessExit(Process process, string tempFile)
        {
            if (View is Window window)
            {
                window.Visibility = Visibility.Collapsed;
                Task.Run(() =>
                {
                    process.WaitForExit();
                    File.Delete(tempFile);
                    Execute.OnUIThreadAsync
                    (
                        () =>
                        {
                            NotifyOfPropertyChange(nameof(CanClean));
                            NotifyOfPropertyChange(nameof(CanDownload));
                            NotifyOfPropertyChange(nameof(CanUpdate));
                            window.Visibility = Visibility.Visible;
                        }
                    );
                });
            }
        }

        public async void Clean()
        {
            var tempFile = await DownloadDownloaderAsync(SelectedItem.DownloaderUrl);
            if (!string.IsNullOrWhiteSpace(tempFile) && File.Exists(tempFile))
            {
                var builder = new StringBuilder();
                builder.Append($"--layout \"{DownloadFolder}\"");
                foreach (var directory in Directory.GetDirectories(ArchiveFolder))
                {
                    var files = Directory.GetFiles(directory, "Catalog.json", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        builder.Append($" --clean \"{files[0]}\"");
                    }
                }

                var process = Run(tempFile, builder.ToString());
                await Execute.OnUIThreadAsync(() => WaitProcessExit(process, tempFile));
                RemoveCleanedFiles();
            }
        }

        public async void Download(IList languages)
        {
            if (Directory.Exists(DownloadFolder) && (Directory.GetFiles(DownloadFolder).Length > 0 ||
                                                     Directory.GetDirectories(DownloadFolder).Length > 0))
            {
                if (_windowManager.ShowMessageBox($"下载目录【{DownloadFolder}】不是一个空目录，需要清空目录吗？", "提示", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Directory.Delete(DownloadFolder, true);
                }
            }
            var tempFile = await DownloadDownloaderAsync(SelectedItem.DownloaderUrl);
            if (!string.IsNullOrWhiteSpace(tempFile) && File.Exists(tempFile))
            {
                var builder = new StringBuilder();
                builder.Append($"--layout \"{DownloadFolder}\"");
                foreach (var language in languages.Cast<LanguageItem>())
                {
                    builder.Append($" --lang {language.Key}");
                }

                foreach (var component in Components)
                {
                    if (!string.IsNullOrWhiteSpace(component.Id))
                    {
                        if (component.IsSelected)
                        {
                            builder.Append($" --add {component.Id}");
                        }
                    }
                    else
                    {
                        foreach (var child in component.Children)
                        {
                            if (child.IsSelected)
                            {
                                builder.Append($" --add {child.Id}");
                            }
                        }
                    }
                }

                builder.Append(" --includeRecommended --includeOptional");
                var process = Run(tempFile, builder.ToString());
                await Execute.OnUIThreadAsync(() => WaitProcessExit(process, tempFile));
            }
        }

        private static Process Run(string cmd, string arg)
        {
            var process = new Process();
            process.StartInfo.FileName = cmd;
            process.StartInfo.Arguments = arg;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.Start();
            return process;
        }

        private void AnalysisCnComponents(HtmlDocument document)
        {
            var rootNode = document.DocumentNode.SelectSingleNode($"//*[@data-moniker=\"{_version}\"]");
            var nodes = rootNode.SelectNodes("h2");
            var components = new List<ComponentItem>();
            foreach (var node in nodes)
            {
                var title = node.InnerText.Trim();
                if (node.GetAttributeValue("id", string.Empty) == "unaffiliated-components")
                {
                    var componentItem = new ComponentItem { Title = title };
                    var rowNodes = NextUtil(node, x => x.Name == "table").SelectNodes("tbody/tr");
                    foreach (var rowNode in rowNodes)
                    {
                        componentItem.Children.Add(new ComponentItem
                        {
                            Title = rowNode.SelectSingleNode("td[2]").InnerText,
                            Id = rowNode.SelectSingleNode("td[1]").InnerText
                        });
                    }

                    components.Add(componentItem);
                }
                else
                {
                    var idNode = NextUtil(node, x => x.Name == "p");
                    var id = idNode.LastChild.InnerText.Trim();
                    if (id.Equals("Microsoft.VisualStudio.Workload.CoreEditor", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var descriptionNode = NextUtil(idNode, x => x.Name == "p");
                    var description = descriptionNode.LastChild.InnerText.Trim();
                    components.Add(new ComponentItem { Title = title, Id = id, Description = description });
                }
            }

            Execute.OnUIThread(() =>
            {
                Components = new ObservableCollection<ComponentItem>(components);
                SetDefaultSelectedComponents();
            });
        }

        private HtmlNode NextUtil(HtmlNode node, Func<HtmlNode, bool> selector)
        {
            var next = node.NextSibling;
            if (next == null)
            {
                return null;
            }

            if (selector(next))
            {
                return next;
            }

            return NextUtil(next, selector);
        }

        protected override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
            switch (propertyName)
            {
                case nameof(SelectedItem):
                    if (SelectedItem != null)
                    {
                        var match = _regex.Match(SelectedItem.DetailUrl);
                        _version = match.Groups["version"].Value;
                    }

                    break;
            }
        }

        #endregion
    }

    internal class ComponentItem : PropertyChangedBase
    {
        #region Properties

        public ObservableCollection<ComponentItem> Children { get; set; } = new ObservableCollection<ComponentItem>();
        public string Description { get; set; }
        public string Title { get; set; }
        public string Id { get; set; }
        public bool IsSelected { get; set; }

        #endregion
    }

    internal class LanguageItem
    {
        #region Properties

        public string Title { get; set; }
        public string Key { get; set; }

        #endregion
    }

    //internal class HttpHelper
    //{
    //    #region Methods

    //    public static async Task<string> GetStringAsync(string requestUri, Encoding encoding = null)
    //    {
    //        var buffer = await GetAsync(requestUri);
    //        return (encoding ?? Encoding.UTF8).GetString(buffer);
    //    }

    //    public static async Task<byte[]> GetAsync(string requestUri)
    //    {
    //        byte[] buffer;
    //        using (var client = new HttpClient())
    //        {
    //            var response = await client.GetAsync(requestUri, CancellationToken.None);
    //            buffer = await response.Content.ReadAsByteArrayAsync();
    //        }

    //        return buffer;
    //    }

    //    #endregion
    //}
}