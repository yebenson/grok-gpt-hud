using QuotaHud;

namespace QuotaHud.Tests;

public class DashboardTests
{
    [Fact]
    public void Never_forwards_access_tokens_and_masks_emails()
    {
        var state = DashboardState.Build(
            source: "hermes",
            phase: "ready",
            loaded: true,
            grokSideError: null,
            chatgptSideError: null,
            grokResults:
            [
                new AccountResult
                {
                    Account = new Account
                    {
                        Id = "g1",
                        Side = "grok",
                        Email = "benson@outlook.com",
                        Label = "work",
                        AccessToken = "secret-token",
                    },
                    Ok = true,
                    Quota = new AccountQuota { RemainingPercent = 72, Color = "blue", Display = "72%" },
                },
            ],
            chatgptResults: [],
            isVisible: (_, _, _) => true);

        var dumped = System.Text.Json.JsonSerializer.Serialize(state);
        Assert.DoesNotContain("secret-token", dumped);
        Assert.Equal("b****n@outlook.com", state.Grok.Accounts[0].Email);
        Assert.Null(state.Grok.Accounts[0].AccessToken);
    }
}
