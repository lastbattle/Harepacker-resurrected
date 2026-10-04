using System;
using System.IO;
using HaSharedLibrary.Audio;

namespace UnitTest_AudioEditor;

public sealed class AudioProjectTests
{
    [Fact]
    public void ProjectJsonRoundTripDoesNotContainDecodedSamples()
    {
        var project = AudioProject.Create("Round trip", "modern");
        var track = project.AddTrack("BGM", AudioTrackRole.Music);
        track.AddClip(new AudioSourceReference
        {
            SourceKind = AudioSourceKind.NativeWz,
            ImagePath = "Sound/Bgm00.img",
            PropertyPath = "GoPicnic",
            ContentHash = "abc",
        }, startSample: 123, durationSample: 456);

        var json = AudioProjectSerializer.Serialize(project);
        var loaded = AudioProjectSerializer.Deserialize(json);
        Assert.Equal(project.ProjectId, loaded.ProjectId);
        Assert.Equal("GoPicnic", loaded.Tracks[0].Clips[0].SourceReference.PropertyPath);
        Assert.DoesNotContain("Samples", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewerSchemaIsRejected()
    {
        var json = "{\"schemaVersion\":999,\"projectId\":\"" + Guid.NewGuid() + "\"}";
        var exception = Assert.Throws<AudioProjectSchemaException>(() => AudioProjectSerializer.Deserialize(json));
        Assert.Equal(999, exception.SchemaVersion);
    }

    [Fact]
    public void UndoRedoRestoresClipGain()
    {
        var project = AudioProject.Create();
        var track = project.AddTrack();
        var clip = track.AddClip(new AudioSourceReference { SourceKind = AudioSourceKind.ExternalFile, ExternalPath = "a.wav" }, 0, 8);
        var editor = new AudioProjectEditor(project);

        editor.SetClipGain(clip.Id, 0.25);
        Assert.Equal(0.25, project.FindClip(clip.Id)!.Gain);
        Assert.True(editor.History.Undo());
        Assert.Equal(1, project.FindClip(clip.Id)!.Gain);
        Assert.True(editor.History.Redo());
        Assert.Equal(0.25, project.FindClip(clip.Id)!.Gain);
    }

    [Fact]
    public async Task AutosaveCanRecoverNewerProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hasound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "project.hasound.json");
            var project = AudioProject.Create("autosave");
            var service = new AudioProjectAutosaveService();
            await service.SaveAsync(project, projectPath);
            Assert.True(service.HasRecovery(projectPath));
            Assert.Equal("autosave", service.TryRecover(projectPath)!.Title);
            service.DiscardRecovery(projectPath);
            Assert.False(File.Exists(service.GetAutosavePath(projectPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
