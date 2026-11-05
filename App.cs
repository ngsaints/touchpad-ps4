using System;
using System.Drawing;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    // controles
    Label status, sensLabel, hint;
    Button toggle, reconnect;
    TrackBar sens;
    CheckBox cTap, cScroll;
    System.Windows.Forms.Timer timer;
    Thread eng;
    volatile bool running = false;

    // hotkey global Ctrl+Alt+M
    const int HOTKEY_ID = 1;
    const uint MOD_CONTROL = 0x0002, MOD_ALT = 0x0001, VK_M = 0x4D;
    const int WM_HOTKEY = 0x0312;
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    public MainForm()
    {
        Text = "Touchpad PS4 como Mouse";
        Size = new Size(430, 330);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(32, 33, 36);
        ForeColor = Color.White;

        status = new Label { Left = 15, Top = 12, Width = 385, Height = 42, ForeColor = Color.Gainsboro, Text = "Procurando controle..." };
        Add(status);

        toggle = new Button { Left = 15, Top = 60, Width = 385, Height = 44, Text = "ATIVAR  (Ctrl+Alt+M)", Font = new Font("Segoe UI", 11, FontStyle.Bold), BackColor = Color.FromArgb(60, 63, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        toggle.Click += delegate { Toggle(); };
        Add(toggle);

        sensLabel = new Label { Left = 15, Top = 114, Width = 130, Height = 22, ForeColor = Color.Gainsboro, Text = "Sensibilidade: 1.0" };
        Add(sensLabel);
        sens = new TrackBar { Left = 150, Top = 110, Width = 250, Minimum = 2, Maximum = 30, Value = 10, TickFrequency = 2 };
        sens.Scroll += delegate
        {
            Ds4.Sens = sens.Value / 10.0;
            sensLabel.Text = "Sensibilidade: " + Ds4.Sens.ToString("0.0");
        };
        Add(sens);

        cTap = new CheckBox { Left = 15, Top = 148, Width = 170, Height = 24, Text = "Toque rapido = clique", ForeColor = Color.Gainsboro, Checked = true };
        cTap.CheckedChanged += delegate { Ds4.TapEnabled = cTap.Checked; };
        Add(cTap);

        cScroll = new CheckBox { Left = 200, Top = 148, Width = 200, Height = 24, Text = "2 dedos = rolagem", ForeColor = Color.Gainsboro, Checked = true };
        cScroll.CheckedChanged += delegate { Ds4.ScrollEnabled = cScroll.Checked; };
        Add(cScroll);

        hint = new Label
        {
            Left = 15, Top = 182, Width = 385, Height = 76, ForeColor = Color.Gray,
            Text = "Ctrl+Alt+M liga/desliga na hora (mesmo em jogo).\r\n" +
                   "O controle NUNCA e remapeado: jogos veem o DualShock normal.\r\n" +
                   "Com desligado, nada de mouse e injetado no sistema."
        };
        Add(hint);

        reconnect = new Button { Left = 15, Top = 262, Width = 130, Height = 32, Text = "Reconectar", BackColor = Color.FromArgb(60, 63, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        reconnect.Click += delegate { StartEngine(true); };
        Add(reconnect);

        timer = new System.Windows.Forms.Timer { Interval = 400 };
        timer.Tick += delegate { RefreshStatus(); };
        timer.Start();

        Load += delegate { StartEngine(false); };
    }

    void Add(Control c) { Controls.Add(c); }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_M);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, HOTKEY_ID);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) Toggle();
        base.WndProc(ref m);
    }

    void Toggle()
    {
        if (!running) { StartEngine(true); return; }
        Ds4.Enabled = !Ds4.Enabled;
        if (!Ds4.Enabled) Ds4.ForceRelease();
        RefreshStatus();
    }

    void StartEngine(bool force)
    {
        if (running && !force) return;
        if (running) StopEngine();

        string path = FindController();
        if (path == null)
        {
            status.Text = "Controle PS4 nao encontrado. Pareie via Bluetooth.";
            RefreshStatus();
            return;
        }
        Ds4.IsBt = path.IndexOf("HID#{") >= 0;   // BT vem de {00001124...}; USB vem de VID_054C
        int err = Ds4.Open(path);
        if (err != 0)
        {
            status.Text = "Erro ao abrir dispositivo (Win32 " + err + "). Feche Steam e Reconectar.";
            return;
        }
        Ds4.Stop = false;
        Ds4.Enabled = false;
        eng = new Thread(Ds4.Loop);
        eng.IsBackground = true;
        eng.Start();
        running = true;
        RefreshStatus();
    }

    void StopEngine()
    {
        Ds4.Stop = true;
        Ds4.Enabled = false;
        Ds4.ForceRelease();
        if (eng != null && eng.IsAlive) eng.Join(500);
        Ds4.Close();
        running = false;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        StopEngine();
        base.OnFormClosed(e);
    }

    // Monta o path de interface HID a partir do DeviceID (WMI = mesmo dado do Get-PnpDevice).
    static string FindController()
    {
        string usb = null, bt = null;
        try
        {
            using (var s = new ManagementObjectSearcher(
                "SELECT DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE PNPClass='HIDClass'"))
            {
                foreach (var o in s.Get())
                {
                    string id = o["DeviceID"] as string;
                    object ec = o["ConfigManagerErrorCode"];
                    if (id == null || (ec != null && (uint)ec != 0)) continue;
                    if (id.IndexOf("054C") < 0 || id.IndexOf("09CC") < 0) continue;
                    if (id.StartsWith("HID\\{")) bt = id; else usb = id;
                }
            }
        }
        catch { }
        string inst = usb ?? bt;
        if (inst == null) return null;
        return "\\\\?\\" + inst.Replace('\\', '#') + "#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    }

    void RefreshStatus()
    {
        if (!running)
        {
            if (status.Text.IndexOf("Erro") < 0 && status.Text.IndexOf("nao encontr") < 0)
                status.Text = "Parado.";
            toggle.Text = "ATIVAR  (Ctrl+Alt+M)";
            toggle.BackColor = Color.FromArgb(60, 63, 68);
            return;
        }
        string st = Ds4.Disconnected ? "DESCONECTADO - Reconectar" :
                    (Ds4.FullSeen ? "Conectado - modo completo (touchpad ativo)" : "Conectado - aguardando modo completo");
        status.Text = st + "\r\nRelatorios: 0x11=" + Ds4.N11 + "  0x01=" + Ds4.N01;
        if (Ds4.Enabled)
        {
            toggle.Text = "DESLIVAR  (Ctrl+Alt+M)";
            toggle.BackColor = Color.FromArgb(0, 120, 60);
        }
        else
        {
            toggle.Text = "ATIVAR  (Ctrl+Alt+M)";
            toggle.BackColor = Color.FromArgb(60, 63, 68);
        }
    }
}
