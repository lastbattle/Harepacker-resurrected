using BenchmarkDotNet.Attributes;
using HaSharedLibrary.Audio;
using HaSharedLibrary.Audio.AI;

namespace UnitTest_Perf;

[MemoryDiagnoser]
public class AudioStudioBenchmarks
{
    private AudioBuffer buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        const int sampleRate = 48000;
        var left = new float[sampleRate * 30];
        var right = new float[left.Length];
        for (var i = 0; i < left.Length; i++)
        {
            left[i] = MathF.Sin(i * .013f) * .5f;
            right[i] = MathF.Sin(i * .017f) * .5f;
        }
        buffer = new AudioBuffer(new AudioFormatDescriptor(sampleRate, 2, 32, AudioEncoding.Float32),
            new[] { left, right }, true);
    }

    [Benchmark]
    public AudioWaveformData BuildPeakPyramid() => AudioWaveformData.Build(buffer, "benchmark", 256);

    [Benchmark]
    public AudioBuffer ResampleTo44100() => AudioDsp.Resample(buffer, 44100);

    [Benchmark]
    public AudioAiBrief CompileMapleStoryPrompt() =>
        new AudioAiPromptCompiler().Compile("town bells and warm strings", "Town", "Maple forest", true, 30);
}
