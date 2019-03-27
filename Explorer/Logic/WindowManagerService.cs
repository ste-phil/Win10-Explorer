using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Explorer.Logic
{
    public delegate void ViewClosedHandler(ViewLifetimeControl viewControl, EventArgs e);

    // For instructions on testing this service see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/features/multiple-views.md
    // More details about showing multiple views at https://docs.microsoft.com/windows/uwp/design/layout/show-multiple-views
    public class WindowManagerService
    {
        private static WindowManagerService _current;

        public static WindowManagerService Current => _current ?? (_current = new WindowManagerService());

        // Contains all the opened secondary views.
        public ObservableCollection<ViewLifetimeControl> SecondaryViews { get; } = new ObservableCollection<ViewLifetimeControl>();

        public int MainViewId { get; private set; }

        public CoreDispatcher MainDispatcher { get; private set; }

        public event EventHandler<MessageEventArgs> MainWindowMessageReceived;

        public void Initialize()
        {
            MainViewId = ApplicationView.GetForCurrentView().Id;
            MainDispatcher = Window.Current.Dispatcher;
        }

        // Displays a view as a standalone
        // You can use the resulting ViewLifeTileControl to interact with the new window.
        public async Task<ViewLifetimeControl> TryShowAsStandaloneAsync(string windowTitle, Type pageType, string dataContext = null)
        {
            ViewLifetimeControl viewControl = await CreateViewLifetimeControlAsync(windowTitle, pageType, dataContext);
            SecondaryViews.Add(viewControl);
            viewControl.StartViewInUse();
            var viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(viewControl.Id, ViewSizePreference.Default, ApplicationView.GetForCurrentView().Id, ViewSizePreference.Default);
            viewControl.StopViewInUse();
            return viewControl;
        }

        // Displays a view in the specified view mode
        public async Task<ViewLifetimeControl> TryShowAsViewModeAsync(string windowTitle, Type pageType, ApplicationViewMode viewMode = ApplicationViewMode.Default)
        {
            ViewLifetimeControl viewControl = await CreateViewLifetimeControlAsync(windowTitle, pageType);
            SecondaryViews.Add(viewControl);
            viewControl.StartViewInUse();
            var viewShown = await ApplicationViewSwitcher.TryShowAsViewModeAsync(viewControl.Id, viewMode);
            viewControl.StopViewInUse();
            return viewControl;
        }

        private async Task<ViewLifetimeControl> CreateViewLifetimeControlAsync(string windowTitle, Type pageType, string dataContext = null)
        {
            ViewLifetimeControl viewControl = null;

            await CoreApplication.CreateNewView().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                viewControl = ViewLifetimeControl.CreateForCurrentView();
                viewControl.Title = windowTitle;
                viewControl.Context = dataContext;
                viewControl.StartViewInUse();
                var frame = new Frame();
                //frame.RequestedTheme = ThemeSelectorService.Theme;
                frame.Navigate(pageType, viewControl);
                Window.Current.Content = frame;
                Window.Current.Activate();
                ApplicationView.GetForCurrentView().Title = viewControl.Title;
            });

            return viewControl;
        }

        public bool IsWindowOpen(string windowTitle) => SecondaryViews.Any(v => v.Title == windowTitle);

        public ViewLifetimeControl GetWindowById(int id) => SecondaryViews.FirstOrDefault(v => v.Id == id);

        public void SendMessage(int toid, string message, object data = null)
        {
            if (toid == MainViewId)
            {
                // Special case for main window
                MainWindowMessageReceived?.Invoke(this, new MessageEventArgs(ApplicationView.GetForCurrentView().Id, toid, message, data));
            }
            else
            {
                // Any secondary window
                GetWindowById(toid)?.SendMessage(message, ApplicationView.GetForCurrentView().Id, data);
            }
        }
    }

    // A custom event that fires whenever the secondary view is ready to be closed. You should
    // clean up any state (including deregistering for events) then close the window in this handler
    public delegate void ViewReleasedHandler(object sender, EventArgs e);

    // Whenever the main view is about to interact with the secondary view, it should call
    // StartViewInUse on this object. When finished interacting, it should call StopViewInUse.
    public sealed class ViewLifetimeControl
    {
        // Window for this particular view. Used to register and unregister for events
        private CoreWindow _window;
        private int _refCount = 0;
        private bool _released = false;

        private event ViewReleasedHandler InternalReleased;

        // Necessary to communicate with the window
        public CoreDispatcher Dispatcher { get; private set; }

        // This id is used in all of the ApplicationViewSwitcher and ProjectionManager APIs
        public int Id { get; private set; }

        // Initial title for the window
        public string Title { get; set; }

        // Optional context to provide from window opener
        public string Context { get; set; }

        public event EventHandler<MessageEventArgs> MessageReceived;

        public event ViewReleasedHandler Released
        {
            add
            {
                bool releasedCopy = false;
                lock (this)
                {
                    releasedCopy = _released;
                    if (!_released)
                    {
                        InternalReleased += value;
                    }
                }

                if (releasedCopy)
                {
                    throw new InvalidOperationException("ExceptionViewLifeTimeControlViewDisposal");
                }
            }

            remove
            {
                lock (this)
                {
                    InternalReleased -= value;
                }
            }
        }

        private ViewLifetimeControl(CoreWindow newWindow)
        {
            Dispatcher = newWindow.Dispatcher;
            _window = newWindow;
            Id = ApplicationView.GetApplicationViewIdForWindow(_window);
            RegisterForEvents();
        }

        public static ViewLifetimeControl CreateForCurrentView()
        {
            return new ViewLifetimeControl(CoreWindow.GetForCurrentThread());
        }

        public void SendMessage(string message, int fromid, object data = null)
        {
            MessageReceived?.Invoke(this, new MessageEventArgs(fromid, Id, message, data));
        }

        // Signals that the view is being interacted with by another view,
        // so it shouldn't be closed even if it becomes "consolidated"
        public int StartViewInUse()
        {
            bool releasedCopy = false;
            int refCountCopy = 0;

            lock (this)
            {
                releasedCopy = _released;
                if (!_released)
                {
                    refCountCopy = ++_refCount;
                }
            }

            if (releasedCopy)
            {
                throw new InvalidOperationException("ExceptionViewLifeTimeControlViewDisposal");
            }

            return refCountCopy;
        }

        // Should come after any call to StartViewInUse
        // Signals that the another view has finished interacting with the view tracked by this object
        public int StopViewInUse()
        {
            int refCountCopy = 0;
            bool releasedCopy = false;

            lock (this)
            {
                releasedCopy = _released;
                if (!_released)
                {
                    refCountCopy = --_refCount;
                    if (refCountCopy == 0)
                    {
                        var task = Dispatcher.RunAsync(CoreDispatcherPriority.Low, FinalizeRelease);
                    }
                }
            }

            if (releasedCopy)
            {
                throw new InvalidOperationException("ExceptionViewLifeTimeControlViewDisposal");
            }

            return refCountCopy;
        }

        private void RegisterForEvents()
        {
            ApplicationView.GetForCurrentView().Consolidated += ViewConsolidated;
        }

        private void UnregisterForEvents()
        {
            ApplicationView.GetForCurrentView().Consolidated -= ViewConsolidated;
        }

        private void ViewConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs e)
        {
            StopViewInUse();
        }

        private void FinalizeRelease()
        {
            bool justReleased = false;
            lock (this)
            {
                if (_refCount == 0)
                {
                    justReleased = true;
                    _released = true;
                }
            }

            if (justReleased)
            {
                UnregisterForEvents();
                InternalReleased(this, null);
            }
        }
    }

    /// <summary>
    /// Information about Message sent between Windows.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        public int FromId { get; private set; }

        public int ToId { get; private set; }

        public string Message { get; private set; }

        /// <summary>
        /// Extra misc data, should be primitive or thread-safe type.
        /// </summary>
        public object Data { get; private set; }

        public MessageEventArgs(int from, int to, string message, object data = null)
        {
            FromId = from;
            ToId = to;
            Message = message;
            Data = data;
        }
    }
}
