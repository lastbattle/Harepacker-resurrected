using System.Collections;
using System.IO;
using System.Globalization;
using System.Reflection;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace UnitTest_MapleGame;

[Collection("Game session host")]
public sealed class AudioRelaunchTests
{
    [AudioFact]
    public async Task BgmAndSoundEffectPlaybackSurviveTwoDisposedGames()
    {
        string wavPath = CreateTemporaryWaveFile();
        try
        {
            var session = new AudioRelaunchSession(wavPath);
            Assert.True(GameSessionHost.TryStart(() => session, out GameSessionHandle handle));

            GameSessionResult result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Null(result.Error);
            Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
            Assert.Equal(2, session.CycleCount);
            Assert.All(session.BgmSampleDeltas, delta => Assert.True(delta > 0));
            Assert.All(session.SoundEffectSampleDeltas, delta => Assert.True(delta > 0));
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    private static string CreateTemporaryWaveFile()
    {
        const int sampleRate = 44_100;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int sampleCount = sampleRate * 2;
        int blockAlign = channels * (bitsPerSample / 8);
        int dataLength = sampleCount * blockAlign;
        string path = Path.Combine(
            Path.GetTempPath(),
            $"MapleGameAudio_{Guid.NewGuid():N}.wav");

        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataLength);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * blockAlign);
            writer.Write((short)blockAlign);
            writer.Write(bitsPerSample);
            writer.Write("data"u8.ToArray());
            writer.Write(dataLength);

            for (int sample = 0; sample < sampleCount; sample++)
            {
                double phase = 2d * Math.PI * 440d * sample / sampleRate;
                writer.Write((short)(Math.Sin(phase) * short.MaxValue * 0.2));
            }
        }

        return path;
    }

    private sealed class AudioRelaunchSession : IGameSession
    {
        private readonly string wavePath;

        public AudioRelaunchSession(string wavePath)
        {
            this.wavePath = wavePath;
        }

        public int CycleCount { get; private set; }
        public List<long> BgmSampleDeltas { get; } = new();
        public List<long> SoundEffectSampleDeltas { get; } = new();

        public void Run(CancellationToken cancellationToken)
        {
            for (int cycle = 0; cycle < 2; cycle++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var game = new Game();
                using var sound = new WzBinaryProperty("relaunch", wavePath);
                using var bgm = new MonoGameBgmPlayer(sound, looped: true, volume: 0f);
                using var soundManager = new SoundManager();

                soundManager.RegisterSound("relaunch-sfx", sound);
                bgm.Play();
                soundManager.Volume = 0f;
                soundManager.PlaySound("relaunch-sfx");

                SoundEffectInstance bgmInstance = GetPrivateInstance(bgm, "_instance");
                SoundEffectInstance sfxInstance = GetActiveSoundManagerInstance(soundManager);
                long bgmStart = ReadSamplesPlayed(bgmInstance);
                long sfxStart = ReadSamplesPlayed(sfxInstance);

                WaitForSampleAdvance(bgmInstance, bgmStart);
                WaitForSampleAdvance(sfxInstance, sfxStart);

                BgmSampleDeltas.Add(ReadSamplesPlayed(bgmInstance) - bgmStart);
                SoundEffectSampleDeltas.Add(ReadSamplesPlayed(sfxInstance) - sfxStart);
                CycleCount++;
            }
        }

        public void Dispose()
        {
        }
    }

    private static SoundEffectInstance GetActiveSoundManagerInstance(SoundManager manager)
    {
        FieldInfo activeSoundsField = typeof(SoundManager).GetField(
            "_activeSounds",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(SoundManager).FullName, "_activeSounds");
        var activeSounds = (IList)activeSoundsField.GetValue(manager)!;
        Assert.NotEmpty(activeSounds);
        object oneShot = activeSounds[0]!;
        return GetPrivateInstance(oneShot, "_instance");
    }

    private static SoundEffectInstance GetPrivateInstance(object owner, string fieldName)
    {
        FieldInfo field = FindField(owner.GetType(), fieldName)
            ?? throw new MissingFieldException(owner.GetType().FullName, fieldName);
        return Assert.IsType<SoundEffectInstance>(field.GetValue(owner));
    }

    private static long ReadSamplesPlayed(SoundEffectInstance instance)
    {
        object voice = FindField(instance.GetType(), "_voice")?.GetValue(instance)
            ?? throw new InvalidOperationException("MonoGame did not create a native source voice.");
        PropertyInfo stateProperty = voice.GetType().GetProperty(
            "State",
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(voice.GetType().FullName, "State");
        object state = stateProperty.GetValue(voice)
            ?? throw new InvalidOperationException("Native source voice returned no state.");
        object samples = state.GetType().GetProperty("SamplesPlayed")?.GetValue(state)
            ?? state.GetType().GetField("SamplesPlayed")?.GetValue(state)
            ?? throw new MissingMemberException(state.GetType().FullName, "SamplesPlayed");
        return Convert.ToInt64(samples, CultureInfo.InvariantCulture);
    }

    private static void WaitForSampleAdvance(SoundEffectInstance instance, long start)
    {
        if (!SpinWait.SpinUntil(
                () => ReadSamplesPlayed(instance) > start,
                TimeSpan.FromSeconds(3)))
        {
            throw new Xunit.Sdk.XunitException("Native source voice SamplesPlayed did not advance.");
        }
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
                return field;
        }

        return null;
    }

    private sealed class AudioFactAttribute : FactAttribute
    {
        public AudioFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("MAPLEGAME_AUDIO_TESTS"),
                    "1",
                    StringComparison.Ordinal))
            {
                Skip = "Set MAPLEGAME_AUDIO_TESTS=1 to run the native MonoGame audio relaunch test.";
            }
        }
    }
}
