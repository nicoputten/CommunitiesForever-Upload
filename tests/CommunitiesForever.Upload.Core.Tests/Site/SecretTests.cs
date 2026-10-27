using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Core.Tests.Site;

public sealed class SecretTests
{
    private const string Value = "cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy";

    [Fact]
    public void SecretsNeverShowInText()
    {
        // Logs, exceptions and records print values with ToString (CLAUDE.md rule 4).
        var token = new AccessToken(Value);
        var poll = new TokenPoll(PollStatus.Granted, token);

        Assert.DoesNotContain(Value, token.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Value, poll.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Value, $"{token}", StringComparison.Ordinal);
        Assert.DoesNotContain("kq3V0", new DeviceCode("kq3V0dXyPZ8m1nR2c9WbTg").ToString(), StringComparison.Ordinal);
        Assert.Equal(Value, token.Reveal());
    }

    [Theory]
    [InlineData(Value, true)]
    [InlineData("  " + Value + "\n", true)]
    [InlineData("cfu_short", false)]
    [InlineData("xyz_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy", false)]
    [InlineData("cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLEx!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PastedTokensMustHaveTheFormat(string? text, bool valid)
    {
        Assert.Equal(valid, AccessToken.TryParse(text, out var token));
        Assert.Equal(valid, token is not null);
    }

    [Fact]
    public void SecretsCompareByValue()
    {
        Assert.Equal(new AccessToken(Value), new AccessToken(Value));
        Assert.NotEqual<Secret>(new AccessToken(Value), new DeviceCode(Value));
    }
}
