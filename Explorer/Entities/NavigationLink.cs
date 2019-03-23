using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Explorer.Entities
{
    public class NavigationLink
    {
        public NavigationLink(Symbol icon, string name, string path)
        {
            Icon = icon;
            Name = name;
            Path = path;
        }

        public Symbol Icon { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
