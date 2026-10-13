using Avalonia.Controls;
using AVACitiesOfGold.Views;

namespace AVACitiesOfGold;

public partial class MainWindow : Window
{
    public MainWindow() : this([]) { }

    public MainWindow(string[] args)
    {
        InitializeComponent();
        Content = new GameCanvas(silent: args.Contains("--no-audio"));
    }
}
