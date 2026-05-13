using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
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
    private IStorageFile? _fileSaved;
    private bool _hasFileChanged;
    private bool _isFileOpened;
    private bool _changedWithCode;
    private readonly RegistryOptions _registryOptions;
    private readonly TextMate.Installation _textMateInstallation;
    private readonly FoldingManager _foldingManager;
    private readonly BraceFoldingStrategy _braceFoldingStrategy;
    private readonly XmlFoldingStrategy _xmlFoldingStrategy;
    private Language? _language;
    private readonly DispatcherTimer _dispatcherTimer;

    public MainView()
    {
        InitializeComponent();

        // Setting up text folding
        _foldingManager = FoldingManager.Install(Editor.TextArea);
        _braceFoldingStrategy = new BraceFoldingStrategy();
        _xmlFoldingStrategy = new XmlFoldingStrategy();

        // Setting up syntax highlighting
        _registryOptions = new RegistryOptions(ThemeName.Dracula);
        _textMateInstallation = Editor.InstallTextMate(_registryOptions);

        // Editor Options
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.IndentationSize = 2;

        // Subscribing to events
        Editor.Document.UndoStack.PropertyChanged += OnUndoPropertyChanged;
        Editor.Document.TextChanged += Editor_TextChanged;

        // Setting up timer to update text folding
        _dispatcherTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, TimerTick);
        _dispatcherTimer.Start();
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        _dispatcherTimer.Stop();
        _dispatcherTimer.Start();
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        _dispatcherTimer.Stop();
        if (!_hasFileChanged) return;

        if (_language?.Id is "xml" or "html")
            _xmlFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        else
            _braceFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);

        if (_topLevel is Window window) window.Closing += OnExit;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is Window window) window.Closing -= OnExit;
        base.OnDetachedFromVisualTree(e);
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
        string fileName = _fileSaved != null ? _fileSaved.Name : "Untitled";
        window.Title = $"{prefix}{fileName} - Rizor";
    }

    private async Task UpdateFile()
    {
        try
        {
            if (_fileSaved == null) return;

            await using Stream stream = await _fileSaved.OpenWriteAsync();
            await using StreamWriter streamWriter = new(stream);
            await streamWriter.WriteAsync(Editor.Text);
            Editor.Document.UndoStack.MarkAsOriginalFile();
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }

    private async Task SaveFile(string title, string suggestedFileName)
    {
        if (!_isFileOpened)
        {
            _fileSaved = await _topLevel?.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
                SuggestedFileType = FilePickerFileTypes.TextPlain
            })!;
        }

        if (_fileSaved == null) return;

        await UpdateFile();

        _isFileOpened = true;
        _hasFileChanged = false;

        RefreshTitle();
    }

    private void OnNew(object? sender, RoutedEventArgs e)
    {
        ClearStatusBar();

        _changedWithCode = true;

        Editor.Clear();

        _isFileOpened = false;
        _hasFileChanged = false;
        _fileSaved = null;

        RefreshTitle();

        _changedWithCode = false;
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
            _fileSaved = fileOpened[0];
            await using Stream stream = await _fileSaved.OpenReadAsync();

            Editor.Load(stream);
            Editor.Document.UndoStack.MarkAsOriginalFile();

            _language = _registryOptions.GetLanguageByExtension(Path.GetExtension(_fileSaved.Path.LocalPath));
            string scopeName = _registryOptions.GetScopeByLanguageId(_language.Id);

            _textMateInstallation.SetGrammar(scopeName);

            if (_language?.Id is "xml" or "html")
                _xmlFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
            else
                _braceFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);

            _isFileOpened = true;
            _changedWithCode = false;
            _hasFileChanged = false;

            RefreshTitle();
        }
        catch (Exception ex)
        {
            switch (ex)
            {
                case UnauthorizedAccessException:
                    Console.WriteLine("Permission Denied!");
                    break;
                case ArgumentException:
                    Console.WriteLine("File Encoding Not Supported!");
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
            Console.WriteLine("err");
        }
    }

    private async void OnSaveAs(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_topLevel == null) return;

            ClearStatusBar();

            await SaveFile("Save File As", _fileSaved != null ? _fileSaved.Name : "untitled");
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }

    private void ClearStatusBar()
    {
        if (StatusBar.Text != string.Empty)
            StatusBar.ClearValue(TextBlock.TextProperty);
    }

    private void OnUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnCut(object? sender, RoutedEventArgs e) => Editor.Cut();
    private void OnCopy(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void OnPaste(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void OnDelete(object? sender, RoutedEventArgs e) => Editor.SelectedText = "";
    private void OnSelectAll(object? sender, RoutedEventArgs e) => Editor.SelectAll();

    private async void OnExit(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            if (_topLevel is not Window window || !_hasFileChanged) return;
            e.Cancel = true;
            string fileName = _fileSaved != null ? _fileSaved.Name : "Untitled";
            IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(
                new MessageBoxStandardParams()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ButtonDefinitions = ButtonEnum.YesNoCancel,
                    ContentTitle = "Unsaved Changes",
                    ContentMessage = $"Save changes in  {fileName}",
                    CanResize = false,
                });

            ButtonResult result = await box.ShowWindowDialogAsync(window);

            if (result == ButtonResult.Cancel) return;

            if (result == ButtonResult.Yes)
            {
                if (_fileSaved == null)
                    await SaveFile("Save File", "untitled");
                else
                    await UpdateFile();
            }

            window.Closing -= OnExit;
            window.Close();
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }

    private void EnableWordWrap(object? sender, RoutedEventArgs e)
    {
        if (WordWrapCheckBox.IsChecked == null) return;

        WordWrapCheckBox.IsChecked = !WordWrapCheckBox.IsChecked;
        Editor.WordWrap = (bool)WordWrapCheckBox.IsChecked;
    }

    private void OnNewWindow(object? sender, RoutedEventArgs e)
    {
        if (_topLevel is Window)
        {
            MainWindow newMainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };

            newMainWindow.Show();
        }
    }

    private async void OnChangeFont(object? sender, RoutedEventArgs e)
    {
        try
        {
            await DialogHost.Show(null, MainDialogHost);
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }
}