using System.Xml.Linq;
using CommunitiesForever.Upload.Core.Notifications;
using Microsoft.Win32;

namespace CommunitiesForever.Upload.Windows.Tests;

/// <summary>Autostart, registration, toasts and the single instance, on test keys and a test name.</summary>
public sealed class TrayAdapterTests : IDisposable
{
    private readonly string _root = @"Software\CommunitiesForever.Upload.Tests\" + Guid.NewGuid().ToString("N");

    [Fact]
    public void AutostartAddsAndRemovesTheRunEntry()
    {
        var autostart = new RegistryAutostart(@"C:\Apps\Uploader.exe", _root + @"\Run");

        autostart.Apply(true);
        Assert.True(autostart.IsEnabled);
        using (var key = Registry.CurrentUser.OpenSubKey(_root + @"\Run"))
        {
            Assert.Equal("\"C:\\Apps\\Uploader.exe\" --tray", key!.GetValue(AppIdentity.DisplayName));
        }

        autostart.Apply(false);
        Assert.False(autostart.IsEnabled);
        autostart.Apply(false);
    }

    [Fact]
    public void RegistersTheNotificationSenderAndTheAppLink()
    {
        ShellRegistration.Register(@"C:\Apps\Uploader.exe", @"C:\Apps\app-icon.png", _root + @"\Classes");

        using var sender = Registry.CurrentUser.OpenSubKey($@"{_root}\Classes\AppUserModelId\{AppIdentity.AppId}");
        Assert.Equal(AppIdentity.DisplayName, sender!.GetValue("DisplayName"));
        Assert.Equal(@"C:\Apps\app-icon.png", sender.GetValue("IconUri"));
        using var command = Registry.CurrentUser.OpenSubKey($@"{_root}\Classes\{AppIdentity.Scheme}\shell\open\command");
        Assert.Equal("\"C:\\Apps\\Uploader.exe\" \"%1\"", command!.GetValue(""));
    }

    [Fact]
    public void StartMenuShortcutCarriesTheAppId()
    {
        string folder = Path.Combine(Path.GetTempPath(), "cfu-tests-" + Guid.NewGuid().ToString("N"));
        string shortcut = Path.Combine(folder, "Uploader.lnk");
        string exe = Path.Combine(AppContext.BaseDirectory, "testhost.exe");
        try
        {
            StartMenuShortcut.Ensure(exe, shortcut);
            StartMenuShortcut.Ensure(exe, shortcut);

            var read = StartMenuShortcut.Read(shortcut);
            Assert.NotNull(read);
            Assert.Equal(exe, read.Value.Target, ignoreCase: true);
            Assert.Equal(AppIdentity.AppId, read.Value.AppId);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ToastXmlHasTheTextLogoAndLinkButtons()
    {
        var notification = new AppNotification(
            "Uploaded · 5 levels gained",
            "Ves 37 → 39 & more",
            AppIdentity.Open,
            [new NotificationButton("See what changed", new Uri("https://wow.vestras.nl/upload/42")), new NotificationButton("Later", null)]);

        var toast = XElement.Parse(ToastXml.Build(notification, @"C:\Apps\app-icon.png"));

        Assert.Equal("communitiesforever-uploader://open/", toast.Attribute("launch")!.Value);
        Assert.Equal(["Uploaded · 5 levels gained", "Ves 37 → 39 & more"], toast.Descendants("text").Select(t => t.Value));
        Assert.Equal("file:///C:/Apps/app-icon.png", toast.Descendants("image").Single().Attribute("src")!.Value);
        var actions = toast.Descendants("action").ToList();
        Assert.Equal("protocol", actions[0].Attribute("activationType")!.Value);
        Assert.Equal("https://wow.vestras.nl/upload/42", actions[0].Attribute("arguments")!.Value);
        Assert.Equal("system", actions[1].Attribute("activationType")!.Value);
        Assert.Equal("dismiss", actions[1].Attribute("arguments")!.Value);
    }

    [Fact]
    public async Task ASecondStartHandsItsArgumentsToTheFirst()
    {
        string name = "CommunitiesForever.Upload.Tests." + Guid.NewGuid().ToString("N");
        using var first = new SingleInstance(name);
        var received = new TaskCompletionSource<string[]>();
        first.ArgumentsReceived += (_, args) => received.TrySetResult(args);
        first.Listen();

        using (var second = new SingleInstance(name))
        {
            Assert.True(first.IsFirst);
            Assert.False(second.IsFirst);
            Assert.True(second.Forward(["communitiesforever-uploader://reconnect"]));
        }

        string[] args = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(["communitiesforever-uploader://reconnect"], args);
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
}
