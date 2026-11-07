using Avalonia.Controls;
using AVAHack.App.Views;
using AVAHack.Content;

namespace AVAHack.App;

public partial class MainWindow : Window
{

    public MainWindow()
    {
        InitializeComponent();
        Content = new GameView(ContentDb.LoadBuiltIn(), startLoop: true);
    }
}
