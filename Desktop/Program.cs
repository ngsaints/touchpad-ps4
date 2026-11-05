using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

static class ModernProgram
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "Local\\TouchpadPS4.UWP.Tray", out created))
        {
            if (!created) { MessageBox.Show("O Touchpad PS4 já está aberto. Use o ícone perto do relógio para abrir a janela.", "Touchpad PS4"); return; }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            try { new ModernWindow(app); app.Run(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Não foi possível abrir o Touchpad PS4"); }
        }
    }
}

sealed class ModernWindow
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    readonly Application app;
    readonly Window window;
    readonly TrayContext controller;
    bool syncing, exiting;
    T Get<T>(string name) where T : class { return (T)window.FindName(name); }
    public ModernWindow(Application application)
    {
        app = application;
        string root = AppDomain.CurrentDomain.BaseDirectory;
        using (var reader = File.OpenRead(Path.Combine(root, "MainWindow.xaml"))) window = (Window)XamlReader.Load(reader);
        app.MainWindow = window;
        Get<Image>("ControllerImage").Source = new BitmapImage(new Uri(Path.Combine(root, "dualshock4.png")));
        window.SourceInitialized += delegate
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                int dark = 1, rounded = 2, mica = 2;
                DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
                DwmSetWindowAttribute(hwnd, 33, ref rounded, 4);
                DwmSetWindowAttribute(hwnd, 38, ref mica, 4);
            }
            catch (DllNotFoundException) { }
        };
        controller = new TrayContext(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TouchpadPS4"));
        controller.OpenRequested += delegate { window.Show(); if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Maximized; window.Activate(); };
        controller.ShutdownRequested += delegate { exiting = true; app.Shutdown(); };
        controller.StateChanged += Update;
        Get<Slider>("SensitivitySlider").Value = controller.Sensitivity;
        Get<Slider>("SpeedSlider").Value = controller.SpeedBoost * 100;
        Get<CheckBox>("TapCheck").IsChecked = controller.Tap;
        Get<CheckBox>("ScrollCheck").IsChecked = controller.Scroll;
        Get<CheckBox>("TrayCheck").IsChecked = controller.KeepTray;
        Get<Slider>("SensitivitySlider").ValueChanged += delegate { Save(controller.Desired); };
        Get<Slider>("SpeedSlider").ValueChanged += delegate { Save(controller.Desired); };
        foreach (string name in new[] { "TapCheck", "ScrollCheck", "TrayCheck" })
        {
            Get<CheckBox>(name).Checked += delegate { Save(controller.Desired); };
            Get<CheckBox>(name).Unchecked += delegate { Save(controller.Desired); };
        }
        Get<Button>("ToggleButton").Click += delegate { Save(!controller.Desired); };
        Get<Button>("ReconnectButton").Click += delegate { controller.ReconnectController(); };
        Get<Button>("ResetButton").Click += delegate { syncing = true; Get<Slider>("SensitivitySlider").Value = 1; Get<Slider>("SpeedSlider").Value = 0; syncing = false; Save(controller.Desired); };
        Get<Button>("HideButton").Click += delegate { Get<CheckBox>("TrayCheck").IsChecked = true; Save(controller.Desired); window.Hide(); };
        Get<Button>("ExitButton").Click += delegate { controller.Shutdown(); };
        window.Closing += OnClosing;
        window.StateChanged += delegate { if (window.WindowState == WindowState.Minimized && controller.KeepTray) window.Hide(); };
        Update(); window.Show();
    }
    void Save(bool enabled)
    {
        if (syncing) return;
        try
        {
            controller.SetPreferences(enabled, Get<Slider>("SensitivitySlider").Value, Get<Slider>("SpeedSlider").Value / 100,
                Get<CheckBox>("TapCheck").IsChecked == true, Get<CheckBox>("ScrollCheck").IsChecked == true, Get<CheckBox>("TrayCheck").IsChecked == true);
        }
        catch (IOException ex) { Get<TextBlock>("FooterStatus").Text = "Não foi possível salvar: " + ex.Message; }
    }
    void Update()
    {
        Get<TextBlock>("SensValue").Text = Get<Slider>("SensitivitySlider").Value.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + "×";
        Get<TextBlock>("SpeedValue").Text = "+" + Get<Slider>("SpeedSlider").Value.ToString("0") + "%";
        Get<Button>("ToggleButton").Content = controller.Desired ? "Pausar touchpad" : "Ativar touchpad";
        bool connected = Ds4.Handle != IntPtr.Zero && Ds4.Handle != new IntPtr(-1) && !Ds4.Disconnected;
        Get<TextBlock>("ConnectionTitle").Text = connected ? (Ds4.FullSeen ? "Seu controle está pronto" : "Conectando seu controle") : "Vamos conectar seu controle";
        Get<TextBlock>("ConnectionStatus").Text = controller.CurrentStatus ?? "Conecte por USB ou pareie via Bluetooth.";
        Get<TextBlock>("TransportText").Text = connected ? (Ds4.IsBt ? "BLUETOOTH" : "USB") : "USB / BLUETOOTH";
        Get<TextBlock>("FooterStatus").Text = (Ds4.Enabled ? "Mouse do PS4 ativado" : "Mouse do PS4 pausado") + "  ·  Ajustes salvos automaticamente";
    }
    void OnClosing(object sender, CancelEventArgs e)
    {
        if (exiting) return;
        if (controller.KeepTray) { e.Cancel = true; window.Hide(); }
        else { e.Cancel = true; controller.Shutdown(); }
    }
}
