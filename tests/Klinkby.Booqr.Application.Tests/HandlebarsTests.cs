using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Tests;

public sealed class HandlebarsTests
{
    [Theory]
    [AutoData]
    public void GIVEN_TemplateWithSingleToken_THEN_TokenIsReplaced([Frozen] string value)
    {
        var actual = Handlebars.Replace("Hello {{name}}",
            new() { ["name"] = value });

        Assert.Equal($"Hello {value}", actual);
    }

    [Theory]
    [AutoData]
    public void GIVEN_TemplateWithMultipleTokens_THEN_AllTokensAreReplaced(string value1,
        string value2)
    {
        var actual = Handlebars.Replace("Start {{first}}-mid-{{second}}-end",
            new()
            {
                ["first"] = value1,
                ["second"] = value2
            });

        Assert.Equal($"Start {value1}-mid-{value2}-end", actual);
    }

    [Theory]
    [AutoData]
    public void GIVEN_TemplateWithRepeatedToken_THEN_AllInstancesAreReplaced(string value)
    {
        var actual = Handlebars.Replace("{{value}},{{value}},{{value}}",
            new() { ["value"] = value });

        Assert.Equal($"{value},{value},{value}", actual);
    }

    [Theory]
    [AutoData]
    public void GIVEN_TemplateWithToken_HAVING_MissingReplacement_THEN_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Handlebars.Replace("X-{{present}}-Y-{{missing}}-Z",
                new() { ["present"] = value }));
    }


    [Fact]
    public void GIVEN_EmptyTemplate_THEN_ReturnsEmptyString()
    {
        var actual = Handlebars.Replace(ReadOnlySpan<char>.Empty, new());

        Assert.Equal(string.Empty, actual);
    }

    [Theory]
    [AutoData]
    public void GIVEN_TokensAtEdgesAndAdjacent_THEN_AllPositionsHandledCorrectly(string a, string b)
    {
        var actual = Handlebars.Replace("{{a}}--{{b}}{{a}}",
            new()
            {
                ["a"] = a,
                ["b"] = b
            });

        Assert.Equal($"{a}--{b}{a}", actual);
    }
}
