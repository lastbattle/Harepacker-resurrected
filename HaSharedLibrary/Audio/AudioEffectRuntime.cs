using System;
using System.Collections.Generic;

namespace HaSharedLibrary.Audio;

/// <summary>Stateful sample processor used by the offline render graph.</summary>
internal sealed class EffectRuntime
{
    private readonly IReadOnlyList<AudioEffectNode> nodes;
    private readonly int sampleRate;
    private readonly Dictionary<AudioEffectNode, float[][]> delays = new();
    private readonly Dictionary<AudioEffectNode, int> delayPositions = new();

    public EffectRuntime(IReadOnlyList<AudioEffectNode> nodes, int sampleRate)
    { this.nodes = nodes; this.sampleRate = Math.Max(1, sampleRate); }

    public void Process(float[] sample, long position)
    {
        foreach (var node in nodes)
        {
            if (node.Bypass) continue;
            var dry = (float[])sample.Clone();
            var type = node.Type.Trim().ToLowerInvariant();
            var wet = Math.Clamp(node.WetDry, 0, 1);
            switch (type)
            {
                case "gain": Scale(sample, Get(node, "gain", 1)); break;
                case "limiter": ScaleAndLimit(sample, Get(node, "ceiling", 1)); break;
                case "compressor": Compress(sample, (float)Get(node, "threshold", .5), (float)Get(node, "ratio", 4)); break;
                case "gate": Gate(sample, (float)Get(node, "threshold", .01)); break;
                case "delay": Delay(sample, node); break;
                case "distortion": Distort(sample, (float)Get(node, "drive", 2)); break;
                case "pan": Pan(sample, (float)Get(node, "pan", 0)); break;
                case "lowpass": case "highpass": Filter(sample, node, type == "highpass"); break;
                case "dc offset": case "dcoffset": break;
                case "mute": Array.Clear(sample, 0, sample.Length); break;
            }
            if (wet < 1) for (var i = 0; i < sample.Length; i++) sample[i] = dry[i] * (1 - (float)wet) + sample[i] * (float)wet;
        }
    }

    private static double Get(AudioEffectNode n, string key, double fallback) => n.Parameters.TryGetValue(key, out var v) ? v : fallback;
    private static void Scale(float[] s, double g) { for (var i = 0; i < s.Length; i++) s[i] *= (float)g; }
    private static void ScaleAndLimit(float[] s, double c) { c = Math.Abs(c); Scale(s, 1); for (var i = 0; i < s.Length; i++) s[i] = Math.Clamp(s[i], (float)-c, (float)c); }
    private static void Compress(float[] s, float t, float r) { t = Math.Abs(t); r = Math.Max(1, r); for (var i = 0; i < s.Length; i++) { var a = Math.Abs(s[i]); if (a > t) a = t + (a - t) / r; s[i] = MathF.CopySign(a, s[i]); } }
    private static void Gate(float[] s, float t) { for (var i = 0; i < s.Length; i++) if (Math.Abs(s[i]) < t) s[i] = 0; }
    private static void Distort(float[] s, float d) { d = Math.Max(1, d); for (var i = 0; i < s.Length; i++) s[i] = MathF.Tanh(s[i] * d); }
    private static void Pan(float[] s, float p) { if (s.Length < 2) return; p = Math.Clamp(p, -1, 1); var l = MathF.Sqrt((1-p)*.5f); var r = MathF.Sqrt((1+p)*.5f); s[0] *= l; s[1] *= r; }
    private void Delay(float[] s, AudioEffectNode n) { var len = Math.Max(1, (int)Math.Round(Get(n, "samples", sampleRate * Get(n, "milliseconds", 250) / 1000))); if (!delays.TryGetValue(n, out var b) || b[0].Length != len) { b = new float[s.Length][]; for (var c=0;c<s.Length;c++) b[c]=new float[len]; delays[n]=b; delayPositions[n]=0; } var p=delayPositions[n]; var wet=(float)Math.Clamp(Get(n,"wet",.5),0,1); var fb=(float)Math.Clamp(Get(n,"feedback",0),0,.99); for(var c=0;c<s.Length;c++){var old=b[c][p]; b[c][p]=s[c]+old*fb; s[c]=s[c]*(1-wet)+old*wet;} delayPositions[n]=(p+1)%len; }
    private void Filter(float[] s, AudioEffectNode n, bool high) { var cutoff=Math.Clamp(Get(n,"cutoff",1000),20,sampleRate/2.0); var a=(float)Math.Exp(-2*Math.PI*cutoff/sampleRate); for(var i=0;i<s.Length;i++){var prev=s[i]; var low=prev*(1-a); s[i]=high?prev-low:low;} }
}
