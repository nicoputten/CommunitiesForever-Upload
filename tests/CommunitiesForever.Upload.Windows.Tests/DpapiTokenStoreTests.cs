using System.Text;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Windows.Tests;

public sealed class DpapiTokenStoreTests : IDisposable
{
    private const string Value = "cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cfu-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void KeepsTheTokenEncrypted()
    {
        var store = new DpapiTokenStore(_directory);

        store.Save(new AccessToken(Value));

        Assert.Equal(new AccessToken(Value), new DpapiTokenStore(_directory).Load());
        byte[] file = File.ReadAllBytes(Path.Combine(_directory, "token.bin"));
        Assert.DoesNotContain(Value, Encoding.UTF8.GetString(file), StringComparison.Ordinal);
        Assert.DoesNotContain(Value, Encoding.Unicode.GetString(file), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void NoTokenYetIsNull() => Assert.Null(new DpapiTokenStore(_directory).Load());

    [Fact]
    public void DamagedFileIsNull()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "token.bin"), [1, 2, 3]);

        Assert.Null(new DpapiTokenStore(_directory).Load());
    }

    [Fact]
    public void SaveReplacesAndDeleteRemoves()
    {
        var store = new DpapiTokenStore(_directory);
        string other = "cfu_" + new string('a', 43);

        store.Save(new AccessToken(Value));
        store.Save(new AccessToken(other));
        Assert.Equal(new AccessToken(other), store.Load());

        store.Delete();
        Assert.Null(store.Load());
        store.Delete();
    }

    [Fact]
    public void DeleteWithoutFolderIsFine() => new DpapiTokenStore(_directory).Delete();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
