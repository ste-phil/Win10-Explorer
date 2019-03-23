using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;

using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.UI.Core;

namespace Explorer.Logic
{
    public class DeviceWatcherService
    {
        private static DeviceWatcherService instance;

        public event EventHandler DeviceChanged;

        private CoreDispatcher dispatcher;
        private DeviceWatcher watcher;

        public int count;
        public DeviceInformation[] interfaces = new DeviceInformation[1000];
        public bool isEnumerationComplete;
        public string stopStatus = null;

        public static DeviceWatcherService Instance => instance ?? (instance = new DeviceWatcherService());

        private DeviceWatcherService() 
        {
            WatchDevices();    
        }

        public async void WatchDevices()
        {
            try
            {
                dispatcher = Window.Current.CoreWindow.Dispatcher;
                watcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

                // Add event handlers
                watcher.Added += Watcher_Added;
                watcher.Removed += Watcher_Removed;
                watcher.Updated += Watcher_Updated;
                watcher.EnumerationCompleted += Watcher_EnumerationCompleted;
                watcher.Stopped += Watcher_Stopped;
                watcher.Start();

            }
            catch (ArgumentException)
            {
                //The ArgumentException gets thrown by FindAllAsync when the GUID isn't formatted properly
                //The only reason we're catching it here is because the user is allowed to enter GUIDs without validation
                //In normal usage of the API, this exception handling probably wouldn't be necessary when using known-good GUIDs 
            }
        }

        public async void StopWatcher(object sender, RoutedEventArgs eventArgs)
        {
            try
            {
                if (watcher.Status == Windows.Devices.Enumeration.DeviceWatcherStatus.Stopped)
                {
                    stopStatus = "The enumeration is already stopped.";
                }
                else
                {
                    watcher.Stop();
                }
            }
            catch (ArgumentException)
            {
            }
        }

        public async void Watcher_Added(DeviceWatcher sender, DeviceInformation deviceInterface)
        {
            interfaces[count] = deviceInterface;
            count += 1;
            if (isEnumerationComplete)
            {
                await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    DisplayDeviceInterfaceArray();
                });
            }

            DeviceChanged?.Invoke(this, null);
        }

        public async void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate devUpdate)
        {
            int count2 = 0;
            foreach (DeviceInformation deviceInterface in interfaces)
            {
                if (count2 < count)
                {
                    if (interfaces[count2].Id == devUpdate.Id)
                    {
                        //Update the element.
                        interfaces[count2].Update(devUpdate);
                    }

                }
                count2 += 1;
            }

            DeviceChanged?.Invoke(this, null);
            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => DisplayDeviceInterfaceArray());
        }

        public async void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate devUpdate)
        {
            int count2 = 0;

            //Convert interfaces array to a list (IList).
            List<DeviceInformation> interfaceList = new List<DeviceInformation>(interfaces);
            foreach (DeviceInformation deviceInterface in interfaces)
            {
                if (count2 < count)
                {
                    if (interfaces[count2].Id == devUpdate.Id)
                    {
                        //Remove the element.
                        interfaceList.RemoveAt(count2);
                    }

                }
                count2 += 1;
            }

            DeviceChanged?.Invoke(this, null);

            //Convert the list back to the interfaces array.
            interfaces = interfaceList.ToArray();
            count -= 1;
            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                DisplayDeviceInterfaceArray();
            });
        }

        public async void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            isEnumerationComplete = true;
            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                DisplayDeviceInterfaceArray();
            });
        }

        public async void Watcher_Stopped(DeviceWatcher sender, object args)
        {
            if (watcher.Status == Windows.Devices.Enumeration.DeviceWatcherStatus.Aborted)
            {
                stopStatus = "Enumeration stopped unexpectedly. Click Watch to restart enumeration.";
            }
            else if (watcher.Status == Windows.Devices.Enumeration.DeviceWatcherStatus.Stopped)
            {
                stopStatus = "You requested to stop the enumeration. Click Watch to restart enumeration.";
            }
        }

        public async void DisplayDeviceInterfaceArray()
        {
            int count2 = 0;
            foreach (DeviceInformation deviceInterface in interfaces)
            {
                if (count2 < count)
                {
                    DisplayDeviceInterface(deviceInterface);
                }
                count2 += 1;
            }
        }

        public async void DisplayDeviceInterface(DeviceInformation deviceInterface)
        {
            var id = "Id:" + deviceInterface.Id;
            var name = deviceInterface.Name;
            var isEnabled = "IsEnabled:" + deviceInterface.IsEnabled;


            var item = id + " is \n" + name + " and \n" + isEnabled;
        }
    }
}