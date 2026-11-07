using AVAHack.Content;

namespace AVAHack.Magic;

internal static class SpellNames
{
    public static string Of(ContentDb db, string id) => db.Spells.FirstOrDefault(s => s.Id == id) is { } s ? s.Id : id;
}
