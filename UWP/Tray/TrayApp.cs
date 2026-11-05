using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class TrayProgram
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "Local\\TouchpadPS4.UWP.Tray", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.Run(new TrayContext());
        }
    }
}

sealed class HotkeyWindow : NativeWindow, IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    public event Action Toggle;
    public readonly bool Registered;
    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
        Registered = RegisterHotKey(Handle, 1, 0x4003, 0x4D);
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && Toggle != null) Toggle();
        base.WndProc(ref message);
    }
    public void Dispose() { UnregisterHotKey(Handle, 1); DestroyHandle(); }
}

sealed class TrayContext : ApplicationContext
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFamilyName(ref uint size, StringBuilder name);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenThread(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CancelSynchronousIo(IntPtr thread);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    readonly NotifyIcon icon;
    readonly ToolStripMenuItem toggle;
    readonly HotkeyWindow hotkey;
    readonly System.Windows.Forms.Timer timer;
    readonly string folder;
    Thread worker, finder;
    volatile uint workerId;
    volatile bool finding;
    volatile string foundPath;
    bool desired, keepTray = true, exiting;
    double sensitivity = 1;
    double speedBoost;
    bool tap = true, scroll = true;
    string settings = "", command = "", connection = "Procurando controle…";
    DateTime nextSearch = DateTime.MinValue, started = DateTime.UtcNow;

    public TrayContext()
    {
        uint size = 0;
        GetCurrentPackageFamilyName(ref size, null);
        if (size == 0) throw new InvalidOperationException("Inicie este componente pelo aplicativo instalado.");
        var family = new StringBuilder((int)size);
        if (GetCurrentPackageFamilyName(ref size, family) != 0) throw new InvalidOperationException("Pacote Windows não identificado.");
        folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family.ToString(), "LocalState");
        Directory.CreateDirectory(folder);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Touchpad PS4", null, delegate { OpenApp(); });
        toggle = new ToolStripMenuItem("Ativar mouse", null, delegate { Toggle(); });
        menu.Items.Add(toggle);
        menu.Items.Add("Reconectar controle", null, delegate { Reconnect(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, delegate { ExitThread(); });
        icon = new NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath), Text = "Touchpad PS4 · desativado", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += delegate { OpenApp(); };
        hotkey = new HotkeyWindow(); hotkey.Toggle += Toggle;
        timer = new System.Windows.Forms.Timer { Interval = 300 };
        timer.Tick += delegate { Tick(); }; timer.Start();
        ReadSettings();
    }
    void OpenApp()
    {
        try { Process.Start(new ProcessStartInfo("touchpadps4:") { UseShellExecute = true }); }
        catch (Exception ex) { icon.ShowBalloonTip(3000, "Touchpad PS4", ex.Message, ToolTipIcon.Error); }
    }
    void ReadSettings()
    {
        try
        {
            string path = Path.Combine(folder, "settings.txt");
            if (!File.Exists(path)) return;
            string value = File.ReadAllText(path);
            if (value == settings) return;
            string[] a = value.Replace("\r", "").Split('\n');
            double parsed;
            if (a.Length < 5 || !double.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed)) return;
            settings = value;
            desired = a[0] == "1"; sensitivity = Math.Max(.2, Math.Min(3, parsed));
            double boost;
            speedBoost = a.Length >= 6 && double.TryParse(a[5], NumberStyles.Float, CultureInfo.InvariantCulture, out boost)
                && !double.IsNaN(boost) && !double.IsInfinity(boost) ? Math.Max(0, Math.Min(2, boost)) : 0;
            tap = a[2] == "1"; scroll = a[3] == "1"; keepTray = a[4] == "1";
            Ds4.SetMotionSettings(sensitivity, speedBoost); Ds4.TapEnabled = tap; Ds4.ScrollEnabled = scroll;
            Ds4.SetEnabled(desired && worker != null && worker.IsAlive && !Ds4.Disconnected);
        }
        catch (IOException) { }
    }
    void Write(string name, string text)
    {
        string target = Path.Combine(folder, name), temp = target + ".tray.tmp";
        File.WriteAllText(temp, text);
        if (File.Exists(target)) File.Replace(temp, target, null); else File.Move(temp, target);
    }
    void Toggle()
    {
        ReadSettings(); desired = !desired;
        settings = (desired ? "1" : "0") + "\n" + sensitivity.ToString(CultureInfo.InvariantCulture) + "\n" + (tap ? "1" : "0") + "\n" + (scroll ? "1" : "0") + "\n" + (keepTray ? "1" : "0") + "\n" + speedBoost.ToString(CultureInfo.InvariantCulture);
        try { Write("settings.txt", settings); } catch (IOException) { }
        Ds4.SetEnabled(desired && worker != null && worker.IsAlive && !Ds4.Disconnected);
        UpdateStatus();
    }
    void Tick()
    {
        ReadSettings();
        try
        {
            string path = Path.Combine(folder, "command.txt");
            if (File.Exists(path))
            {
                string value = File.ReadAllText(path);
                if (value != command)
                {
                    command = value;
                    if (value.EndsWith("\nexit")) { File.Delete(path); ExitThread(); return; }
                    if (value.EndsWith("\nreconnect")) Reconnect();
                }
            }
            if (!keepTray && (DateTime.UtcNow - started).TotalSeconds > 10)
            {
                string heartbeat = Path.Combine(folder, "heartbeat.txt");
                if (!File.Exists(heartbeat) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(heartbeat)).TotalSeconds > 8) { ExitThread(); return; }
            }
        }
        catch (IOException) { }

        if (worker != null && !worker.IsAlive) { Ds4.Close(); worker = null; connection = "Controle desconectado. Procurando novamente…"; }
        if (worker == null && !finding && DateTime.UtcNow >= nextSearch)
        {
            foundPath = null; finding = true;
            finder = new Thread(delegate() { try { foundPath = FindController(); } finally { finding = false; } });
            finder.IsBackground = true; finder.Start(); nextSearch = DateTime.UtcNow.AddSeconds(4);
        }
        if (!finding && foundPath != null && worker == null)
        {
            string path = foundPath; foundPath = null;
            Ds4.IsBt = path.IndexOf("HID#{", StringComparison.OrdinalIgnoreCase) >= 0;
            int error = Ds4.Open(path);
            if (error == 0)
            {
                Ds4.Stop = false; Ds4.Disconnected = false; Ds4.N01 = 0; Ds4.N11 = 0; Ds4.FullSeen = false;
                worker = new Thread(delegate() { workerId = GetCurrentThreadId(); Ds4.Loop(); }); worker.IsBackground = true; worker.Start();
                Ds4.SetEnabled(desired); connection = "Conectado";
            }
            else connection = "Não foi possível abrir o controle (erro " + error + ").";
        }
        UpdateStatus();
    }
    void UpdateStatus()
    {
        string status = worker != null && worker.IsAlive ? (Ds4.FullSeen ? "Controle conectado · touchpad pronto" : "Controle conectado · aguardando touchpad") : connection;
        if (!hotkey.Registered) status += " · atalho ocupado por outro aplicativo";
        toggle.Text = desired ? "Desativar mouse" : "Ativar mouse";
        toggle.Checked = Ds4.Enabled;
        icon.Text = "Touchpad PS4 · " + (Ds4.Enabled ? "ativado" : "desativado");
        try { Write("status.txt", (desired ? "1" : "0") + "\n" + status); } catch (IOException) { }
    }
    bool StopWorker()
    {
        Ds4.Stop = true; Ds4.SetEnabled(false);
        if (worker != null && worker.IsAlive)
        {
            IntPtr thread = OpenThread(1, false, workerId);
            if (thread != IntPtr.Zero) { CancelSynchronousIo(thread); CloseHandle(thread); }
            if (!worker.Join(1500)) { connection = "Aguardando o dispositivo encerrar a leitura. Tente reconectar novamente."; return false; }
        }
        Ds4.Close(); worker = null; workerId = 0; return true;
    }
    void Reconnect() { if (StopWorker()) { connection = "Procurando controle…"; nextSearch = DateTime.MinValue; } }
    protected override void ExitThreadCore()
    {
        if (exiting) return;
        exiting = true; timer.Stop(); StopWorker(); hotkey.Dispose(); icon.Visible = false; icon.Dispose(); timer.Dispose();
        try { Write("status.txt", "0\nAplicativo encerrado."); } catch (IOException) { }
        base.ExitThreadCore();
    }
    static string FindController()
    {
        string bt = null;
        try
        {
            using (var query = new ManagementObjectSearcher("SELECT DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE PNPClass='HIDClass'"))
            using (var devices = query.Get())
            {
                foreach (ManagementObject device in devices)
                using (device)
                {
                    string id = device["DeviceID"] as string;
                    object error = device["ConfigManagerErrorCode"];
                    if (id == null || (error != null && Convert.ToUInt32(error) != 0)) continue;
                    string upper = id.ToUpperInvariant();
                    if (!upper.Contains("054C") || (!upper.Contains("09CC") && !upper.Contains("05C4"))) continue;
                    string path = "\\\\?\\" + id.Replace('\\', '#') + "#{4d1e55b2-f16f-11cf-88cb-001111000030}";
                    if (!upper.StartsWith("HID\\{")) return path;
                    bt = path;
                }
            }
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        return bt;
    }
}
