using System.Text;

namespace CommunitiesForever.Upload.Core.Tests;

public sealed class ContentHashTests
{
    [Fact]
    public async Task ComputesLowercaseSha256OfTheBytes()
    {
        using var content = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var hash = await ContentHash.ComputeAsync(content, TestContext.Current.CancellationToken);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public async Task SameBytesGiveTheSameHash()
    {
        var bytes = new byte[] { 1, 2, 3, 0, 255 };
        using var first = new MemoryStream(bytes);
        using var second = new MemoryStream(bytes);

        Assert.Equal(
            await ContentHash.ComputeAsync(first, TestContext.Current.CancellationToken),
            await ContentHash.ComputeAsync(second, TestContext.Current.CancellationToken));
    }
}
