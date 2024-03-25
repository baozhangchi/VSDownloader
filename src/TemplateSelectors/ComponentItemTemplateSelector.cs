#region

using System.Windows;
using System.Windows.Controls;
using VSDownloader.ViewModels;

#endregion

namespace VSDownloader.TemplateSelectors
{
    internal class ComponentItemTemplateSelector : DataTemplateSelector
    {
        #region Methods

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ComponentItem componentItem)
            {
                if (string.IsNullOrWhiteSpace(componentItem.Id))
                {
                    return CannotSelectTemplate;
                }

                return CanSelectTemplate;
            }

            return base.SelectTemplate(item, container);
        }

        #endregion

        #region Properties

        public DataTemplate CannotSelectTemplate { get; set; }

        public DataTemplate CanSelectTemplate { get; set; }

        #endregion
    }
}