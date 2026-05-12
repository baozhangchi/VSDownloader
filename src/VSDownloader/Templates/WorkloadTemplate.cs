using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace VSDownloader.Templates;

//internal class WorkloadTemplate : FuncTreeDataTemplate<WorkloadInfo>
//{
//    public WorkloadTemplate(Func<WorkloadInfo, INameScope, Control> build) : base(x => true, (x, n) =>
//    {
//        if (x.Components.All(x => x.IsRequired == "必须"))
//        {
//            return new CheckBox()
//            {
//                Content = x.Title,
//                IsChecked = true,
//                IsEnabled = false
//            };
//        }

//        return 
//    }, x => x.Components)
//    {
//    }
//}