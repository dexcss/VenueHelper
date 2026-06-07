using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VenueHelper.Attributes;
using VenueHelper.Logic;
using VenueHelper.Windows;

namespace VenueHelper;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Venue Helper";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IChatGui Chat { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    public Configuration Configuration { get; init; }

    public VenueCounter Counter { get; init; }
    public RaffleService Raffle { get; init; }
    public AuctionService Auction { get; init; }
    public GiveawayTracker Giveaway { get; init; }
    public DeathrollManager Deathroll { get; init; }
    public TradeWatcher TradeWatcher { get; init; }
    public HookManager HookManager { get; init; }

    public readonly WindowSystem WindowSystem = new("VenueHelper");
    public MainWindow MainWindow { get; init; }

    private readonly PluginCommandManager<Plugin> commandManager;

    public Plugin()
    {
        // QuestPDF requires a license to be declared once before use. The
        // Community license is free for individuals and small businesses.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        Counter = new VenueCounter(this);
        Raffle = new RaffleService(this);
        Auction = new AuctionService(this);
        Giveaway = new GiveawayTracker(this);
        Deathroll = new DeathrollManager(this);
        TradeWatcher = new TradeWatcher(this);
        HookManager = new HookManager(this);

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        commandManager = new PluginCommandManager<Plugin>(this, CommandManager);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += OpenMain;

        Framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        Counter.Update();
        TradeWatcher.Update();
    }

    private void DrawUI() => WindowSystem.Draw();
    private void OpenMain() => MainWindow.IsOpen = true;

    // Returns the full "Name\uE05DWorld" of the current target, or "" if none /
    // not a player. Mirrors the proven targeting helper used by the references.
    public static string GetTargetName()
    {
        var target = TargetManager.SoftTarget ?? TargetManager.Target;
        if (target is not IPlayerCharacter pc || pc.HomeWorld.ValueNullable == null)
            return string.Empty;
        return $"{pc.Name}\uE05D{pc.HomeWorld.Value.Name}";
    }

    [Command("/venuehelper")]
    [Aliases("/vhelp", "/vh")]
    [HelpMessage("Opens the Venue Helper window (counter, raffle, auction).")]
    public void OnCommand(string command, string args)
    {
        MainWindow.IsOpen = true;
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenMain;

        commandManager.Dispose();
        HookManager.Dispose();
    }
}
