using Explorer.Helper;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using static Explorer.Controls.Notifications;

namespace Explorer.Controls
{
    public static class Notifications
    {
        public static void Show(string notification, Symbol symbol, string actionName = "", Action action = null, int timeout = 5000)
            => NotificationControl.Instance.Show(notification, symbol, actionName, action, timeout);
    }


    public sealed partial class NotificationControl : UserControl
    {
        public static NotificationControl Instance;

        private Storyboard actionBtnAnimation;

        public NotificationControl()
        {
            this.InitializeComponent();
            actionBtnAnimation = (Storyboard)Resources["GradientAnimation"];

            Instance = this;

            //if (Instance == null) Instance = this;
            //else throw new Exception("Only one NotificationControl allowed");
        }

        public void Show(string notification, Symbol symbol, string actionName = "", Action action = null, int timeout = 5000)
        {
            var cts = new CancellationTokenSource();

            PrepareActionBtn(cts, actionName, action);
            NotificationText.Text = notification;
            NotificationType.Symbol = symbol;

            _ = Window.Current.Dispatcher.TryRunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await Task.Delay(timeout);
                if (!cts.IsCancellationRequested) Hide();
            });

            Show();
        }

        private void PrepareActionBtn(CancellationTokenSource cts, string actionName = "", Action action = null)
        {
            if (actionName == "")
            {
                ActionBtn.Visibility = Visibility.Collapsed;
                return;
            }

            ActionBtn.Visibility = Visibility.Visible;
            ActionBtn.Content = actionName;
            ActionBtn.Command = new Command(() => { cts.Cancel(); HideAction(); action?.Invoke(); }, () => true);
        }

        private void Show()
        {
            Translation = new Vector3(0, 0, 0);
        }

        private async void HideAction()
        {
            actionBtnAnimation.Begin();
            NotificationType.Symbol = Symbol.Redo;
            NotificationText.Text = "Undone";
            ActionBtn.Visibility = Visibility.Collapsed;

            await Task.Delay(1250);
            await Window.Current.Dispatcher.TryRunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Hide();
            });
        }

        private async void Hide()
        {
            Translation = new Vector3(0, 100, 0);
            await Task.Delay(250);
            actionBtnAnimation.Stop();
            actionBtnAnimation.Seek(TimeSpan.Zero);
        }
    }
}
