namespace LiveForge_Studio.Models;

public sealed class WorkspaceState
{
    public int SchemaVersion { get; set; } = 1;
    public string ProductName { get; set; } = "LiveForge Studio";
    public string ProfileName { get; set; } = "Default Profile";
    public string CollectionName { get; set; } = "Default Collection";
    public string CanvasFormat { get; set; } = "1920 × 1080 · 30 fps";
    public List<SceneState> Scenes { get; set; } = [];
    public List<SlideState> Slides { get; set; } = [];
    public int SelectedSceneIndex { get; set; }
    public int SelectedSlideIndex { get; set; }
}

public sealed class SceneState
{
    public string Name { get; set; } = "Scene";
}

public sealed class SlideState
{
    public string Text { get; set; } = string.Empty;
}
