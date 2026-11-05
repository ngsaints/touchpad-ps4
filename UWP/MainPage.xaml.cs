using System;
using System.Globalization;
using System.IO;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace TouchpadPS4
{
    public sealed partial class MainPage : Page
    {
        bool ready, updating;
        readonly string folder = ApplicationData.Current.LocalFolder.Path;
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        public MainPage()
        {
            InitializeComponent();
            var view = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
            view.SetPreferredMinSize(new Windows.Foundation.Size(380, 500));
            Loaded += OnLoaded;
            Unloaded += delegate { timer.Stop(); };
            timer.Tick += delegate { RefreshStatus(); };
            Application.Current.Suspending += delegate { timer.Stop(); };
            Application.Current.Resuming += delegate { timer.Start(); RefreshStatus(); };
        }
        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            updating = true;
            try
            {
                var path = Path.Combine(folder, "settings.txt");
                if (File.Exists(path))
                {
                    var a = File.ReadAllLines(path);
                    if (a.Length >= 5)
                    {
                        EnabledSwitch.IsOn = a[0] == "1";
                        double value;
                        if (double.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) Sensitivity.Value = value;
                        TapSwitch.IsOn = a[2] == "1"; ScrollSwitch.IsOn = a[3] == "1"; TraySwitch.IsOn = a[4] == "1";
                        if (a.Length >= 6 && double.TryParse(a[5], NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                            && !double.IsNaN(value) && !double.IsInfinity(value)) Speed.Value = value * 100;
                    }
                }
            }
            catch (IOException) { }
            updating = false; ready = true;
            Save();
            try { await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync(); timer.Start(); }
            catch (Exception ex) { StatusText.Text = "Não foi possível iniciar o componente desktop: " + ex.Message; }
        }
        void Save()
        {
            if (!ready || updating) return;
            try
            {
                var text = (EnabledSwitch.IsOn ? "1" : "0") + "\n" + Sensitivity.Value.ToString(CultureInfo.InvariantCulture)
                    + "\n" + (TapSwitch.IsOn ? "1" : "0") + "\n" + (ScrollSwitch.IsOn ? "1" : "0") + "\n" + (TraySwitch.IsOn ? "1" : "0")
                    + "\n" + (Speed.Value / 100).ToString(CultureInfo.InvariantCulture);
                AtomicWrite("settings.txt", text);
            }
            catch (IOException ex) { StatusText.Text = "Não foi possível salvar: " + ex.Message; }
        }
        void AtomicWrite(string name, string value)
        {
            // Rename evita que o processo auxiliar leia um arquivo incompleto.
            string temp = Path.Combine(folder, name + ".uwp.tmp"), target = Path.Combine(folder, name);
            File.WriteAllText(temp, value);
            if (File.Exists(target)) File.Replace(temp, target, null); else File.Move(temp, target);
        }
        void SettingsChanged(object sender, RoutedEventArgs e) { Save(); }
        void SensitivityChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (SensitivityLabel != null) SensitivityLabel.Text = "Sensibilidade · " + e.NewValue.ToString("0.0") + "×";
            Save();
        }
        void SpeedChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (SpeedLabel != null) SpeedLabel.Text = "Velocidade em movimentos rápidos · +" + e.NewValue.ToString("0") + "%";
            Save();
        }
        void ResetMotionClick(object sender, RoutedEventArgs e)
        {
            updating = true; Sensitivity.Value = 1; Speed.Value = 0; updating = false;
            Save();
        }
        void RefreshStatus()
        {
            try
            {
                AtomicWrite("heartbeat.txt", DateTime.UtcNow.Ticks.ToString());
                string path = Path.Combine(folder, "status.txt");
                if (!File.Exists(path)) return;
                var a = File.ReadAllLines(path);
                if (a.Length < 2) return;
                StatusText.Text = a[1];
                if (File.GetLastWriteTimeUtc(path) >= File.GetLastWriteTimeUtc(Path.Combine(folder, "settings.txt")))
                { updating = true; EnabledSwitch.IsOn = a[0] == "1"; updating = false; }
            }
            catch (IOException) { }
        }
        void ReconnectClick(object sender, RoutedEventArgs e) { AtomicWrite("command.txt", Guid.NewGuid() + "\nreconnect"); }
        void HideClick(object sender, RoutedEventArgs e)
        {
            TraySwitch.IsOn = true;
            Save();
            // Encerrar a interface não encerra o processo auxiliar full-trust.
            Application.Current.Exit();
        }
        void ExitClick(object sender, RoutedEventArgs e)
        {
            AtomicWrite("command.txt", Guid.NewGuid() + "\nexit");
            Application.Current.Exit();
        }
    }
}
