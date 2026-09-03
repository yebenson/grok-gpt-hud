using QuotaHud;

namespace QuotaHud.Tests;

public class PlanNameTests
{
    [Theory]
    [InlineData("pro", "Pro")]
    [InlineData("chatgpt_pro", "Pro")]
    [InlineData("plus", "Plus")]
    [InlineData("business", "Business")]
    [InlineData("team", "Team")]
    [InlineData("supergrok", "SuperGrok")]
    [InlineData("super_grok", "SuperGrok")]
    public void Maps_known_plans(string raw, string expected) => Assert.Equal(expected, PlanName.Display(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("free")]
    [InlineData("1")]
    [InlineData("unknown")]
    public void Hides_unknown_or_free_plans(string? raw) => Assert.Null(PlanName.Display(raw));
}
