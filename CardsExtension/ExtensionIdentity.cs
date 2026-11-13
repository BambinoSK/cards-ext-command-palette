namespace CardsExtension;

/// <summary>
/// The two identities of this extension. Release builds (the Store package)
/// use the release values. Debug builds use the dev values.
/// </summary>
internal static class ExtensionIdentity
{
    /// <summary>COM class id used by the Store package. Must match Package.appxmanifest.</summary>
    internal const string ReleaseClsid = "a99fc43d-a3a3-48c7-b087-23dd12091ed4";

    /// <summary>COM class id of the local dev package. Never appears in the repo manifest.</summary>
    internal const string DevClsid = "8872ba2b-5909-43a4-8d51-b3aeb6a108b4";

    /// <summary>Package name of the local dev package (read by the csproj).</summary>
    internal const string DevPackageName = "BambinoSK.CardsForCommandPalette.Dev";

    /// <summary>Name shown for the dev copy (read by the csproj).</summary>
    internal const string DevDisplayName = "Cards (Dev)";

#if DEBUG
    internal const string Clsid = DevClsid;
    internal const string DisplayName = DevDisplayName;
    internal const string ProviderId = "cards-dev";
    internal const string StorageName = "CardsExtension.Dev";
#else
    internal const string Clsid = ReleaseClsid;
    internal const string DisplayName = "Cards";
    internal const string ProviderId = "cards";
    internal const string StorageName = "CardsExtension";
#endif
}
