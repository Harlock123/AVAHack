using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;

namespace AVAHack.App.Views;

/// <summary>The crafting screen (K; spec 13.5): Alchemy, cooking, Smithing at an anvil and Enchanting at an altar.</summary>
public sealed partial class GameView
{
    private enum CraftTab { Alchemy, Cooking, Smithing, Enchanting }

    private CraftTab _craftTab;
    private string _craftSaid = "";
    private readonly List<int> _mortar = new();
    private Item? _enchantTarget;

    public void OpenCraft(string? station = null)
    {
        _craftTab = station switch { "anvil" => CraftTab.Smithing, "altar" => CraftTab.Enchanting, _ => _craftTab };
        _craftSaid = "";
        _mortar.Clear();
        _enchantTarget = null;
        Overlay = Overlay.Craft;
    }

    private void Crafted(string said)
    {
        _craftSaid = said;
        Engine.Say(said);
        _mortar.Clear();
    }

    private void DrawCraft(DrawingContext dc)
    {
        dc.FillRectangle(Shadow, new Rect(0, 0, CanvasWidth, CanvasHeight));
        var box = new Rect(110, 30, 1060, 660);
        dc.DrawRectangle(PanelBrush, new Pen(PanelEdge, 3), box, 8, 8);
        var h = Engine.Selected;
        DrawText(dc, "Crafting", box.X + 24, box.Y + 14, 28, Gold, face: Bold);
        DrawText(dc, $"{h.Name} · Alchemy {h.SkillLevel(SkillId.Alchemy)} · Survival {h.SkillLevel(SkillId.Survival)} · Smithing {h.SkillLevel(SkillId.Smithing)} · Enchanting {h.SkillLevel(SkillId.Enchanting)}",
            box.X + 200, box.Y + 24, 14, Parchment, maxWidth: 640);
        Button(dc, new Rect(box.Right - 200, box.Y + 14, 36, 32), "◀", () => { CycleSelected(); CycleSelected(); CycleSelected(); _mortar.Clear(); });
        Button(dc, new Rect(box.Right - 158, box.Y + 14, 36, 32), "▶", () => { CycleSelected(); _mortar.Clear(); });
        Button(dc, new Rect(box.Right - 110, box.Y + 14, 90, 32), "Close", () => Overlay = Overlay.None, size: 14);
        double x = box.X + 24;
        foreach (var t in Enum.GetValues<CraftTab>())
        {
            bool here = t switch { CraftTab.Smithing => Engine.AtStation("anvil"), CraftTab.Enchanting => Engine.AtStation("altar"), _ => true };
            Button(dc, new Rect(x, box.Y + 60, 170, 36), here ? t.ToString() : t + " (away)", () => { _craftTab = t; _mortar.Clear(); }, selected: _craftTab == t, size: 14);
            x += 180;
        }
        double y = box.Y + 112;
        switch (_craftTab)
        {
            case CraftTab.Alchemy: DrawAlchemy(dc, box, h, y); break;
            case CraftTab.Cooking: DrawCooking(dc, box, h, y); break;
            case CraftTab.Smithing: DrawSmithing(dc, box, h, y); break;
            case CraftTab.Enchanting: DrawEnchanting(dc, box, h, y); break;
        }
        if (_craftSaid.Length > 0) DrawText(dc, _craftSaid, box.X + 24, box.Bottom - 34, 14, Gold, maxWidth: box.Width - 48);
    }

    private void DrawAlchemy(DrawingContext dc, Rect box, Character h, double y)
    {
        DrawText(dc, Engine.HasTool("mortar") ? "Click two or three reagents, then Brew. Taste one to learn its effects." : "No mortar and pestle in the party: you can taste, but not brew.",
            box.X + 24, y, 14, Parchment);
        double yy = y + 28;
        for (int i = 0; i < h.Backpack.Length && yy < box.Bottom - 110; i++)
        {
            if (h.Backpack[i] is not { } it || Content.Reagent(it.BaseId) is not { } r) continue;
            int slot = i;
            var known = Engine.KnownEffects(r.Id).Select(e => Content.Effect(e).Name).ToList();
            string label = $"{it.Name(Content)}: {(known.Count == 0 ? "?" : string.Join(", ", known))}{new string(' ', 1)}{(known.Count < 4 ? "…" : "")}";
            Button(dc, new Rect(box.X + 24, yy, 760, 30), label, () =>
            {
                if (!_mortar.Remove(slot) && _mortar.Count < 3) _mortar.Add(slot);
            }, selected: _mortar.Contains(slot), size: 13, item: it);
            Button(dc, new Rect(box.X + 794, yy, 100, 30), "Taste", () => Crafted(Engine.Taste(h, slot)), size: 13);
            yy += 36;
        }
        if (yy == y + 28) DrawText(dc, $"{h.Name} carries no reagents.", box.X + 24, yy, 14, Dim);
        Button(dc, new Rect(box.Right - 180, box.Bottom - 80, 156, 38), $"Brew ({_mortar.Count})", () => Crafted(Engine.Brew(h, _mortar.ToList())),
            enabled: _mortar.Count >= 2 && Engine.HasTool("mortar"), size: 15);
    }

    private void DrawCooking(DrawingContext dc, Rect box, Character h, double y)
    {
        DrawText(dc, Engine.HasTool("campfireKit") ? "Cook at a campfire, away from enemies. Survival makes a meal's benefit last longer." : "No campfire kit in the party.",
            box.X + 24, y, 14, Parchment);
        double yy = y + 28;
        foreach (var r in Content.Recipes)
        {
            string needs = string.Join(" + ", r.Ingredients.GroupBy(i => i).Select(g => $"{(g.Count() > 1 ? g.Count() + "× " : "")}{Content.Base(g.Key).Name} ({Engine.Carried(g.Key)})"));
            string gives = $"food {r.Food:0}, water {r.Water:0}{(r.Buff is { } b ? $", {b}" : "")}";
            var why = Engine.WhyNotCook(r.Id);
            Button(dc, new Rect(box.X + 24, yy, 1010, 34), $"{r.Name}: {needs} → {gives}", () => Crafted(Engine.Cook(h, r.Id)), enabled: why is null, size: 13, item: new Item { BaseId = "meal:" + r.Id });
            yy += 40;
        }
    }

    private void DrawSmithing(DrawingContext dc, Rect box, Character h, double y)
    {
        if (!Engine.AtStation("anvil")) { DrawText(dc, "Smithing needs an anvil: a forge in the Deep Works, or a Waystone.", box.X + 24, y, 15, Parchment); return; }
        DrawText(dc, $"Scrap iron: {Engine.Carried("scrapIron")}. {h.Name} can work items up to +{GameEngine.QualityCap(h)}.", box.X + 24, y, 14, Parchment);
        DrawText(dc, "Improve", box.X + 24, y + 26, 14, Gold, face: Bold);
        double yy = y + 50;
        foreach (var it in h.Equipped.Values.Concat(h.Backpack.Where(i => i is not null).Select(i => i!)))
        {
            var b = Content.Base(it.BaseId);
            if (!(b.Kind is ItemKind.Weapon or ItemKind.Shield or ItemKind.Armour || b.Gear is { Ac: > 0 })) continue;
            if (yy > box.Bottom - 80) break;
            Button(dc, new Rect(box.X + 24, yy, 420, 30), $"{it.Name(Content)} → +{it.Quality + 1} ({GameEngine.ImproveCost(it)} scrap)", () => Crafted(Engine.Improve(h, it)),
                enabled: it.Quality < GameEngine.QualityCap(h), size: 12, item: it);
            int slot = Array.IndexOf(h.Backpack, it);
            if (slot >= 0) Button(dc, new Rect(box.X + 450, yy, 90, 30), "Melt", () => Crafted(Engine.Melt(h, slot)), size: 12);
            yy += 34;
        }
        DrawText(dc, "Forge", box.X + 580, y + 26, 14, Gold, face: Bold);
        yy = y + 50;
        foreach (var (b, scrap) in Engine.ForgeOptions(h).OrderByDescending(o => o.Base.Value).Take(12))
        {
            Button(dc, new Rect(box.X + 580, yy, 456, 30), $"{b.Name} ({scrap} scrap)", () => Crafted(Engine.Forge(h, b.Id)), enabled: Engine.Carried("scrapIron") >= scrap, size: 12, item: new Item { BaseId = b.Id });
            yy += 34;
        }
    }

    private void DrawEnchanting(DrawingContext dc, Rect box, Character h, double y)
    {
        if (!Engine.AtStation("altar")) { DrawText(dc, "Enchanting needs an enchanting altar, or a Waystone.", box.X + 24, y, 15, Parchment); return; }
        DrawText(dc, $"Spirit shards: {Engine.Carried("spiritShard")}. {h.Name} can bind up to {GameEngine.AffixCap(h)} affix{(GameEngine.AffixCap(h) == 1 ? "" : "es")} into one thing.",
            box.X + 24, y, 14, Parchment);
        DrawText(dc, "Items (click to choose; Unravel destroys it to learn an affix)", box.X + 24, y + 26, 14, Gold, face: Bold);
        double yy = y + 50;
        foreach (var it in h.Equipped.Values.Concat(h.Backpack.Where(i => i is not null).Select(i => i!)))
        {
            var b = Content.Base(it.BaseId);
            if (b.Kind is not (ItemKind.Weapon or ItemKind.Shield or ItemKind.Armour or ItemKind.Gear or ItemKind.Wand)) continue;
            if (yy > box.Bottom - 80) break;
            var target = it;
            Button(dc, new Rect(box.X + 24, yy, 420, 30), it.Name(Content), () => _enchantTarget = target, selected: _enchantTarget == it, size: 12, item: it);
            int slot = Array.IndexOf(h.Backpack, it);
            if (slot >= 0 && b.Kind == ItemKind.Wand) Button(dc, new Rect(box.X + 450, yy, 100, 30), "Recharge", () => Crafted(Engine.Recharge(h, slot)), size: 12);
            else if (slot >= 0 && it.Affixes.Count > 0) Button(dc, new Rect(box.X + 450, yy, 100, 30), "Unravel", () => Crafted(Engine.Disenchant(h, slot)), size: 12);
            yy += 34;
        }
        DrawText(dc, "Known enchantments", box.X + 580, y + 26, 14, Gold, face: Bold);
        yy = y + 50;
        if (Engine.Enchantments.Count == 0) DrawText(dc, "None yet: unravel an enchanted item to learn one.", box.X + 580, yy, 13, Dim);
        foreach (var a in Engine.Enchantments)
        {
            var def = Content.Affix(a);
            var t = _enchantTarget;
            string cost = t is null ? "" : $" ({GameEngine.EnchantCost(t)} shard{(GameEngine.EnchantCost(t) == 1 ? "" : "s")})";
            Button(dc, new Rect(box.X + 580, yy, 456, 30), $"{def.Name}: {def.Kind}{(def.Scope is { } s ? " " + s : "")}{cost}", () => { if (t is not null) Crafted(Engine.Enchant(h, t, a)); },
                enabled: t is not null, size: 12);
            yy += 34;
            if (yy > box.Bottom - 80) break;
        }
    }

    /// <summary>Food and water under the bars, when the Delve counts them.</summary>
    private void DrawMeters(DrawingContext dc, double x, double y, Character h)
    {
        if (!Engine.FoodOn) return;
        dc.FillRectangle(Trough, new Rect(x, y, 100, 5));
        dc.FillRectangle(h.Food < Character.HungryBelow ? Brushes.OrangeRed : Brushes.Goldenrod, new Rect(x, y, h.Food, 5));
        dc.FillRectangle(Trough, new Rect(x + 110, y, 100, 5));
        dc.FillRectangle(h.Water < Character.HungryBelow ? Brushes.OrangeRed : Brushes.SteelBlue, new Rect(x + 110, y, h.Water, 5));
        _ = CultureInfo.InvariantCulture;
    }
}
