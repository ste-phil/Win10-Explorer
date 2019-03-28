using Explorer.Helper;
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
        public Symbol Icon { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }

        public NavigationLink(Symbol icon, string name, string path)
        {
            Icon = icon;
            Name = name;
            Path = path;
        }
    }

    public class FavoriteNavigationLink
    {
        public Symbol Icon { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }

        public GenericCommand<FavoriteNavigationLink> MoveUpCommand { get; set; }
        public GenericCommand<FavoriteNavigationLink> MoveDownCommand { get; set; }
        public GenericCommand<FavoriteNavigationLink> RemoveCommand { get; set; }

        public FavoriteNavigationLink(Symbol icon, string name, string path, 
            GenericCommand<FavoriteNavigationLink> upCmd, GenericCommand<FavoriteNavigationLink> downCmd, GenericCommand<FavoriteNavigationLink> removeCmd)
        {
            Icon = icon;
            Name = name;
            Path = path;

            MoveUpCommand = upCmd;
            MoveDownCommand = downCmd;
            RemoveCommand = removeCmd;
        }
    }
}
