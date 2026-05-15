using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.TextInput;
using Avalonia.Threading;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using DialogHostAvalonia;
using Rizor.Core.ViewModels;
using TextMateSharp.Grammars;

namespace Rizor.Core.Views;

public partial class MainView : UserControl
{
    private TopLevel? _topLevel;
    private IStorageFile? _fileOpened;
    private Language? _language;
    private readonly RegistryOptions _registryOptions;
    private readonly TextMate.Installation _textMateInstallation;
    private readonly FoldingManager _foldingManager;
    private readonly BraceFoldingStrategy _braceFoldingStrategy;
    private readonly XmlFoldingStrategy _xmlFoldingStrategy;
    private readonly IndentFoldingStrategy _indentFoldingStrategy;
    private readonly DispatcherTimer _dispatcherTimer;
    private bool _saveAs;
    private bool _hasFileChanged;
    private bool _changedWithCode;

    public MainView()
    {
        InitializeComponent();

        // Setting up something
        _foldingManager = FoldingManager.Install(Editor.TextArea);
        _braceFoldingStrategy = new BraceFoldingStrategy();
        _xmlFoldingStrategy = new XmlFoldingStrategy();
        _indentFoldingStrategy = new IndentFoldingStrategy();

        // Setting up syntax highlighting
        _registryOptions = new RegistryOptions(ThemeName.Dracula);
        _textMateInstallation = Editor.InstallTextMate(_registryOptions);

        // Editor Options
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.IndentationSize = 2;

        // Subscribing to events
        Editor.Document.UndoStack.PropertyChanged += OnUndoPropertyChanged;
        Editor.Document.TextChanged += Editor_TextChanged;
        Editor.TextArea.PointerPressed += (_, _) =>
        {
            if (OperatingSystem.IsAndroid())
            {
                _topLevel?.FocusManager.Focus(null);
                Editor.TextArea.Focus();
            }
        };

        // Setting up timer to update text folding
        _dispatcherTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(0.5),
            DispatcherPriority.Background,
            TimerTick);
        _dispatcherTimer.Start();

        // Setting Text Input Options for Android ig
        TextInputOptions.SetContentType(Editor.TextArea, TextInputContentType.Normal);
        TextInputOptions.SetMultiline(Editor.TextArea, true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // hlo
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);

        if (_topLevel is Window window) window.Closing += OnExit;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is Window window) window.Closing -= OnExit;
        base.OnDetachedFromVisualTree(e);
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        // tick-tock
        _dispatcherTimer.Stop();
        _dispatcherTimer.Start();
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        // hi
        _dispatcherTimer.Stop();
        SetFoldingStrategies();
    }

    private void SetFoldingStrategies()
    {
        if (_language?.Id is "xml" or "html")
            _xmlFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        else if (_language?.Id is "python" or "yaml")
            _indentFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        else
            _braceFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
    }

    private void SetGrammar()
    {
        if (_fileOpened == null) return;

        _language = _registryOptions.GetLanguageByExtension(Path.GetExtension(_fileOpened.Path.LocalPath));
        string scopeName = _registryOptions.GetScopeByLanguageId(_language.Id);

        _textMateInstallation.SetGrammar(scopeName);
    }

    private void OnUndoPropertyChanged(object? sender, EventArgs e)
    {
        if (_changedWithCode) return;

        if (StatusBar.Text != string.Empty) ClearStatusBar();

        _hasFileChanged = !Editor.Document.UndoStack.IsOriginalFile;
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (_topLevel is not Window window) return;
        string prefix = _hasFileChanged ? "*" : "";
        string fileName = _fileOpened != null ? _fileOpened.Name : "Untitled";
        window.Title = $"{prefix}{fileName} - Rizor";
    }

    private async Task UpdateFile()
    {
        try
        {
            if (_fileOpened == null) return;

            await using Stream stream = await _fileOpened.OpenWriteAsync();
            await using StreamWriter streamWriter = new(stream);
            await streamWriter.WriteAsync(Editor.Text);
            Editor.Document.UndoStack.MarkAsOriginalFile();
        }
        catch (Exception)
        {
            ShowError("An Error Occured While Saving The File!");
        }
    }

    private async Task SaveFile(string title, string suggestedFileName)
    {
        // self explanatory ig
        if (_fileOpened == null || _saveAs)
        {
            IStorageFile? savedFile = await _topLevel?.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
                SuggestedFileType = FilePickerFileTypes.TextPlain
            })!;
            _saveAs = false;
            if (savedFile == null) return;
            _fileOpened = savedFile;
        }

        SetGrammar();
        await UpdateFile();

        _hasFileChanged = false;

        RefreshTitle();
    }

    private async void OnNew(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_hasFileChanged)
                await SaveChanges();

            ClearStatusBar();

            _changedWithCode = true;

            Editor.Clear();

            _hasFileChanged = false;
            _fileOpened = null;

            RefreshTitle();

            _changedWithCode = false;
        }
        catch (Exception)
        {
            ShowError();
        }
    }

    private async Task SaveChanges()
    {
        try
        {
            string fileName = _fileOpened != null ? _fileOpened.Name : "Untitled";
            IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(
                new MessageBoxStandardParams()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ButtonDefinitions = ButtonEnum.YesNoCancel,
                    ContentTitle = "Unsaved Changes",
                    ContentMessage = $"Save changes in {fileName}",
                    CanResize = false
                });

            ButtonResult result = await box.ShowAsync();

            if (result is ButtonResult.Cancel or ButtonResult.None) return;

            if (result == ButtonResult.Yes)
                await SaveFile("Save File", "untitled");
        }
        catch (Exception)
        {
            ShowError();
        }
    }

    private void OnNewWindow(object? sender, RoutedEventArgs e)
    {
        // oi
        if (_topLevel is Window)
        {
            MainWindow newMainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };

            newMainWindow.Show();
        }
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_topLevel == null) return;

            IReadOnlyList<IStorageFile> fileOpened = await _topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions()
                {
                    Title = "Open File",
                    AllowMultiple = false
                });

            if (fileOpened.Count == 0) return;

            Editor.Clear();
            _fileOpened = fileOpened[0];
            await using Stream stream = await _fileOpened.OpenReadAsync();

            Editor.Load(stream);
            Editor.Document.UndoStack.MarkAsOriginalFile();

            SetGrammar();
            SetFoldingStrategies();

            _changedWithCode = false;
            _hasFileChanged = false;

            RefreshTitle();
        }
        catch (Exception ex)
        {
            switch (ex)
            {
                case UnauthorizedAccessException:
                    ShowError("Permission Denied To Open This File!");
                    break;
            }
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_topLevel == null) return;

            ClearStatusBar();

            await SaveFile("Save File", "untitled");
        }
        catch (Exception)
        {
            ShowError();
        }
    }

    private async void OnSaveAs(object? sender, RoutedEventArgs e)
    {
        // hey
        try
        {
            if (_topLevel == null) return;

            ClearStatusBar();
            _saveAs = true;
            await SaveFile("Save File As", _fileOpened != null ? _fileOpened.Name : "untitled");
        }
        catch (Exception)
        {
            ShowError();
        }
    }

    private async void OnExit(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            // howdy
            if (_topLevel is not Window window || !_hasFileChanged) return;
            e.Cancel = true;
        
            await SaveChanges();

            window.Closing -= OnExit;
            window.Close();
        }
        catch (Exception)
        {
            ShowError();
        }
    }

    private void ClearStatusBar()
    {
        // wassup
        if (StatusBar.Text != string.Empty)
            StatusBar.ClearValue(TextBlock.TextProperty);
    }

// soup
    private void OnUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnCut(object? sender, RoutedEventArgs e) => Editor.Cut();
    private void OnCopy(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void OnPaste(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void OnDelete(object? sender, RoutedEventArgs e) => Editor.SelectedText = "";
    private void OnSelectAll(object? sender, RoutedEventArgs e) => Editor.SelectAll();
    private void ShowError(string err = "An Error Occured!") => StatusBar.Text = err;

    private void EnableWordWrap(object? sender, RoutedEventArgs e)
    {
        // yo
        if (WordWrapCheckBox.IsChecked == null) return;

        WordWrapCheckBox.IsChecked = !WordWrapCheckBox.IsChecked;
        Editor.WordWrap = (bool)WordWrapCheckBox.IsChecked;
    }

    private async void OnChangeFont(object? sender, RoutedEventArgs e)
    {
        // yoi
        try
        {
            await DialogHost.Show(null, MainDialogHost);
        }
        catch (Exception)
        {
            ShowError();
        }
    }
}