using Avalonia.Controls;
using AVAHack.App.Views;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.App;

public partial class MainWindow : Window
{
    /// <summary>A fresh run of The Cellars with a new seed.</summary>
    private static GameEngine NewGame() => new(LevelLoader.LoadBuiltIn("the-cellars"), (ulong)Environment.TickCount64);

    public MainWindow()
    {
        InitializeComponent();
        Content = new GameView(NewGame(), startLoop: true, restart: NewGame);
    }
}
