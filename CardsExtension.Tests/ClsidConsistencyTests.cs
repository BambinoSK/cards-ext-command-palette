// The extension is found by Command Palette through its COM class id (CLSID).
// The repo manifest must carry the release id (it becomes the Store package),
// and the dev id used by Debug builds must be a different one so the dev copy
// can be installed next to the Store copy.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Xunit;

namespace CardsExtension.Tests;

public class ClsidConsistencyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CardsExtension.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root (CardsExtension.sln) not found.");
    }

    private static string ManifestText() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "CardsExtension", "Package.appxmanifest"));

    [Fact]
    public void Manifest_ClassIdAndCreateInstance_MatchReleaseId()
    {
        var manifest = ManifestText();

        var classId = Regex.Match(manifest, @"<com:Class\s+Id=""([^""]+)""");
        var createInstance = Regex.Match(manifest, @"<CreateInstance\s+ClassId=""([^""]+)""");

        Assert.True(classId.Success, "com:Class Id not found in manifest");
        Assert.True(createInstance.Success, "CreateInstance ClassId not found in manifest");
        Assert.Equal(Guid.Parse(ExtensionIdentity.ReleaseClsid), Guid.Parse(classId.Groups[1].Value));
        Assert.Equal(Guid.Parse(ExtensionIdentity.ReleaseClsid), Guid.Parse(createInstance.Groups[1].Value));
    }

    [Fact]
    public void Manifest_IdentityName_IsNotTheDevName()
    {
        var name = Regex.Match(ManifestText(), @"<Identity[^>]*?Name=""([^""]+)""", RegexOptions.Singleline);

        Assert.True(name.Success, "Identity Name not found in manifest");
        Assert.NotEqual(ExtensionIdentity.DevPackageName, name.Groups[1].Value);
        Assert.StartsWith(name.Groups[1].Value + ".", ExtensionIdentity.DevPackageName);
    }

    [Fact]
    public void DevClsid_DiffersFromReleaseClsid_AndIsAValidGuid()
    {
        Assert.True(Guid.TryParse(ExtensionIdentity.DevClsid, out var dev));
        Assert.NotEqual(Guid.Parse(ExtensionIdentity.ReleaseClsid), dev);
    }

    [Fact]
    public void CodeClsid_IsTheIdForTheCurrentConfiguration()
    {
        var attribute = typeof(CardsExtension).GetCustomAttribute<GuidAttribute>();
        Assert.NotNull(attribute);
#if DEBUG
        Assert.Equal(Guid.Parse(ExtensionIdentity.DevClsid), Guid.Parse(attribute!.Value));
#else
        Assert.Equal(Guid.Parse(ExtensionIdentity.ReleaseClsid), Guid.Parse(attribute!.Value));
#endif
    }
}
