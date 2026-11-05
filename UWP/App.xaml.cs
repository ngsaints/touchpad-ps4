using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TouchpadPS4
{
    sealed partial class App : Application
    {
        public App() { InitializeComponent(); }
        protected override void OnLaunched(LaunchActivatedEventArgs e) { Open(); }
        protected override void OnActivated(IActivatedEventArgs args) { Open(); }
        private static void Open()
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null) { frame = new Frame(); Window.Current.Content = frame; }
            if (frame.Content == null) frame.Navigate(typeof(MainPage));
            Window.Current.Activate();
        }
    }
}
