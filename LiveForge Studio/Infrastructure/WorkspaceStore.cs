using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LiveForge_Studio.Models;

namespace LiveForge_Studio.Infrastructure;

public sealed class WorkspaceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _workspacePath;

    public WorkspaceStore(string? workspacePath = null)
    {
        _workspacePath = workspacePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiveForge Studio",
            "workspace.json");
    }

    public string WorkspacePath => _workspacePath;

    public WorkspaceState Load()
    {
        if (!File.Exists(_workspacePath))
        {
            return new WorkspaceState { Scenes = [new SceneState { Name = "Scene 1" }] };
        }

        try
        {
            using var stream = File.OpenRead(_workspacePath);
            var state = JsonSerializer.Deserialize<WorkspaceState>(stream, JsonOptions);
            return Validate(state);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceError("Workspace load failed for {0}: {1}", _workspacePath, exception);
            PreserveCorruptWorkspace();
            return new WorkspaceState { Scenes = [new SceneState { Name = "Scene 1" }] };
        }
    }

    public void Save(WorkspaceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state = Validate(state);
        var directory = Path.GetDirectoryName(_workspacePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"workspace.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _workspacePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void PreserveCorruptWorkspace()
    {
        try
        {
            var backupPath = $"{_workspacePath}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            File.Move(_workspacePath, backupPath, overwrite: false);
            Trace.TraceWarning("Invalid workspace was preserved at {0}", backupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError("Unable to preserve invalid workspace {0}: {1}", _workspacePath, exception);
        }
    }

    private static WorkspaceState Validate(WorkspaceState? state)
    {
        if (state is null || state.SchemaVersion != 1)
        {
            return new WorkspaceState { Scenes = [new SceneState { Name = "Scene 1" }] };
        }

        state.ProductName = Clean(state.ProductName, "LiveForge Studio", 80);
        state.ProfileName = Clean(state.ProfileName, "Default Profile", 80);
        state.CollectionName = Clean(state.CollectionName, "Default Collection", 80);
        state.CanvasFormat = state.CanvasFormat is "1280 × 720 · 30 fps" or "1920 × 1080 · 30 fps" or "1920 × 1080 · 60 fps" or "3840 × 2160 · 30 fps"
            ? state.CanvasFormat
            : "1920 × 1080 · 30 fps";
        state.Scenes = state.Scenes?.Where(scene => scene is not null)
            .Select(scene => new SceneState { Name = Clean(scene.Name, "Scene", 80) })
            .Take(100)
            .ToList() ?? [];
        if (state.Scenes.Count == 0)
        {
            state.Scenes.Add(new SceneState { Name = "Scene 1" });
        }

        state.Slides = state.Slides?.Where(slide => slide is not null)
            .Select(slide => new SlideState { Text = Clean(slide.Text, string.Empty, 1000) })
            .Take(500)
            .ToList() ?? [];
        state.SelectedSceneIndex = Math.Clamp(state.SelectedSceneIndex, 0, state.Scenes.Count - 1);
        state.SelectedSlideIndex = state.Slides.Count == 0 ? 0 : Math.Clamp(state.SelectedSlideIndex, 0, state.Slides.Count - 1);
        return state;
    }

    private static string Clean(string? value, string fallback, int maxLength)
    {
        var clean = value?.Trim();
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean[..Math.Min(clean.Length, maxLength)];
    }
}
