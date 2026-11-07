using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Render;
using AVAHack.Rules;
using AVAHack.Saves;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    // ---- Title ----

    /// <summary>What "begin" does with a new party: The Cellars here; the Delve options screen replaces it.</summary>
    public Func<Party, GameEngine>? NewGame { get; set; }

    private GameEngine Begin(Party party) =>
        NewGame?.Invoke(party) ?? new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), (ulong)Environment.TickCount64, party, Content);

    private void DrawTitle(DrawingContext dc)
    {
        dc.DrawImage(_bitmap, new Rect(0, 0, Renderer.Width, Renderer.Height), new Rect(0, 0, CanvasWidth, CanvasHeight));
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), new Rect(0, 0, CanvasWidth, CanvasHeight));
        DrawText(dc, "AVAHack", 640, 120, 84, Gold, centre: true, face: Bold);
        DrawText(dc, "Beneath Harrowgate, the Hollow moves each time the Knell tolls.", 640, 230, 20, Parchment, centre: true);
        double y = 320;
        bool any = Store.List().Count > 0;
        Button(dc, new Rect(500, y, 280, 48), "Quick start", () => StartGame(Begin(DefaultParty(Content))), size: 20);
        Button(dc, new Rect(500, y += 64, 280, 48), "Make a party", () => { _drafts.Clear(); _drafts.Add(HeroDraft.For(Content, 0)); _editing = 0; Screen = Screen.Create; }, size: 20);
        Button(dc, new Rect(500, y += 64, 280, 48), "Continue", () => ContinueLatest(), enabled: any, size: 20);
        Button(dc, new Rect(500, y += 64, 280, 48), "Load a game", () => Screen = Screen.Load, enabled: any, size: 20);
        Button(dc, new Rect(500, y += 64, 280, 48), "Quit", () => (Avalonia.Controls.TopLevel.GetTopLevel(this) as Avalonia.Controls.Window)?.Close(), size: 20);
        DrawText(dc, "An original dungeon crawler · placeholder art drawn in code", 640, 760, 13, Dim, centre: true);
    }

    private void ContinueLatest()
    {
        var latest = Store.List().FirstOrDefault();
        if (latest.Slot is not null) LoadSlot(latest.Slot);
    }

    private void DrawLoad(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, new Rect(0, 0, CanvasWidth, CanvasHeight));
        DrawText(dc, "Load a game", 640, 60, 40, Gold, centre: true, face: Bold);
        SaveList(dc, 140, slot => LoadSlot(slot));
        Button(dc, new Rect(540, 720, 200, 44), "Back", () => Screen = Screen.Title);
    }

    private void SaveList(DrawingContext dc, double top, Action<string> onPick)
    {
        double y = top;
        foreach (var (slot, at, summary) in Store.List().Take(8))
        {
            string when = at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            Button(dc, new Rect(240, y, 800, 56), $"{slot}  ·  {when}  ·  {summary}", () => onPick(slot), size: 15);
            y += 66;
        }
        if (y == top) DrawText(dc, "No saved games yet.", 640, top + 20, 18, Dim, centre: true);
    }

    // ---- Making a party ----

    private sealed class HeroDraft
    {
        public string Name = "";
        public int Ancestry, Calling, Background;
        public AttributeSet Scores = new(10, 10, 10, 10, 10, 10);
        public bool Rolled;
        public int[][]? Rolls;

        private static readonly string[] Names = ["Brannoc", "Pell", "Ama", "Ithren", "Odo", "Sefa", "Kael", "Mirren", "Tova", "Ruun"];

        /// <summary>A sensible new hero: the standard array laid out for the calling's needs.</summary>
        public static HeroDraft For(ContentDb db, int n)
        {
            var d = new HeroDraft { Name = Names[n % Names.Length], Calling = n % db.Callings.Count, Ancestry = n % db.Ancestries.Count, Background = n % db.Backgrounds.Count };
            d.Standard(db);
            return d;
        }

        public void Standard(ContentDb db)
        {
            var order = db.Callings[Calling].Id switch
            {
                "fighter" or "barbarian" or "paladin" => new[] { Attribute.Str, Attribute.Con, Attribute.Dex, Attribute.Wis, Attribute.Cha, Attribute.Int },
                "ranger" or "rogue" or "monk" => [Attribute.Dex, Attribute.Con, Attribute.Wis, Attribute.Str, Attribute.Int, Attribute.Cha],
                "cleric" or "druid" => [Attribute.Wis, Attribute.Con, Attribute.Str, Attribute.Dex, Attribute.Cha, Attribute.Int],
                _ => [Attribute.Int, Attribute.Con, Attribute.Dex, Attribute.Wis, Attribute.Str, Attribute.Cha],
            };
            int[] arr = [15, 14, 13, 12, 10, 8];
            for (int i = 0; i < 6; i++) Scores[order[i]] = arr[i];
            Rolled = false;
            Rolls = null;
        }

        public Character Build(ContentDb db) =>
            CharacterBuilder.Create(db, string.IsNullOrWhiteSpace(Name) ? "Nameless" : Name, db.Ancestries[Ancestry].Id,
                db.Callings[Calling].Id, db.Backgrounds[Background].Id, Scores, Rolled);
    }

    private readonly List<HeroDraft> _drafts = new();
    private int _editing;
    private bool _typingName;
    private readonly Pcg32 _dice = new((ulong)Environment.TickCount64);

    private void DrawCreate(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, new Rect(0, 0, CanvasWidth, CanvasHeight));
        DrawText(dc, "Make a party", 40, 24, 34, Gold, face: Bold);
        DrawText(dc, "Up to four heroes. A lone delver gets +50% HP, +2 to saves and twice the skill XP.", 40, 70, 14, Dim);

        // The party so far.
        for (int i = 0; i < _drafts.Count; i++)
        {
            int k = i;
            var d = _drafts[i];
            string label = $"{(string.IsNullOrWhiteSpace(d.Name) ? "Nameless" : d.Name)}\n{Content.Ancestries[d.Ancestry].Name} {Content.Callings[d.Calling].Name}";
            Button(dc, new Rect(40, 110 + i * 84, 280, 72), label, () => { _editing = k; _typingName = false; }, selected: i == _editing, size: 15);
        }
        if (_drafts.Count < 4) Button(dc, new Rect(40, 110 + _drafts.Count * 84, 280, 44), "+ Add a hero", () => { _drafts.Add(HeroDraft.For(Content, _drafts.Count)); _editing = _drafts.Count - 1; });
        if (_drafts.Count > 1) Button(dc, new Rect(40, 470, 280, 40), "Remove this hero", () => { _drafts.RemoveAt(_editing); _editing = Math.Max(0, _editing - 1); });

        var hero = _drafts[_editing];
        double x = 380, y = 110;
        DrawText(dc, "Name", x, y, 14, Dim);
        Button(dc, new Rect(x + 120, y - 6, 300, 34), (hero.Name.Length == 0 ? "…" : hero.Name) + (_typingName ? "_" : ""), () => _typingName = true, selected: _typingName);
        y += 50;
        Chooser(dc, x, y, "Ancestry", Content.Ancestries[hero.Ancestry].Name, Content.Ancestries[hero.Ancestry].Text, d => hero.Ancestry = Wrap(hero.Ancestry + d, Content.Ancestries.Count));
        y += 74;
        Chooser(dc, x, y, "Calling", Content.Callings[hero.Calling].Name,
            $"d{Content.Callings[hero.Calling].HitDie} · {Content.Callings[hero.Calling].SignatureText}",
            d => { hero.Calling = Wrap(hero.Calling + d, Content.Callings.Count); if (!hero.Rolled) hero.Standard(Content); });
        y += 74;
        Chooser(dc, x, y, "Background", Content.Backgrounds[hero.Background].Name, Content.Backgrounds[hero.Background].Text, d => hero.Background = Wrap(hero.Background + d, Content.Backgrounds.Count));
        y += 84;

        DrawText(dc, hero.Rolled ? "Attributes (rolled: 4d6, drop the lowest)" : $"Attributes (point buy: {AttributeGen.PointBuyBudget - AttributeGen.PointsSpent(hero.Scores)} points left)", x, y, 15, Gold, face: Bold);
        y += 30;
        var preview = Safe(() => hero.Build(Content));
        foreach (var a in Enum.GetValues<Attribute>())
        {
            var attr = a;
            DrawText(dc, attr.ToString().ToUpperInvariant(), x, y + 4, 15, Parchment, face: Bold);
            int final = preview?.Score(attr) ?? hero.Scores[attr];
            DrawText(dc, $"{hero.Scores[attr]} → {final} ({AttributeSet.Modifier(final):+0;-0;0})", x + 60, y + 4, 15, Parchment);
            if (!hero.Rolled)
            {
                Button(dc, new Rect(x + 200, y, 34, 30), "−", () => Nudge(hero, attr, -1), enabled: hero.Scores[attr] > AttributeGen.PointBuyMin);
                Button(dc, new Rect(x + 240, y, 34, 30), "+", () => Nudge(hero, attr, +1), enabled: hero.Scores[attr] < AttributeGen.PointBuyMax && CanAfford(hero, attr));
            }
            y += 36;
        }
        Button(dc, new Rect(x, y + 6, 160, 36), hero.Rolled ? "Point buy" : "Roll 4d6", () =>
        {
            if (hero.Rolled) hero.Standard(Content);
            else { var (s, rolls) = AttributeGen.Roll4d6DropLowest(_dice); hero.Scores = s; hero.Rolls = rolls; hero.Rolled = true; }
        });
        if (hero.Rolled) Button(dc, new Rect(x + 170, y + 6, 120, 36), "Reroll", () => { var (s, rolls) = AttributeGen.Roll4d6DropLowest(_dice); hero.Scores = s; hero.Rolls = rolls; });

        // The preview.
        double px = 900, py = 330;
        if (preview is not null)
        {
            StartingKit.Give(Content, preview);
            DrawText(dc, "Ready to go", px, py, 15, Gold, face: Bold);
            DrawText(dc, $"HP {preview.MaxHp} · Stamina {preview.MaxStamina} · Mana {preview.MaxMana} · AC {preview.ArmourClass}", px, py + 26, 14, Parchment, maxWidth: 360);
            var best = Enum.GetValues<SkillId>().OrderByDescending(preview.Rank).Take(7);
            DrawText(dc, "Best skills: " + string.Join(", ", best.Select(s => $"{Content.Skill(s).Name} {preview.Rank(s)}")), px, py + 52, 13, Dim, maxWidth: 360);
            DrawText(dc, "Carries: " + string.Join(", ", preview.Equipped.Values.Select(i => i.Name(Content))), px, py + 120, 13, Dim, maxWidth: 360);
        }
        Button(dc, new Rect(40, 730, 200, 46), "Back", () => Screen = Screen.Title);
        Button(dc, new Rect(1000, 730, 240, 46), "Begin the descent", () =>
        {
            var heroes = _drafts.Select(d => d.Build(Content)).ToArray();
            foreach (var h in heroes) StartingKit.Give(Content, h);
            StartGame(Begin(new Party(heroes)));
        }, enabled: _drafts.All(d => Safe(() => d.Build(Content)) is not null), size: 18);
    }

    private void Chooser(DrawingContext dc, double x, double y, string label, string value, string text, Action<int> change)
    {
        DrawText(dc, label, x, y + 6, 14, Dim);
        Button(dc, new Rect(x + 120, y, 34, 32), "◀", () => change(-1));
        DrawText(dc, value, x + 280, y + 4, 18, Parchment, centre: true, face: Bold);
        Button(dc, new Rect(x + 406, y, 34, 32), "▶", () => change(+1));
        DrawText(dc, text, x + 120, y + 38, 13, Dim, maxWidth: 760);
    }

    private static int Wrap(int v, int n) => ((v % n) + n) % n;

    private static void Nudge(HeroDraft d, Attribute a, int by) => d.Scores[a] = Math.Clamp(d.Scores[a] + by, AttributeGen.PointBuyMin, AttributeGen.PointBuyMax);

    private static bool CanAfford(HeroDraft d, Attribute a)
    {
        var trial = d.Scores.Clone();
        trial[a]++;
        return AttributeGen.IsValidPointBuy(trial);
    }

    private static Character? Safe(Func<Character> f)
    {
        try { return f(); }
        catch (ArgumentException) { return null; }
    }

    private bool CreateKey(Avalonia.Input.KeyEventArgs e)
    {
        if (!_typingName) return false;
        var hero = _drafts[_editing];
        if (e.PhysicalKey == PhysicalKey.Backspace && hero.Name.Length > 0) hero.Name = hero.Name[..^1];
        else if (e.PhysicalKey is PhysicalKey.Enter or PhysicalKey.Escape or PhysicalKey.Tab) _typingName = false;
        InvalidateVisual();
        return true;
    }

    private void CreateText(string text)
    {
        if (!_typingName) return;
        var hero = _drafts[_editing];
        foreach (char ch in text)
            if ((char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '\'') && hero.Name.Length < 18) hero.Name += ch;
        InvalidateVisual();
    }

    // ---- The menu (Esc) ----

    private void DrawMenu(DrawingContext dc)
    {
        dc.FillRectangle(Shadow, new Rect(0, 0, CanvasWidth, CanvasHeight));
        var box = new Rect(240, 60, 800, 540);
        dc.DrawRectangle(PanelBrush, new Pen(PanelEdge, 3), box, 8, 8);
        DrawText(dc, "Menu", box.Center.X, box.Y + 16, 30, Gold, centre: true, face: Bold);
        double y = box.Y + 70;
        Button(dc, new Rect(box.X + 40, y, 220, 40), "Resume", () => Overlay = Overlay.None);
        Button(dc, new Rect(box.X + 290, y, 220, 40), "Help (F1)", () => Overlay = Overlay.Help);
        Button(dc, new Rect(box.X + 540, y, 220, 40), "Quit to title", () => { Screen = Screen.Title; Overlay = Overlay.None; });
        y += 70;
        DrawText(dc, "Save to a slot", box.X + 40, y, 16, Gold, face: Bold);
        y += 28;
        for (int i = 1; i <= 3; i++)
        {
            string slot = $"slot{i}";
            var have = Store.List().FirstOrDefault(s => s.Slot == slot);
            Button(dc, new Rect(box.X + 40, y, 720, 40), have.Slot is null ? $"Slot {i}: empty" : $"Slot {i}: {have.Summary}", () =>
            {
                if (Engine.Mode != GameMode.Playing) return;
                Store.Save(slot, SaveNow());
                Engine.Say($"Saved to slot {i}.");
                Overlay = Overlay.None;
            }, enabled: Engine.Mode == GameMode.Playing, size: 14);
            y += 48;
        }
        y += 14;
        DrawText(dc, "Load", box.X + 40, y, 16, Gold, face: Bold);
        y += 28;
        foreach (var (slot, _, summary) in Store.List().Take(4))
        {
            Button(dc, new Rect(box.X + 40, y, 720, 36), $"{slot}: {summary}", () => LoadSlot(slot), size: 14);
            y += 42;
        }
        DrawText(dc, "F5 quick save · F9 quick load", box.X + 40, box.Bottom - 30, 13, Dim);
    }

    private void DrawHelp(DrawingContext dc)
    {
        dc.FillRectangle(Shadow, ViewArea);
        var box = ViewArea.Deflate(40);
        dc.DrawRectangle(PanelBrush, new Pen(PanelEdge, 3), box, 8, 8);
        DrawText(dc, "How to play", box.X + 24, box.Y + 16, 26, Gold, face: Bold);
        string[] lines =
        [
            "W A S D: step and strafe.  Q E (or the arrows): turn.  F: use a door, lever or chest.  G: pick up.",
            "1 2 · 3 4 · 5 6 · 7 8: each hero's left and right hand. Hold a weapon hand to charge a power attack;",
            "hold a shield to block (raise it just as a blow lands for a perfect block); tap a shield to bash.",
            "Only the front row can reach with a melee weapon. Reach weapons and bows work from the back.",
            "Tab: next hero.  Ctrl+1–4: move the chosen hero to a slot.  I: inventory.  C: character sheet.",
            "B: spellbook.  F1–F8 cast the chosen hero's quick spells.  X: sneak.  R: rest.  Space: pause.",
            "F5 / F9: quick save and load.  Esc: menu.",
            "",
            "Every blow has a tell: watch for the wind-up and step aside or block. Creatures not facing you are",
            "hit with advantage; ones that haven't noticed you take double damage. Bones need crushing.",
        ];
        for (int i = 0; i < lines.Length; i++) DrawText(dc, lines[i], box.X + 24, box.Y + 64 + i * 26, 14, Parchment);
        Button(dc, new Rect(box.Right - 160, box.Bottom - 56, 136, 40), "Close", () => Overlay = Overlay.None);
    }

    /// <summary>Keys the overlays handle: F1 help, C sheet, B spellbook; later milestones add more.</summary>
    private bool OverlayKey(Avalonia.Input.KeyEventArgs e)
    {
        switch (e.PhysicalKey)
        {
            case PhysicalKey.F1: Overlay = Overlay == Overlay.Help ? Overlay.None : Overlay.Help; return true;
            case PhysicalKey.C: Overlay = Overlay == Overlay.Character ? Overlay.None : Overlay.Character; return true;
            case PhysicalKey.B: Overlay = Overlay == Overlay.Spellbook ? Overlay.None : Overlay.Spellbook; return true;
        }
        return ExtraKey(e);
    }

    /// <summary>Keys later milestones add while playing (casting, sneaking, resting).</summary>
    private bool ExtraKey(Avalonia.Input.KeyEventArgs e) => ExtraKeys?.Invoke(e.PhysicalKey) ?? false;
    public Func<PhysicalKey, bool>? ExtraKeys { get; set; }

    // ---- Inventory ----

    private static readonly EquipSlot[] DollOrder =
    [
        EquipSlot.Head, EquipSlot.Neck, EquipSlot.Cloak, EquipSlot.RightHand, EquipSlot.Chest, EquipSlot.LeftHand,
        EquipSlot.Hands, EquipSlot.Belt, EquipSlot.Ring1, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Ring2,
    ];

    private readonly Dictionary<string, WriteableBitmap> _icons = new();

    private void DrawInventory(DrawingContext dc)
    {
        var h = Engine.Selected;
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 22, 18, 14)), ViewArea);
        DrawText(dc, $"{h.Name}'s things", 24, 14, 24, Gold, face: Bold);
        Button(dc, new Rect(360, 12, 36, 32), "◀", () => { CycleSelected(); CycleSelected(); CycleSelected(); });
        Button(dc, new Rect(402, 12, 36, 32), "▶", CycleSelected);
        Button(dc, new Rect(860, 12, 80, 32), "Close", () => Overlay = Overlay.None);
        (Item Item, string Where)? hover = null;

        // Paper doll.
        for (int i = 0; i < DollOrder.Length; i++)
        {
            var slot = DollOrder[i];
            var r = new Rect(24 + (i % 3) * 82, 60 + (i / 3) * 96, 72, 72);
            DrawText(dc, SlotName(slot), r.Center.X, r.Bottom + 2, 10, Dim, centre: true);
            var it = h.ItemIn(slot);
            ItemBox(dc, r, it, () => { if (!h.Unequip(slot) && it is not null) Engine.Say(it.Cursed(Content) ? $"The {it.Name(Content)} is cursed. It won't come off." : "No room in the pack."); });
            if (it is not null && r.Contains(_pointer)) hover = (it, "worn");
        }

        // Backpack.
        DrawText(dc, $"Pack ({Character.BackpackSize - h.FreeSlots}/{Character.BackpackSize}) · {h.CarriedKg:0.#} of {h.CarryCapacityKg} kg", 300, 44, 13, Dim);
        for (int i = 0; i < Character.BackpackSize; i++)
        {
            int k = i;
            var r = new Rect(300 + (i % 6) * 70, 64 + (i / 6) * 70, 64, 64);
            var it = h.Backpack[i];
            ItemBox(dc, r, it, () => PackClick(h, k), () => { if (Engine.DropItem(h, k)) Engine.Say("Dropped."); });
            if (it is not null && r.Contains(_pointer)) hover = (it, "pack");
        }
        DrawText(dc, "Click: wear or use · Right-click: drop", 300, 346, 12, Dim);

        // The floor.
        var pile = Engine.Level.PileAt(Engine.PartyCell);
        DrawText(dc, pile.Count == 0 ? "Nothing on the floor here." : "On the floor (click to take):", 300, 380, 13, Dim);
        for (int i = 0; i < Math.Min(pile.Count, 10); i++)
        {
            var it = pile[i];
            var r = new Rect(300 + i * 58, 402, 52, 52);
            ItemBox(dc, r, it, () => { Engine.TakeFromFloor(it, h); Engine.IdentifyAll(); });
            if (r.Contains(_pointer)) hover = (it, "floor");
        }
        if (pile.Count > 0) Button(dc, new Rect(300, 466, 160, 34), "Take all (G)", () => Engine.PickUpAll());

        // Numbers.
        double sx = 740, sy = 60;
        DrawText(dc, "Gold: " + Engine.Party.Gold, sx, sy, 15, Gold, face: Bold);
        string[] stats =
        [
            $"AC {h.ArmourClass}",
            $"HP {Fmt(h.Hp)} / {h.MaxHp}",
            $"Stamina {Fmt(h.Stamina)} / {h.MaxStamina}",
            $"Mana {Fmt(h.Mana)} / {h.MaxMana}",
            $"To hit {(h.MainHand is { } w ? h.AttackBonus(w.Skill, w.Finesse && h.Mod(Attribute.Dex) > h.Mod(Attribute.Str) ? Attribute.Dex : Attribute.Str) : 0):+0;-0;0}",
            string.Join("  ", Enum.GetValues<Attribute>().Select(a => $"{a.ToString().ToUpperInvariant()} {h.Score(a)}")),
        ];
        for (int i = 0; i < stats.Length; i++) DrawText(dc, stats[i], sx, sy + 28 + i * 22, 13, Parchment, maxWidth: 210);

        if (hover is { } hv)
        {
            var lines = hv.Item.Describe(Content).ToList();
            var box = new Rect(Math.Min(_pointer.X + 16, 960 - 300), Math.Min(_pointer.Y + 16, 600 - 30 - lines.Count * 18), 290, 30 + lines.Count * 18);
            dc.DrawRectangle(PanelBrush, new Pen(RarityBrush(hv.Item.Rarity), 2), box, 4, 4);
            DrawText(dc, hv.Item.Name(Content), box.X + 8, box.Y + 6, 14, RarityBrush(hv.Item.Rarity), face: Bold, maxWidth: 274);
            for (int i = 0; i < lines.Count; i++) DrawText(dc, lines[i], box.X + 8, box.Y + 26 + i * 18, 12, Parchment, maxWidth: 274);
        }
    }

    private void PackClick(Character h, int index)
    {
        if (h.Backpack[index] is not { } it) return;
        var b = Content.Base(it.BaseId);
        if (b.Slots.Length > 0)
        {
            if (h.WhyNotEquip(index) is { } why) Engine.Say(why);
            else h.Equip(index);
            return;
        }
        if (Engine.UseItem(h, index) is null) Engine.Say($"{h.Name} can't use the {b.Name} like that.");
    }

    private static string SlotName(EquipSlot s) => s switch
    {
        EquipSlot.RightHand => "right hand", EquipSlot.LeftHand => "left hand", EquipSlot.Ring1 or EquipSlot.Ring2 => "ring",
        _ => s.ToString().ToLowerInvariant(),
    };

    private void ItemBox(DrawingContext dc, Rect r, Item? it, Action onClick, Action? onRight = null)
    {
        dc.FillRectangle(Trough, r);
        if (it is not null)
        {
            var icon = IconBitmap(Scene.IconKind(Engine, it));
            dc.DrawImage(icon, new Rect(0, 0, icon.PixelSize.Width, icon.PixelSize.Height), r.Deflate(6));
            if (it.Count > 1) DrawText(dc, it.Count.ToString(CultureInfo.InvariantCulture), r.Right - 4 - 7 * it.Count.ToString(CultureInfo.InvariantCulture).Length, r.Bottom - 16, 11, Parchment, face: Bold);
            if (!it.Identified) DrawText(dc, "?", r.X + 4, r.Y + 2, 12, Brushes.Orange, face: Bold);
        }
        dc.DrawRectangle(null, new Pen(it is null ? PanelEdge : RarityBrush(it.Rarity), it is null ? 1 : 2), r);
        ClickArea(r, onClick, onRight);
    }

    private static IBrush RarityBrush(Rarity r) => r switch
    {
        Rarity.Fine => Brushes.LightGreen,
        Rarity.Enchanted => Brushes.CornflowerBlue,
        Rarity.Masterwork => Brushes.Gold,
        Rarity.Relic => Brushes.Orange,
        Rarity.Set => Brushes.Turquoise,
        _ => Brushes.Gainsboro,
    };

    private unsafe WriteableBitmap IconBitmap(string kind)
    {
        if (_icons.TryGetValue(kind, out var bmp)) return bmp;
        var img = _art.ItemIcon(kind);
        bmp = new WriteableBitmap(new PixelSize(img.Width, img.Height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = bmp.Lock())
        {
            fixed (uint* p = img.Pixels)
                for (int y = 0; y < img.Height; y++)
                    Buffer.MemoryCopy(p + y * img.Width, (byte*)fb.Address + y * fb.RowBytes, fb.RowBytes, img.Width * 4L);
        }
        return _icons[kind] = bmp;
    }
}
