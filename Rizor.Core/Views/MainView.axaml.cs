using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace Rizor.Core.Views;

public partial class MainView : UserControl
{
    private TopLevel? _topLevel;
    private IStorageFile? _fileSaved;
    private bool _hasFileChanged;
    private bool _isFileOpened;
    private bool _changedWithCode;
    private bool _isBinaryFile;

    public MainView()
    {
        InitializeComponent();
        Editor.TextChanging += OnTextChanging;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);

        if (_topLevel is Window window) window.Closing += OnExit;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_topLevel is Window window) window.Closing -= OnExit;
    }

    private void OnTextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_changedWithCode) return;

        if (StatusBar.Text != string.Empty) ClearStatusBar();

        if (_hasFileChanged) return;
        _hasFileChanged = true;
        RefreshTitle();
    }

    private async Task UpdateFile()
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
        if (_topLevel is not Window window) return;
        string prefix = _hasFileChanged ? "*" : "";
        string fileName = _fileSaved != null ? _fileSaved.Name : "Untitled";
        window.Title = $"{prefix}{fileName} - Rizor";
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

            var fileOpened = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                AllowMultiple = false
            });

            if (fileOpened.Count == 0) return;

            _changedWithCode = true;
            Editor.Clear();
            _changedWithCode = false;

            _fileSaved = fileOpened[0];
            await using Stream stream = await fileOpened[0].OpenReadAsync();
            BinaryReader binaryReader = new BinaryReader(stream);
            _isBinaryFile = binaryReader.ReadBytes(512).Contains((byte)0);
            StatusBar.Text = _isBinaryFile ? "Binary files are not supported!" : "";

            if (_isBinaryFile)
            {
                _fileSaved = null;
                _isFileOpened = false;
            }
            else
            {
                stream.Position = 0;
                using StreamReader streamReader = new StreamReader(stream);
                _changedWithCode = true;
                Editor.IsUndoEnabled = false;
                Editor.Text = await streamReader.ReadToEndAsync();
                Editor.IsUndoEnabled = true;
                _isFileOpened = true;
                _changedWithCode = false;
            }

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
            if (_topLevel is Window window && _hasFileChanged)
            {
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
        Console.WriteLine($"Word wrap is on: {WordWrapCheckBox.IsChecked}");
        Editor.TextWrapping = (bool)WordWrapCheckBox.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void OnNewWindow(object? sender, RoutedEventArgs e)
    {
        if (_topLevel is Window)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}