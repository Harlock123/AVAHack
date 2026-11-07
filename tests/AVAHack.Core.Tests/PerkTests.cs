using AVAHack.Characters;

namespace AVAHack.Core.Tests;

public sealed class PerkTests
{
    [Fact]
    public void A_perk_needs_a_point_the_rank_and_its_prerequisites()
    {
        var ama = Heroes.Ama(); // human: one point; Blade 5
        Assert.Equal("Needs Blade 20.", ama.WhyNotPerk("blade.quickDraw"));
        Assert.True(ama.BuyPerk("blade.edgeDiscipline"));
        Assert.Equal(1, ama.PerkRank("blade.edgeDiscipline"));
        Assert.Equal("No perk points to spend.", ama.WhyNotPerk("stealth.hush"));
        Assert.Equal(1.2, ama.DamageMultiplier("blade"), 9);
        Assert.Equal(1.0, ama.DamageMultiplier("blunt"), 9);
    }

    [Fact]
    public void Ranked_perks_need_more_skill_for_each_rank()
    {
        var b = Heroes.Brannoc(); // Blade 20
        Heroes.LevelTo(b, 4, except: SkillId.Blade);
        Assert.Equal(20, b.Rank(SkillId.Blade));
        Assert.True(b.BuyPerk("blade.edgeDiscipline"));
        Assert.True(b.BuyPerk("blade.edgeDiscipline")); // rank 2 needs 20
        Assert.Equal("Needs Blade 40.", b.WhyNotPerk("blade.edgeDiscipline"));
        Assert.Equal(1.4, b.DamageMultiplier("blade"), 9);
    }

    [Fact]
    public void Prerequisites_are_checked()
    {
        var b = Heroes.Brannoc();
        Heroes.LevelTo(b, 3);
        Assert.Equal("Needs Edge Discipline first.", b.WhyNotPerk("blade.quickDraw"));
        b.BuyPerk("blade.edgeDiscipline");
        Assert.Null(b.WhyNotPerk("blade.quickDraw"));
        Assert.True(b.BuyPerk("blade.quickDraw"));
        Assert.Equal("You have every rank of it.", b.WhyNotPerk("blade.quickDraw"));
        Assert.Equal(-15, b.PerkTotal("recoveryPct", "blade"));
    }

    [Fact]
    public void Perk_effects_sum_and_flags_are_found()
    {
        var i = Heroes.Ithren(); // Destruction 20
        Heroes.LevelTo(i, 3);
        i.BuyPerk("destruction.adeptFlame");
        Assert.Equal(1.25, i.DamageMultiplier("fire"), 9);
        Assert.Equal(1.0, i.DamageMultiplier("cold"), 9);
        Assert.Equal(-25, i.PerkTotal("manaCostPct", "fire"));
        Assert.False(i.HasPerkFlag("lingeringFlame"));
    }

    [Fact]
    public void Respec_refunds_every_point_and_costs_more_each_time()
    {
        var b = Heroes.Brannoc();
        Heroes.LevelTo(b, 3);
        int points = b.PerkPoints;
        b.BuyPerk("blade.edgeDiscipline");
        b.BuyPerk("blade.edgeDiscipline");
        int gold = 700;
        Assert.True(b.Respec(ref gold));
        Assert.Equal(500, gold);
        Assert.Equal(points, b.PerkPoints);
        Assert.Empty(b.Perks);
        b.BuyPerk("blade.edgeDiscipline");
        Assert.Equal(400, b.RespecCost);
        Assert.True(b.Respec(ref gold));
        Assert.Equal(100, gold);
        b.BuyPerk("blade.edgeDiscipline");
        Assert.False(b.Respec(ref gold)); // 800 needed
    }
}
