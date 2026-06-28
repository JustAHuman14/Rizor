using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rizor.Models;

namespace Rizor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // Observable Variables
    [ObservableProperty] public partial FontFamily SelectedFontFamily { get; set; }
    [ObservableProperty] public partial double SelectedFontSize { get; set; }
    public ObservableCollection<Node> Nodes { get; }

    // Non-Observable Variables
    public List<FontFamily> FontFamiliesList { get; }
    public List<double> FontSizeList { get; }

    public MainWindowViewModel()
    {
        Nodes = new()
        {
            new Node("Folder", [
                new Node("Sub Folder", [
                    new Node("Sub-Sub Folder", [
                        new Node("File 1"),
                        new Node("File 2"),
                        new Node("File 3"),
                        new Node("File 4")
                    ])
                ])
            ])
        };

        FontFamiliesList = FontManager.Current.SystemFonts
            .OrderBy(n => n.Name)
            .ToList();

        FontSizeList =
            [6, 8, 9, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 44, 48, 52, 56, 60, 68, 76];

        SelectedFontFamily = FontManager.Current.DefaultFontFamily;
        SelectedFontSize = 16;
    }
}