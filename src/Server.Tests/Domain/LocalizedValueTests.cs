using Everdue.Server.Domain;

namespace Everdue.Server.Tests.Domain;

/// <summary>
/// Reading a number or a date a person typed, in a product shipped in Spanish and English.
///
/// These exist because of a real bug: with thousands separators allowed, <c>1200,5</c> parsed under the invariant
/// culture as <em>twelve thousand and five</em> — a silently corrupted value a thousand times too large, in the one
/// feature whose entire promise is that it only ever displays what you put in.
/// </summary>
public class LocalizedValueTests
{
    [Theory]
    [InlineData("1200.5", "1200.5")]
    [InlineData("1200,5", "1200.5")]
    [InlineData("-3", "-3")]
    [InlineData(" 42 ", "42")]
    [InlineData("0", "0")]
    public void A_number_reads_the_same_in_either_language(string input, string expected)
    {
        LocalizedValues.TryParseDecimal(input, out var parsed).ShouldBeTrue();
        LocalizedValues.Canonical(parsed).ShouldBe(expected);
    }

    [Theory]
    [InlineData("1.200,50")]
    [InlineData("1,200.50")]
    public void A_grouped_number_is_refused_rather_than_guessed(string input)
    {
        // Refusing with a message beats storing 1200.50 or 120050 depending on which culture happened to win.
        LocalizedValues.TryParseDecimal(input, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12 34")]
    public void Nonsense_is_refused(string? input)
    {
        LocalizedValues.TryParseDecimal(input, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-03-15")]
    [InlineData("15/03/2026")]
    [InlineData("3/15/2026")]
    public void A_date_reads_from_iso_or_either_language(string input)
    {
        LocalizedValues.TryParseDate(input, out var parsed).ShouldBeTrue();
        LocalizedValues.Canonical(parsed).ShouldBe("2026-03-15");
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("not-a-date")]
    [InlineData("")]
    public void An_impossible_date_is_refused(string input)
    {
        LocalizedValues.TryParseDate(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void A_custom_field_validates_through_the_same_rule()
    {
        var number = new EntityFieldDef { FieldType = EntityFieldType.Number, Name = "Capacity" };

        // The bug, as it would have reached the database.
        EntityCustomFields.Validate(number, "1200,5").Normalized.ShouldBe("1200.5");
        EntityCustomFields.Validate(number, "1.200,50").Ok.ShouldBeFalse();

        var date = new EntityFieldDef { FieldType = EntityFieldType.Date, Name = "Purchased" };

        // A Spanish spreadsheet's date, which the form's own date input never produces but an import does.
        EntityCustomFields.Validate(date, "15/03/2026").Normalized.ShouldBe("2026-03-15");
        EntityCustomFields.Validate(date, "31/02/2026").Ok.ShouldBeFalse();
    }

    [Fact]
    public void A_select_matches_case_insensitively_and_stores_the_defined_spelling()
    {
        var definition = new EntityFieldDef
        {
            FieldType = EntityFieldType.Select,
            Name = "Condition",
            OptionsJson = EntityCustomFields.SerializeOptions(["Good", "Needs service"]),
        };

        // Stored as defined, not as typed: otherwise the same option arrives three ways and nothing groups.
        EntityCustomFields.Validate(definition, "needs SERVICE").Normalized.ShouldBe("Needs service");
        EntityCustomFields.Validate(definition, "Broken").Ok.ShouldBeFalse();
    }

    [Fact]
    public void Clearing_a_value_is_always_allowed()
    {
        var definition = new EntityFieldDef { FieldType = EntityFieldType.Number, Name = "Capacity" };

        // Nothing here is ever required — a required-field workflow would make a custom field drive behaviour.
        var cleared = EntityCustomFields.Validate(definition, "   ");

        cleared.Ok.ShouldBeTrue();
        cleared.Normalized.ShouldBeNull();
    }

    [Fact]
    public void A_malformed_column_reads_as_empty_rather_than_throwing()
    {
        // A display-only field must never be able to make an entity unreadable.
        EntityCustomFields.Parse("{not json").ShouldBeEmpty();
        EntityCustomFields.Parse(null).ShouldBeEmpty();
        EntityCustomFields.ParseOptions("[[[").ShouldBeEmpty();

        // And an empty set stores nothing at all, rather than "{}".
        EntityCustomFields.Serialize(new Dictionary<Guid, string>()).ShouldBeNull();
    }
}
