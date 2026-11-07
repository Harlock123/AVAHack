using Avalonia.Controls;
using AVAHack.App.Views;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Content = new GameView(new GameEngine(LevelLoader.LoadBuiltIn("test-hall")), startLoop: true);
    }
}
