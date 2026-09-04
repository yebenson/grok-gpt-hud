using QuotaHud;

namespace QuotaHud.Tests;

public class PlanNameTests
{
    [Theory]
    [InlineData("go", "Go")]
    [InlineData("chatgpt_go", "Go")]
    [InlineData("plus", "Plus")]
    [InlineData("chatgpt_plus", "Plus")]
    [InlineData("pro", "Pro")]
    [InlineData("chatgpt_pro", "Pro")]
    [InlineData("team", "Team")]
    [InlineData("chatgpt_team", "Team")]
    [InlineData("business", "Business")]
    [InlineData("chatgpt_business", "Business")]
    [InlineData("enterprise", "Enterprise")]
    [InlineData("edu", "Edu")]
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
