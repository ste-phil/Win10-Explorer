using System;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Explorer.Models
{
    public class MainPageModel : BaseModel
    {
        public ObservableCollection<NavigationViewItemBase> NavigationItems { get; set; }

        public MainPageModel()
        {
            NavigationItems = new ObservableCollection<NavigationViewItemBase>();

            AddDrivesToNavigation();
        }

        private void AddDrivesToNavigation()
        {
            var drives = FileSystem.GetDrives();

            for (int i = 0; i < drives.Length; i++)
            {
                //Inaccesible due to UWP permission stuff
                //var rootPath = $"{drives[i].VolumeLabel}:\\";
                var displayName = $"{drives[i].Name}"; //  ({rootPath})

                SymbolIcon icon;
                switch (drives[i].DriveType)
                {
                    case DriveType.Removable:
                        icon = new SymbolIcon((Symbol)0xE88E);
                        break;
                    case DriveType.CDRom:
                        icon = new SymbolIcon((Symbol)0xE958);
                        break;
                    case DriveType.Network:
                        icon = new SymbolIcon((Symbol)0xE969);
                        break;
                    default:
                        icon = new SymbolIcon((Symbol)0xEDA2);
                        break;
                }

                NavigationItems.Add(new NavigationViewItem { Tag = displayName, Content = displayName, Icon = icon});
            }
        }


        public void NavigateNavigationFSE(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var path = args.InvokedItemContainer.Tag.ToString();

            //NavigateTo(new FileSystemElement {Path = path});
        }
    }
}