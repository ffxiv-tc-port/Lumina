using System;
using System.IO;
using Xunit;

namespace Lumina.Tests;

public sealed class RequiresGameInstallationFact : FactAttribute
{
    private const string DefaultPath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";

    // LUMINA_TEST_SQPACK points the tests at another installation's sqpack directory, e.g. a regional client.
    private static readonly string Path = Environment.GetEnvironmentVariable( "LUMINA_TEST_SQPACK" ) is { Length: > 0 } path ? path : DefaultPath;

    public RequiresGameInstallationFact()
    {
        if( !Directory.Exists( Path ) )
            Skip = "Game installation is not found at the default path or LUMINA_TEST_SQPACK.";
    }

    public static GameData CreateGameData()
    {
        return new( Path, new()
        {
            PanicOnSheetChecksumMismatch = false,
        } );
    }
}