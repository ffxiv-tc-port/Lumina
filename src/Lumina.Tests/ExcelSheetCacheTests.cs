using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Excel.Exceptions;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Lumina.Tests;

[Sheet( "Addon" )]
public readonly struct CacheTestAddon( ExcelPage page, uint offset, uint row ) : IExcelRow< CacheTestAddon >
{
    public ExcelPage ExcelPage => page;
    public uint RowOffset => offset;
    public uint RowId => row;

    static CacheTestAddon IExcelRow< CacheTestAddon >.Create( ExcelPage page, uint offset, uint row ) => new( page, offset, row );
}

[Sheet( "GilShopItem" )]
public readonly struct CacheTestGilShopItem( ExcelPage page, uint offset, uint row, ushort subrow ) : IExcelSubrow< CacheTestGilShopItem >
{
    public ExcelPage ExcelPage => page;
    public uint RowOffset => offset;
    public uint RowId => row;
    public ushort SubrowId => subrow;

    static CacheTestGilShopItem IExcelSubrow< CacheTestGilShopItem >.Create( ExcelPage page, uint offset, uint row, ushort subrow ) =>
        new( page, offset, row, subrow );
}

// Called through reflection on a copy of this assembly loaded into a collectible AssemblyLoadContext.
public static class CollectibleSheetProbe
{
    public static bool RowTypeIsCollectible() => typeof( CacheTestAddon ).IsCollectible;

    public static bool SheetReused( ExcelModule module ) =>
        ReferenceEquals( module.GetSheet< CacheTestAddon >(), module.GetSheet< CacheTestAddon >() );
}

public class ExcelSheetCacheTests
{
    [RequiresGameInstallationFact]
    public void GetSheetReusesInstance()
    {
        var gameData = CreateGameData();

        var first = gameData.Excel.GetSheet< CacheTestAddon >();
        Assert.Same( first, gameData.Excel.GetSheet< CacheTestAddon >() );
        Assert.Same( first, gameData.GetExcelSheet< CacheTestAddon >() );
        Assert.Same( gameData.Excel.GetRawSheet( "Addon" ), first.RawSheet );
    }

    [RequiresGameInstallationFact]
    public void GetSubrowSheetReusesInstance()
    {
        var gameData = CreateGameData();

        var first = gameData.Excel.GetSubrowSheet< CacheTestGilShopItem >();
        Assert.Same( first, gameData.Excel.GetSubrowSheet< CacheTestGilShopItem >() );
        Assert.Same( first, gameData.GetSubrowExcelSheet< CacheTestGilShopItem >() );

        var rowId = first.GetRowAt( 0 ).RowId;
        Assert.Same( first, new SubrowRef< CacheTestGilShopItem >( gameData.Excel, rowId ).Value.Sheet );
    }

    [RequiresGameInstallationFact]
    public void DifferentSheetNamesDoNotShareInstance()
    {
        var gameData = CreateGameData();

        var addon = gameData.Excel.GetSheet< CacheTestAddon >();
        var logMessage = gameData.Excel.GetSheet< CacheTestAddon >( name: "LogMessage" );

        Assert.NotSame( addon, logMessage );
        Assert.Same( gameData.Excel.GetRawSheet( "LogMessage" ), logMessage.RawSheet );
        Assert.Same( logMessage, gameData.Excel.GetSheet< CacheTestAddon >( name: "LogMessage" ) );
        Assert.Same( addon, gameData.Excel.GetSheet< CacheTestAddon >() );
    }

    [RequiresGameInstallationFact]
    public void DifferentLanguagesDoNotShareInstance()
    {
        var gameData = CreateGameData();
        var multi = gameData.Excel.SheetNames
            .Select( n => ( Name: n, Header: gameData.GetFile< ExcelHeaderFile >( $"exd/{n}.exh" ) ) )
            .FirstOrDefault( s => s.Header?.Header.Variant == ExcelVariant.Default && s.Header.Languages.Count( l => l != Language.None ) >= 2 );

        if( multi.Header is null )
        {
            // Single-language client: every language resolves to the one raw sheet or throws, so there is nothing to mix up.
            var reference = gameData.Excel.GetSheet< CacheTestAddon >();
            var resolved = 0;
            foreach( var language in Enum.GetValues< Language >() )
            {
                gameData.Options.DefaultExcelLanguage = language;
                try
                {
                    Assert.Same( reference, gameData.Excel.GetSheet< CacheTestAddon >() );
                    resolved++;
                }
                catch( UnsupportedLanguageException )
                { }
            }

            Assert.Equal( 1, resolved );
            return;
        }

        var languages = multi.Header.Languages.Where( l => l != Language.None ).Take( 2 ).ToArray();
        gameData.Options.DefaultExcelLanguage = languages[ 0 ];
        var first = gameData.Excel.GetSheet< CacheTestAddon >( name: multi.Name );
        gameData.Options.DefaultExcelLanguage = languages[ 1 ];
        var second = gameData.Excel.GetSheet< CacheTestAddon >( name: multi.Name );

        Assert.NotSame( first, second );
        Assert.Equal( languages[ 0 ], first.Language );
        Assert.Equal( languages[ 1 ], second.Language );
        Assert.Same( second, gameData.Excel.GetSheet< CacheTestAddon >( name: multi.Name ) );

        gameData.Options.DefaultExcelLanguage = languages[ 0 ];
        Assert.Same( first, gameData.Excel.GetSheet< CacheTestAddon >( name: multi.Name ) );
    }

    [RequiresGameInstallationFact]
    public void ConcurrentFirstCallsConvergeOnOneInstance()
    {
        var gameData = CreateGameData();
        const int threads = 16;
        var results = new ExcelSheet< CacheTestAddon >[threads];
        using var barrier = new Barrier( threads );

        Parallel.For( 0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            barrier.SignalAndWait();
            results[ i ] = gameData.Excel.GetSheet< CacheTestAddon >();
        } );

        var final = gameData.Excel.GetSheet< CacheTestAddon >();
        Assert.All( results, r => Assert.Same( final, r ) );
    }

    [RequiresGameInstallationFact]
    public void CollectibleRowTypeIsNotCachedAndCanUnload()
    {
        var gameData = CreateGameData();

        Assert.False( CollectibleSheetProbe.RowTypeIsCollectible() );
        Assert.True( CollectibleSheetProbe.SheetReused( gameData.Excel ) );

        var context = RunProbeInCollectibleContext( gameData.Excel, out var isCollectible, out var reused );
        Assert.True( isCollectible );
        Assert.False( reused );

        for( var i = 0; context.IsAlive && i < 20; i++ )
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False( context.IsAlive, "The collectible AssemblyLoadContext is still referenced after unloading." );
    }

    // The default options ask for English, which single-language clients do not ship.
    private static GameData CreateGameData()
    {
        var gameData = RequiresGameInstallationFact.CreateGameData();
        var languages = gameData.GetFile< ExcelHeaderFile >( "exd/Addon.exh" )!.Languages;
        if( !languages.Contains( gameData.Options.DefaultExcelLanguage ) )
            gameData.Options.DefaultExcelLanguage = languages.First( l => l != Language.None );
        return gameData;
    }

    [MethodImpl( MethodImplOptions.NoInlining )]
    private static WeakReference RunProbeInCollectibleContext( ExcelModule module, out bool isCollectible, out bool reused )
    {
        var context = new CollectibleContext();
        var assembly = context.LoadFromAssemblyPath( typeof( ExcelSheetCacheTests ).Assembly.Location );
        var probe = assembly.GetType( typeof( CollectibleSheetProbe ).FullName! )!;

        isCollectible = (bool)probe.GetMethod( nameof( CollectibleSheetProbe.RowTypeIsCollectible ) )!.Invoke( null, null )!;
        reused = (bool)probe.GetMethod( nameof( CollectibleSheetProbe.SheetReused ) )!.Invoke( null, [module] )!;

        // SheetAttributeCache is keyed by Type and would otherwise keep the context alive on its own.
        module.UnloadTypedCache( assembly );
        context.Unload();
        return new WeakReference( context );
    }

    private sealed class CollectibleContext() : AssemblyLoadContext( isCollectible: true )
    {
        protected override Assembly Load( AssemblyName assemblyName ) => null;
    }
}
