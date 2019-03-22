using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Explorer.Entities
{
    public class NavigationItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DriveTemplate { get; set; }
        public DataTemplate PathTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is Drive) return DriveTemplate;
            if (item is FileSystemElement) return PathTemplate;

            return base.SelectTemplateCore(item);
        }
    }
}
