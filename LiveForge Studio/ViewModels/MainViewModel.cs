using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LiveForge_Studio.Infrastructure;
using LiveForge_Studio.Models;

namespace LiveForge_Studio.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceStore _store;
    private readonly DispatcherTimer _saveTimer;
    private readonly RelayCommand _deleteSceneCommand;
    private readonly RelayCommand _deleteSlideCommand;
    private string _productName;
    private string _profileName;
    private string _collectionName;
    private string _canvasFormat;
    private SceneViewModel _selectedScene;
    private SlideViewModel? _selectedSlide;
    private string _saveStatus = "Workspace ready";
    private bool _isDirty;
    private bool _disposed;

    public MainViewModel(WorkspaceStore? store = null)
    {
        _store = store ?? new WorkspaceStore();
        var state = _store.Load();
        _productName = state.ProductName;
        _profileName = state.ProfileName;
        _collectionName = state.CollectionName;
        _canvasFormat = state.CanvasFormat;
        Scenes = new ObservableCollection<SceneViewModel>(state.Scenes.Select(scene => new SceneViewModel(scene.Name)));
        Slides = new ObservableCollection<SlideViewModel>(state.Slides.Select(slide => new SlideViewModel(slide.Text)));
        _selectedScene = Scenes[Math.Clamp(state.SelectedSceneIndex, 0, Scenes.Count - 1)];
        _selectedScene.IsSelected = true;
        _selectedSlide = Slides.Count == 0 ? null : Slides[Math.Clamp(state.SelectedSlideIndex, 0, Slides.Count - 1)];
        UpdateSceneNumbers();

        foreach (var scene in Scenes)
        {
            scene.PropertyChanged += OnWorkspaceItemChanged;
        }

        foreach (var slide in Slides)
        {
            slide.PropertyChanged += OnWorkspaceItemChanged;
        }

        Scenes.CollectionChanged += (_, _) =>
        {
            _deleteSceneCommand.RaiseCanExecuteChanged();
            UpdateSceneNumbers();
            OnPropertyChanged(nameof(Scenes));
            RequestSave();
        };
        Slides.CollectionChanged += (_, _) =>
        {
            _deleteSlideCommand.RaiseCanExecuteChanged();
            UpdateSlideCommands();
            OnPropertyChanged(nameof(PresentationStatus));
            RequestSave();
        };

        _deleteSceneCommand = new RelayCommand(_ => DeleteSelectedScene(), _ => Scenes.Count > 1);
        _deleteSlideCommand = new RelayCommand(_ => DeleteSelectedSlide(), _ => SelectedSlide is not null);
        AddSceneCommand = new RelayCommand(_ => AddScene());
        DeleteSceneCommand = _deleteSceneCommand;
        SelectSceneCommand = new RelayCommand(parameter => SelectScene(parameter as SceneViewModel));
        AddSlideCommand = new RelayCommand(_ => AddSlide());
        DeleteSlideCommand = _deleteSlideCommand;
        PreviousSlideCommand = new RelayCommand(_ => MoveSlide(-1), _ => Slides.Count > 0 && SelectedSlide is not null);
        NextSlideCommand = new RelayCommand(_ => MoveSlide(1), _ => Slides.Count > 0 && SelectedSlide is not null);
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _saveTimer.Tick += (_, _) => SavePendingChanges();
    }

    public ObservableCollection<SceneViewModel> Scenes { get; }
    public ObservableCollection<SlideViewModel> Slides { get; }
    public ICommand AddSceneCommand { get; }
    public ICommand DeleteSceneCommand { get; }
    public ICommand SelectSceneCommand { get; }
    public ICommand AddSlideCommand { get; }
    public ICommand DeleteSlideCommand { get; }
    public ICommand PreviousSlideCommand { get; }
    public ICommand NextSlideCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public string ProductName
    {
        get => _productName;
        private set => SetProperty(ref _productName, value);
    }

    public string ProfileName
    {
        get => _profileName;
        private set => SetProperty(ref _profileName, value);
    }

    public string CollectionName
    {
        get => _collectionName;
        private set => SetProperty(ref _collectionName, value);
    }

    public string CanvasFormat
    {
        get => _canvasFormat;
        private set => SetProperty(ref _canvasFormat, value);
    }

    public SceneViewModel SelectedScene
    {
        get => _selectedScene;
        private set
        {
            if (ReferenceEquals(_selectedScene, value))
            {
                return;
            }

            _selectedScene.IsSelected = false;
            _selectedScene = value;
            _selectedScene.IsSelected = true;
            OnPropertyChanged();
            RequestSave();
        }
    }

    public SlideViewModel? SelectedSlide
    {
        get => _selectedSlide;
        set
        {
            if (!SetProperty(ref _selectedSlide, value))
            {
                return;
            }

            _deleteSlideCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(PresentationStatus));
            RequestSave();
        }
    }

    public string PresentationStatus => Slides.Count == 0
        ? "No slides in this presentation."
        : $"Slide {Slides.IndexOf(SelectedSlide!) + 1} of {Slides.Count}";

    public string SaveStatus
    {
        get => _saveStatus;
        private set => SetProperty(ref _saveStatus, value);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _saveTimer.Stop();
        SavePendingChanges();
    }

    private void AddScene()
    {
        var scene = new SceneViewModel(FindUniqueSceneName());
        scene.PropertyChanged += OnWorkspaceItemChanged;
        Scenes.Add(scene);
        SelectScene(scene);
    }

    private string FindUniqueSceneName()
    {
        var number = 1;
        while (Scenes.Any(scene => string.Equals(scene.Name, $"Scene {number}", StringComparison.OrdinalIgnoreCase)))
        {
            number++;
        }

        return $"Scene {number}";
    }

    private void DeleteSelectedScene()
    {
        if (Scenes.Count <= 1)
        {
            return;
        }

        var removedIndex = Scenes.IndexOf(SelectedScene);
        SelectedScene.PropertyChanged -= OnWorkspaceItemChanged;
        Scenes.Remove(SelectedScene);
        SelectedScene = Scenes[Math.Min(removedIndex, Scenes.Count - 1)];
    }

    private void SelectScene(SceneViewModel? scene)
    {
        if (scene is not null && Scenes.Contains(scene))
        {
            SelectedScene = scene;
        }
    }

    private void AddSlide()
    {
        var slide = new SlideViewModel("New slide");
        slide.PropertyChanged += OnWorkspaceItemChanged;
        Slides.Add(slide);
        SelectedSlide = slide;
    }

    private void DeleteSelectedSlide()
    {
        if (SelectedSlide is null)
        {
            return;
        }

        var removedIndex = Slides.IndexOf(SelectedSlide);
        SelectedSlide.PropertyChanged -= OnWorkspaceItemChanged;
        Slides.Remove(SelectedSlide);
        SelectedSlide = Slides.Count == 0 ? null : Slides[Math.Min(removedIndex, Slides.Count - 1)];
        UpdateSlideCommands();
    }

    private void MoveSlide(int offset)
    {
        if (SelectedSlide is null || Slides.Count == 0)
        {
            return;
        }

        var index = Slides.IndexOf(SelectedSlide);
        SelectedSlide = Slides[(index + offset + Slides.Count) % Slides.Count];
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(ProductName, CanvasFormat)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ProductName = dialog.ProductNameValue;
        CanvasFormat = dialog.CanvasFormatValue;
        RequestSave();
    }

    private void OnWorkspaceItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is SceneViewModel && e.PropertyName == nameof(SceneViewModel.Name) ||
            sender is SlideViewModel && e.PropertyName == nameof(SlideViewModel.Text))
        {
            RequestSave();
        }
    }

    private void RequestSave()
    {
        if (_disposed)
        {
            return;
        }

        _isDirty = true;
        SaveStatus = "Unsaved changes";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SavePendingChanges()
    {
        _saveTimer.Stop();
        if (!_isDirty)
        {
            return;
        }

        var state = new WorkspaceState
        {
            ProductName = ProductName,
            ProfileName = ProfileName,
            CollectionName = CollectionName,
            CanvasFormat = CanvasFormat,
            Scenes = Scenes.Select(scene => new SceneState { Name = scene.Name }).ToList(),
            Slides = Slides.Select(slide => new SlideState { Text = slide.Text }).ToList(),
            SelectedSceneIndex = Math.Max(0, Scenes.IndexOf(SelectedScene)),
            SelectedSlideIndex = SelectedSlide is null ? 0 : Math.Max(0, Slides.IndexOf(SelectedSlide))
        };

        try
        {
            _store.Save(state);
            _isDirty = false;
            SaveStatus = "Workspace saved";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Trace.TraceError("Workspace save failed for {0}: {1}", _store.WorkspacePath, exception);
            SaveStatus = "Save failed — check local application data permissions";
        }
    }

    private void UpdateSlideCommands()
    {
        ((RelayCommand)NextSlideCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PreviousSlideCommand).RaiseCanExecuteChanged();
    }

    private void UpdateSceneNumbers()
    {
        for (var index = 0; index < Scenes.Count; index++)
        {
            Scenes[index].Number = (index + 1).ToString("00");
        }
    }
}

public sealed class SceneViewModel(string name) : ObservableObject
{
    private string _name = name;
    private bool _isSelected;
    private string _number = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Untitled scene" : value.Trim();
            SetProperty(ref _name, normalized.Length > 80 ? normalized[..80] : normalized);
        }
    }

    public string SourceSummary => Sources.Count == 0 ? "No sources" : $"{Sources.Count} source(s)";
    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    public string Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SelectionBackground));
            }
        }
    }

    public string SelectionBackground => IsSelected ? "#26372F" : "#00000000";
}

public sealed class SlideViewModel(string text) : ObservableObject
{
    private string _text = text;

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value.Length > 1000 ? value[..1000] : value);
    }
}

public sealed class SourceItemViewModel(string name, string type) : ObservableObject
{
    public string Name { get; } = name;
    public string Type { get; } = type;
}
