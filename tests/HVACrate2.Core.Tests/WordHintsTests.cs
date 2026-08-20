using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class WordHintsTests
{
    [Test]
    [Arguments("Прозорец 1", true)]
    [Arguments("Window Frame", true)]
    [Arguments("Random Layer", false)]
    public async Task ContainsAny_Window_MatchesAcrossLanguagesCaseInsensitively(string text, bool expected)
    {
        bool result = WordHints.ContainsAny(text, WordHints.Window);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Врата балкон", true)]
    [Arguments("Door Marker", true)]
    [Arguments("Wall segment", false)]
    public async Task ContainsAny_Door_MatchesAcrossLanguages(string text, bool expected)
    {
        bool result = WordHints.ContainsAny(text, WordHints.Door);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ContainsAny_IsCaseInsensitive()
    {
        await Assert.That(WordHints.ContainsAny("WINDOW", WordHints.Window)).IsTrue();
        await Assert.That(WordHints.ContainsAny("wInDoW", WordHints.Window)).IsTrue();
    }

    [Test]
    [Arguments("Furniture dimension", true)]
    [Arguments("Мебели", true)]
    [Arguments("Стени - интериор", true)]
    [Arguments("Стени - екстериор", false)]
    public async Task ContainsAny_NonOpening_FlagsKnownFalsePositiveSources(string text, bool expected)
    {
        bool result = WordHints.ContainsAny(text, WordHints.NonOpening);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ContainsAny_ExplicitInterior_DoesNotMatchGenericWallHint()
    {
        await Assert.That(WordHints.ContainsAny("Стени - интериор", WordHints.ExplicitInterior)).IsTrue();
        await Assert.That(WordHints.ContainsAny("Стени - екстериор", WordHints.ExplicitInterior)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_EmptyText_ReturnsFalse()
    {
        await Assert.That(WordHints.ContainsAny("", WordHints.Window)).IsFalse();
    }
}
