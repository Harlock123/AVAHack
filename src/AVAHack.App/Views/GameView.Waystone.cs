using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Game;

namespace AVAHack.App.Views;

/// <summary>The Waystone screen (spec 3): merchant, trainer, shrine and stash, for the chosen hero.</summary>
public sealed partial class GameView
{
    private enum WaystoneTab { Merchant, Trainer, Shrine, Stash }

    private WaystoneTab _waystoneTab;
    private string _waystoneSaid = "";

    /// <summary>Opens the Waystone screen (the engine raises this when the party uses the stone).</summary>
    public void OpenWaystone()
    {
        _waystoneSaid = "";
        Overlay = Overlay.Waystone;
    }

    private void Do(string said)
    {
        _waystoneSaid = said;
        Engine.Say(said);
    }

    private void DrawWaystone(DrawingContext dc)
    {
        if (!Engine.AtWaystone) { Overlay = Overlay.None; return; }
        dc.FillRectangle(Shadow, new Rect(0, 0, CanvasWidth, CanvasHeight));
        var box = new Rect(140, 40, 1000, 640);
        dc.DrawRectangle(PanelBrush, new Pen(PanelEdge, 3), box, 8, 8);
        var h = Engine.Selected;
        DrawText(dc, "The Waystone", box.X + 24, box.Y + 14, 28, Gold, face: Bold);
        DrawText(dc, $"{h.Name} · level {h.Level} · gold {Engine.Party.Gold.ToString("N0", CultureInfo.InvariantCulture)}", box.X + 300, box.Y + 22, 16, Parchment);
        Button(dc, new Rect(box.Right - 200, box.Y + 14, 36, 32), "◀", () => { CycleSelected(); CycleSelected(); CycleSelected(); });
        Button(dc, new Rect(box.Right - 158, box.Y + 14, 36, 32), "▶", CycleSelected);
        Button(dc, new Rect(box.Right - 110, box.Y + 14, 90, 32), "Close", () => Overlay = Overlay.None, size: 14);
        double x = box.X + 24;
        foreach (var t in Enum.GetValues<WaystoneTab>())
        {
            Button(dc, new Rect(x, box.Y + 60, 150, 36), t.ToString(), () => _waystoneTab = t, selected: _waystoneTab == t, size: 15);
            x += 160;
        }
        Button(dc, new Rect(box.Right - 200, box.Y + 60, 176, 36), "Rest here (free)", () => Do(Engine.RestAtStone()), size: 14);
        Button(dc, new Rect(box.Right - 390, box.Y + 60, 180, 36), "Anvil and altar (K)", () => OpenCraft("anvil"), size: 14);
        double y = box.Y + 112;
        switch (_waystoneTab)
        {
            case WaystoneTab.Merchant:
            {
                DrawText(dc, "For sale (click to buy)", box.X + 24, y, 15, Gold, face: Bold);
                DrawText(dc, $"{h.Name}'s pack (click to sell for half its worth)", box.X + 520, y, 15, Gold, face: Bold);
                double yy = y + 26;
                foreach (var (index, item, price) in Engine.Stock())
                {
                    Button(dc, new Rect(box.X + 24, yy, 470, 32), $"{item.Name(Content)} — {price} gold", () => Do(Engine.Buy(index, h)),
                        enabled: Engine.Party.Gold >= price, size: 13, item: item);
                    yy += 38;
                }
                yy = y + 26;
                for (int i = 0; i < h.Backpack.Length; i++)
                {
                    if (h.Backpack[i] is not { } it) continue;
                    int slot = i;
                    Button(dc, new Rect(box.X + 520, yy, 456, 30), $"{it.Name(Content)} — {Math.Max(1, GameEngine.Worth(Content, it) / GameEngine.BuyMarkup)} gold", () => Do(Engine.Sell(h, slot)), size: 13, item: it);
                    yy += 34;
                    if (yy > box.Bottom - 70) break;
                }
                break;
            }
            case WaystoneTab.Trainer:
            {
                DrawText(dc, $"Lessons this level: {h.LessonsThisLevel} of {Progression.LessonsPerLevel}. Each costs rank² × 2 gold.", box.X + 24, y, 15, Parchment);
                int col = 0, row = 0;
                foreach (var s in Enum.GetValues<SkillId>())
                {
                    int cost = Progression.TrainingCost(h.Rank(s));
                    var r = new Rect(box.X + 24 + col * 320, y + 30 + row * 38, 304, 32);
                    Button(dc, r, $"{Content.Skill(s).Name} {h.Rank(s)} → {h.Rank(s) + 1}: {cost} gold", () => Do(Engine.TrainAt(h, s)),
                        enabled: Engine.Party.Gold >= cost && h.LessonsThisLevel < Progression.LessonsPerLevel && h.Rank(s) < h.SkillCeiling, size: 13);
                    if (++col == 3) { col = 0; row++; }
                }
                break;
            }
            case WaystoneTab.Shrine:
            {
                double yy = y;
                foreach (var c in Engine.Party.Members)
                {
                    DrawText(dc, $"{c.Name}: {(c.IsDead ? "dead" : c.IsDown ? "down" : $"{(int)c.Hp}/{c.MaxHp} HP")}", box.X + 24, yy + 8, 15, Parchment);
                    Button(dc, new Rect(box.X + 300, yy, 200, 34), c.IsDead ? $"Raise ({GameEngine.ReviveCost(c)} gold)" : "Tend", () => Do(Engine.ReviveAt(c)), enabled: c.IsDead || c.IsDown, size: 13);
                    Button(dc, new Rect(box.X + 510, yy, 220, 34), $"Lift curses ({GameEngine.CleanseCost(c)})", () => Do(Engine.CleanseAt(c)), size: 13);
                    Button(dc, new Rect(box.X + 740, yy, 230, 34), $"Forget perks ({c.RespecCost})", () => Do(Engine.RespecAt(c)), enabled: c.Perks.Count > 0, size: 13);
                    yy += 46;
                }
                break;
            }
            case WaystoneTab.Stash:
            {
                DrawText(dc, "The stash (shared by every Waystone; click to take)", box.X + 24, y, 15, Gold, face: Bold);
                DrawText(dc, $"{h.Name}'s pack (click to store)", box.X + 520, y, 15, Gold, face: Bold);
                double yy = y + 26;
                for (int i = 0; i < Engine.Stash.Count && yy < box.Bottom - 70; i++)
                {
                    int index = i;
                    Button(dc, new Rect(box.X + 24, yy, 470, 30), Engine.Stash[i].Name(Content), () => Do(Engine.Withdraw(index, h)), size: 13, item: Engine.Stash[i]);
                    yy += 34;
                }
                yy = y + 26;
                for (int i = 0; i < h.Backpack.Length && yy < box.Bottom - 70; i++)
                {
                    if (h.Backpack[i] is not { } it) continue;
                    int slot = i;
                    Button(dc, new Rect(box.X + 520, yy, 456, 30), it.Name(Content), () => Do(Engine.Deposit(h, slot)), size: 13, item: it);
                    yy += 34;
                }
                break;
            }
        }
        if (_waystoneSaid.Length > 0) DrawText(dc, _waystoneSaid, box.X + 24, box.Bottom - 40, 14, Gold, maxWidth: box.Width - 48);
    }

    /// <summary>The boss's name and health across the top of the view while it fights.</summary>
    private void DrawBossBar(DrawingContext dc)
    {
        if (Engine.LivingBoss is not { IsAware: true } b) return;
        var bar = new Rect(ViewArea.X + 180, ViewArea.Y + 14, ViewArea.Width - 360, 16);
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 12, 12)), bar);
        dc.FillRectangle(new SolidColorBrush(Color.FromRgb(170, 30, 30)), new Rect(bar.X, bar.Y, bar.Width * Math.Clamp((double)b.Hp / b.MaxHp, 0, 1), bar.Height));
        dc.DrawRectangle(null, new Pen(Gold, 1.5), bar);
        DrawText(dc, b.Name, bar.Center.X, bar.Bottom + 2, 16, Gold, centre: true, face: Bold);
    }
}
