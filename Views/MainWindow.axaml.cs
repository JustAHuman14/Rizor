using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

// ReSharper disable once CheckNamespace
namespace Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly TopLevel? _topLevel;
    private IStorageFile? _fileSaved;
    private bool _hasFileChanged;
    private bool _isFileOpened;
    private bool _changedWithCode;

    public MainWindow()
    {
        InitializeComponent();
        _topLevel = GetTopLevel(this);
        Editor.TextChanging += OnTextChanging;
    }

    private void OnTextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_changedWithCode) return;

        if (!_hasFileChanged)
        {
            _hasFileChanged = true;
            RefreshTitle();
        }
    }

    private async void UpdateFile()
    {
        try
        {
            if (_fileSaved == null) return;

            await using Stream stream = await _fileSaved.OpenWriteAsync();
            await using StreamWriter streamWriter = new(stream);
            await streamWriter.WriteAsync(Editor.Text);
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }

    private void RefreshTitle()
    {
        string prefix = _hasFileChanged ? "*" : "";
        string fileName = _fileSaved != null ? _fileSaved.Name : "Untitled";
        this.Title = $"{prefix}{fileName} - Rizor";
    }

    private void OnNew(object? sender, RoutedEventArgs e)
    {
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

            var fileOpened = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                AllowMultiple = false
            });

            if (fileOpened.Count == 0) return;

            _fileSaved = fileOpened[0];
            await using Stream stream = await fileOpened[0].OpenReadAsync();
            using StreamReader streamReader = new StreamReader(stream);
            string openedFileText = await streamReader.ReadToEndAsync();

            _changedWithCode = true;
            Editor.Text = openedFileText;
            _isFileOpened = true;
            _hasFileChanged = false;

            RefreshTitle();

            _changedWithCode = false;
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_topLevel == null) return;

            if (_isFileOpened == false)
            {
                _fileSaved = await _topLevel?.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                {
                    Title = "Save File",
                    SuggestedFileName = "untitled",
                    SuggestedFileType = FilePickerFileTypes.TextPlain
                })!;
            }

            if (_fileSaved == null) return;

            UpdateFile();

            _isFileOpened = true;
            _hasFileChanged = false;
            RefreshTitle();
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

            var fileSavedAs = await _topLevel?.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save File As",
                SuggestedFileName = _fileSaved != null ? _fileSaved.Name : "untitled",
                SuggestedFileType = FilePickerFileTypes.TextPlain
            })!;

            if (fileSavedAs == null) return;

            _fileSaved = fileSavedAs;

            UpdateFile();

            _isFileOpened = true;
            _hasFileChanged = false;
            RefreshTitle();
        }
        catch (Exception)
        {
            Console.WriteLine("err");
        }
    }

    private void OnUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnCut(object? sender, RoutedEventArgs e) => Editor.Cut();
    private void OnCopy(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void OnPaste(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void OnDelete(object? sender, RoutedEventArgs e) => Editor.SelectedText = "";
    private void OnSelectAll(object? sender, RoutedEventArgs e) => Editor.SelectAll();
    private void OnExit(object? sender, RoutedEventArgs e) => this.Close();

}
