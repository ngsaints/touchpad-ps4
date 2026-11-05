using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class Ds4
{
    public static IntPtr Handle = IntPtr.Zero;
    public static bool IsBt = true;
    public static bool Live = true;
    public static double Sens = 1.0;
    public static bool TapEnabled = true;
    public static bool ScrollEnabled = true;
    public static int TapMs = 280;
    public static int TapDist = 24;
    public static int WheelSteps = 36;
    public static volatile bool Stop = false;

    const uint ACC_R = 0x80000000u, ACC_W = 0x40000000u, SH = 3u, OE = 3u;
    public const uint F_MOVE = 0x0001u, F_LDN = 0x0002u, F_LUP = 0x0004u,
                      F_RDN = 0x0008u, F_RUP = 0x0010u, F_WHEEL = 0x0800u;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(string n, uint a, uint s, IntPtr sec, uint d, uint f, IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr h, byte[] b, uint toRead, out uint rd, IntPtr ol);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(IntPtr h, byte[] b, uint wr, out uint wn, IntPtr ol);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("hid.dll")]
    static extern bool HidD_GetFeature(IntPtr h, byte[] b, uint len);
    [DllImport("hid.dll")]
    static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr pd);
    [DllImport("hid.dll")]
    static extern bool HidD_FreePreparsedData(IntPtr pd);
    [DllImport("hid.dll")]
    static extern int HidP_GetCaps(IntPtr pd, out HIDP_CAPS caps);
    [DllImport("user32.dll")]
    static extern uint SendInput(uint n, ref INPUT inp, int sz);

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        public ushort R0, R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R14, R15, R16;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)]
    struct INPU { [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public INPU u; }

    static int INPUT_SIZE = Marshal.SizeOf(typeof(INPUT));

    static void Mouse(int dx, int dy, uint flags, uint data)
    {
        if (!Live) return;
        INPUT i = new INPUT();
        i.type = 0;
        i.u.mi.dx = dx; i.u.mi.dy = dy; i.u.mi.mouseData = data; i.u.mi.dwFlags = flags;
        SendInput(1, ref i, INPUT_SIZE);
    }

    static uint Crc(uint crc, byte[] d, int len)
    {
        for (int i = 0; i < len; i++)
        {
            crc ^= d[i];
            for (int k = 0; k < 8; k++) crc = ((crc & 1) != 0) ? ((crc >> 1) ^ 0xEDB88320u) : (crc >> 1);
        }
        return crc;
    }

    public static int Open(string path)
    {
        Handle = CreateFile(path, ACC_R | ACC_W, SH, IntPtr.Zero, OE, 0, IntPtr.Zero);
        if (Handle == new IntPtr(-1))
            Handle = CreateFile(path, ACC_R, SH, IntPtr.Zero, OE, 0, IntPtr.Zero);
        if (Handle == new IntPtr(-1)) return Marshal.GetLastWin32Error();
        return 0;
    }

    public static void Close()
    {
        if (Handle != IntPtr.Zero && Handle != new IntPtr(-1)) CloseHandle(Handle);
        Handle = IntPtr.Zero;
    }

    static int FeatLen()
    {
        IntPtr pd;
        if (!HidD_GetPreparsedData(Handle, out pd)) return 64;
        HIDP_CAPS c = new HIDP_CAPS();
        int st = HidP_GetCaps(pd, out c);
        HidD_FreePreparsedData(pd);
        if (st < 0 || c.FeatureReportByteLength < 4) return 64;
        return c.FeatureReportByteLength;
    }

    // Ativa o modo completo (relatorios 0x11 com touchpad) via GET feature 0x02/0x05
    // e output report 0x11 com CRC32 (mesmo metodo do Chromium).
    public static string EnableFullReports()
    {
        int len = FeatLen();
        byte[] b = new byte[len];
        b[0] = 0x02; HidD_GetFeature(Handle, b, (uint)len);
        b = new byte[len];
        b[0] = 0x05; HidD_GetFeature(Handle, b, (uint)len);
        byte[] o = new byte[78];
        o[0] = 0x11; o[1] = 0xC0; o[2] = 0x20; o[3] = 0xF1; o[4] = 0x04;
        uint crc = Crc(0xFFFFFFFFu, new byte[] { 0xA2 }, 1);
        crc = ~Crc(crc, o, 74);
        o[74] = (byte)crc; o[75] = (byte)(crc >> 8); o[76] = (byte)(crc >> 16); o[77] = (byte)(crc >> 24);
        uint w;
        if (WriteFile(Handle, o, 78, out w, IntPtr.Zero)) return "output 0x11 enviado (crc ok)";
        return "output 0x11 falhou, erro " + Marshal.GetLastWin32Error();
    }

    // ---- estado do toque ----
    static int lastX, lastY, touchStart, moveAccum;
    static bool hasLast, wasTouch0, padPrev, padRight, padSeen, twoSeen, tapReady;
    static double fx, fy, scrollAcc;
    static StringBuilder ev = new StringBuilder();

    static string Hex(byte[] b, int n)
    {
        StringBuilder sb = new StringBuilder(n * 2);
        for (int i = 0; i < n; i++) sb.Append(b[i].ToString("X2"));
        return sb.ToString();
    }

    static void EndTouch()
    {
        wasTouch0 = false; hasLast = false; tapReady = false;
        twoSeen = false; padSeen = false; scrollAcc = 0;
    }

    // Retorna linha decodificada pro log; eventos de clique vao em ev.
    static string Process(byte[] r, int n)
    {
        ev.Length = 0;
        if (n < 10) return "";
        byte id = r[0];
        int cnt = 0, s0 = 0, x0 = 0, y0 = 0, s1 = 0, x1 = 0, y1 = 0;
        bool l1 = false, pd = false, hasTouch = false;

        if (id == 0x11 && n >= 45)
        {
            cnt = r[35]; s0 = r[37];
            x0 = r[38] | ((r[39] & 0x0F) << 8); y0 = (r[39] >> 4) | (r[40] << 4);
            s1 = r[41];
            x1 = r[42] | ((r[43] & 0x0F) << 8); y1 = (r[43] >> 4) | (r[44] << 4);
            pd = (r[9] & 0x02) != 0; l1 = (r[8] & 0x01) != 0;
            hasTouch = true;
        }
        else if (id == 0x01 && !IsBt && n >= 43)
        {
            cnt = r[33]; s0 = r[35];
            x0 = r[36] | ((r[37] & 0x0F) << 8); y0 = (r[37] >> 4) | (r[38] << 4);
            s1 = r[39];
            x1 = r[40] | ((r[41] & 0x0F) << 8); y1 = (r[41] >> 4) | (r[42] << 4);
            pd = (r[7] & 0x02) != 0; l1 = (r[6] & 0x01) != 0;
            hasTouch = true;
        }
        else if (id == 0x01)
        {
            pd = (r[7] & 0x02) != 0; l1 = (r[6] & 0x01) != 0;
        }
        else return "";

        // clique fisico do touchpad (borda de pressionar/soltar)
        if (pd && !padPrev) { padRight = l1; Mouse(0, 0, padRight ? F_RDN : F_LDN, 0); ev.Append(padRight ? " [dir-down]" : " [esq-down]"); }
        else if (!pd && padPrev) { Mouse(0, 0, padRight ? F_RUP : F_LUP, 0); ev.Append(" [up]"); }
        padPrev = pd;

        if (!hasTouch) { if (wasTouch0) { ev.Append(" [fim-sem-touch]"); EndTouch(); } return ev.ToString(); }

        bool t0 = (s0 & 0x80) == 0 && cnt > 0 && x0 <= 1919 && y0 <= 941;
        bool t1 = (s1 & 0x80) == 0 && x1 <= 1919 && y1 <= 941;

        if (t0)
        {
            if (pd) padSeen = true;
            if (!wasTouch0)
            {
                lastX = x0; lastY = y0; hasLast = true;
                moveAccum = 0; scrollAcc = 0; touchStart = Environment.TickCount;
                tapReady = true; twoSeen = false; padSeen = pd;
                ev.Append(" [down]");
            }
            else if (hasLast)
            {
                int dx = x0 - lastX, dy = y0 - lastY;
                if (t1)
                {
                    twoSeen = true;
                    if (ScrollEnabled && dy != 0)
                    {
                        scrollAcc += (lastY - y0);
                        while (scrollAcc >= WheelSteps) { Mouse(0, 0, F_WHEEL, 120); scrollAcc -= WheelSteps; }
                        while (scrollAcc <= -WheelSteps) { Mouse(0, 0, F_WHEEL, unchecked((uint)(-120))); scrollAcc += WheelSteps; }
                    }
                    moveAccum += Math.Abs(dx) + Math.Abs(dy);
                    lastX = x0; lastY = y0;
                }
                else if (dx != 0 || dy != 0)
                {
                    moveAccum += Math.Abs(dx) + Math.Abs(dy);
                    fx += dx * Sens; fy += dy * Sens;
                    int ix = (int)fx, iy = (int)fy;
                    fx -= ix; fy -= iy;
                    if (ix != 0 || iy != 0) Mouse(ix, iy, F_MOVE, 0);
                    lastX = x0; lastY = y0;
                }
            }
            wasTouch0 = true;
        }
        else if (wasTouch0)
        {
            int dur = Environment.TickCount - touchStart;
            bool doTap = tapReady && TapEnabled && !twoSeen && !padSeen && dur <= TapMs && moveAccum <= TapDist;
            if (doTap)
            {
                Mouse(0, 0, l1 ? F_RDN : F_LDN, 0);
                Mouse(0, 0, l1 ? F_RUP : F_LUP, 0);
                ev.Append(l1 ? " [tap-dir]" : " [tap-esq]");
            }
            else ev.Append(" [up dur=" + dur + " mov=" + moveAccum + "]");
            EndTouch();
        }

        return " cnt=" + cnt + " t0=" + (t0 ? x0 + "," + y0 : "-") + " t1=" + (t1 ? x1 + "," + y1 : "-")
             + " pd=" + (pd ? 1 : 0) + " l1=" + (l1 ? 1 : 0) + ev.ToString();
    }

    public static void Init()
    {
        Console.CancelKeyPress += delegate(object s, ConsoleCancelEventArgs e) { Stop = true; e.Cancel = true; };
    }

    public static int InputReportLength()
    {
        IntPtr pd;
        if (!HidD_GetPreparsedData(Handle, out pd)) return 512;
        HIDP_CAPS c = new HIDP_CAPS();
        HidP_GetCaps(pd, out c);
        HidD_FreePreparsedData(pd);
        int l = c.InputReportByteLength;
        return l < 16 ? 512 : l;
    }

    public static int Run(int ms, bool probe, string logPath)
    {
        Live = !probe;
        StreamWriter sw = null;
        if (probe && logPath != null) { sw = new StreamWriter(logPath, false); sw.AutoFlush = true; }
        byte[] buf = new byte[InputReportLength()];
        int t0 = Environment.TickCount, n01 = 0, n11 = 0, nother = 0, hexLeft = 12;
        int rc = 0;
        try
        {
            while (!Stop)
            {
                if (ms > 0 && Environment.TickCount - t0 >= ms) break;
                uint rd;
                if (!ReadFile(Handle, buf, (uint)buf.Length, out rd, IntPtr.Zero))
                {
                    rc = Marshal.GetLastWin32Error();
                    Console.WriteLine("ReadFile falhou, erro " + rc);
                    break;
                }
                int n = (int)rd;
                if (n < 1) continue;
                byte id = buf[0];
                if (id == 0x01) n01++; else if (id == 0x11) n11++; else nother++;
                string dec = Process(buf, n);
                if (probe && sw != null)
                {
                    sw.Write("id=" + id.ToString("X2") + " n=" + n);
                    if (hexLeft > 0 && (id == 0x11 || id == 0x01)) { sw.Write(" hex=" + Hex(buf, n)); hexLeft--; }
                    sw.WriteLine(dec);
                }
            }
        }
        finally
        {
            if (sw != null) { sw.WriteLine("resumo: 0x01=" + n01 + " 0x11=" + n11 + " outros=" + nother); sw.Close(); }
            Console.WriteLine("resumo: 0x01=" + n01 + " 0x11=" + n11 + " outros=" + nother);
        }
        return rc;
    }
}
