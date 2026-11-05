using System;
using System.Reflection;

static class EngineChecks
{
    static readonly MethodInfo process = typeof(Ds4).GetMethod("Process", BindingFlags.NonPublic | BindingFlags.Static);
    static byte[] Report(bool touch, bool pressed, bool right, bool two, int x, int y)
    {
        var report = new byte[64]; report[0] = 1; report[33] = 1;
        report[35] = touch ? (byte)0 : (byte)128;
        report[36] = (byte)x; report[37] = (byte)((x >> 8) | ((y & 15) << 4)); report[38] = (byte)(y >> 4);
        report[39] = two ? (byte)1 : (byte)128;
        report[6] = right ? (byte)1 : (byte)0; report[7] = pressed ? (byte)2 : (byte)0;
        return report;
    }
    static void Feed(bool touch, bool pressed, bool right, bool two, int x, int y)
    { var report = Report(touch, pressed, right, two, x, y); process.Invoke(null, new object[] { report, report.Length }); }
    static void Reset()
    { Ds4.SetEnabled(false); Ds4.Events.Clear(); Ds4.MoveX.Clear(); Ds4.SetMotionSettings(1, 0); Ds4.IsBt = false; Ds4.SetEnabled(true); }
    static void ExpectDistance(string label, int expected)
    {
        if (Ds4.MoveX.Count != 1 || Ds4.MoveX[0] != expected) throw new Exception(label + ": distância incorreta.");
        Console.WriteLine("PASS: " + label);
    }
    static void Elapsed(double seconds)
    {
        typeof(Ds4).GetField("lastMoveTicks", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null,
            System.Diagnostics.Stopwatch.GetTimestamp() - (long)(System.Diagnostics.Stopwatch.Frequency * seconds));
    }
    static void Expect(string label, params uint[] expected)
    {
        if (Ds4.Events.Count != expected.Length) throw new Exception(label + ": quantidade incorreta de eventos: " + Ds4.Events.Count);
        for (int i = 0; i < expected.Length; i++) if (Ds4.Events[i] != expected[i]) throw new Exception(label + ": evento incorreto.");
        Console.WriteLine("PASS: " + label);
    }
    static int Main()
    {
        Reset(); Feed(true, false, false, false, 100, 100); Feed(false, false, false, false, 100, 100);
        Expect("toque simples", Ds4.F_LDN, Ds4.F_LUP);
        Reset(); Feed(true, false, true, false, 100, 100); Feed(false, false, true, false, 100, 100);
        Expect("L1 + toque", Ds4.F_RDN, Ds4.F_RUP);
        Reset(); Feed(true, false, false, true, 100, 100); Feed(false, false, false, false, 100, 100);
        Expect("dois dedos não geram clique");
        Reset(); Feed(true, false, false, false, 100, 100); Feed(true, false, false, false, 150, 110); Feed(false, false, false, false, 150, 110);
        Expect("arrasto move sem clique por toque", Ds4.F_MOVE);
        Reset(); Feed(false, true, false, false, 0, 0); Ds4.SetEnabled(false);
        Expect("desativar libera clique físico", Ds4.F_LDN, Ds4.F_LUP);
        Reset(); Feed(true, false, false, true, 100, 100); Feed(true, false, false, true, 100, 140);
        Expect("dois dedos rolam", Ds4.F_WHEEL);
        Reset(); Ds4.SetMotionSettings(2, 0); Feed(true, false, false, false, 100, 100); Feed(true, false, false, false, 150, 100);
        ExpectDistance("sensibilidade dobra o deslocamento do PS4", 100);
        Reset(); Ds4.SetMotionSettings(1, 2); Feed(true, false, false, false, 100, 100); Elapsed(1); Feed(true, false, false, false, 110, 100);
        ExpectDistance("gesto lento mantém precisão com aceleração ligada", 10);
        Reset(); Ds4.SetMotionSettings(1, 2); Feed(true, false, false, false, 100, 100); Elapsed(.001); Feed(true, false, false, false, 1100, 100);
        ExpectDistance("gesto rápido recebe o ganho máximo", 3000);
        Reset(); Ds4.SetMotionSettings(1, 0); Feed(true, false, false, false, 100, 100); Elapsed(.001); Feed(true, false, false, false, 1100, 100);
        ExpectDistance("velocidade extra desligada mantém movimento uniforme", 1000);
        Reset(); Ds4.SetMotionSettings(double.NaN, double.PositiveInfinity); Feed(true, false, false, false, 100, 100); Feed(true, false, false, false, 150, 100);
        ExpectDistance("valores inválidos usam padrão seguro", 50);
        Reset(); Ds4.SetMotionSettings(.2, 0); Feed(true, false, false, false, 100, 100);
        for (int x = 101; x <= 105; x++) Feed(true, false, false, false, x, 100);
        ExpectDistance("baixa sensibilidade preserva movimentos fracionários", 1);
        Ds4.SetEnabled(false);
        return 0;
    }
}
