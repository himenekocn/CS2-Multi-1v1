using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2Multi1v1.Models;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;

namespace CS2Multi1v1;

public class CS2Multi1v1 : BasePlugin
{
    public override string ModuleName => "CS2";
    public override string ModuleVersion => "";
    public override string ModuleAuthor => "";
    public override string ModuleDescription => "S";

    private bool _aimMapLoaded;

    private ILogger<CS2Multi1v1> _logger;
    private Queue<ArenaPlayer> _waitingArenaPlayers;
    private List<Arena> _rankedArenas;

    public CS2Multi1v1(ILogger<CS2Multi1v1> logger)
    {
        _aimMapLoaded = false;
        _logger = logger;
        _waitingArenaPlayers = new Queue<ArenaPlayer>();
        _rankedArenas = new List<Arena>();
    }

    public override void Load(bool hotReload)
    {
        _logger.LogInformation("Loaded CS2Multi1v1!");

        RegisterEventHandler<EventGameNewmap>(OnGameNewmap);
        RegisterEventHandler<EventGameStart>(OnGameStart);
        RegisterEventHandler<EventMapTransition>(OnMapTransition);
        RegisterEventHandler<EventPlayerActivate>(OnPlayerActivate);
        RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
        RegisterEventHandler<EventRoundPrestart>(OnRoundPrestart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventSwitchTeam>(OnSwitchTeam, HookMode.Pre);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam, HookMode.Pre);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        
        RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);
        
        if (hotReload)
        {
            _logger.LogInformation("Detected hot reload...");
            // requeue/calc spawns??
        }
    }

    private void OnMapStartHandler(string mapName)
    {
        _aimMapLoaded = false;
        _waitingArenaPlayers.Clear();
        _rankedArenas.Clear();
        Server.NextFrame(() =>
        {
            Console.WriteLine($"[1v1 Debug] OnMapStart");
            Server.ExecuteCommand($"execifexists 1v1.cfg");
        });
        _logger.LogInformation($"Map: Reset");
    }

    // ----------------------------- SERVER RELATED GAME EVENT HOOKS -------------------------------------//

    public HookResult OnGameNewmap(EventGameNewmap @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    private HookResult OnGameStart(EventGameStart @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    public HookResult OnMapTransition(EventMapTransition @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    public HookResult OnPlayerActivate(EventPlayerActivate @event, GameEventInfo info)
    {
        CCSPlayerController playerController = @event.Userid;
        _logger.LogInformation($"Player Activated: {playerController.Connected.ToString()}");

        if (!playerController.IsValid) return HookResult.Continue;
        if (_rankedArenas.Where(x => x?._player1?.PlayerController == playerController).FirstOrDefault() != null) return HookResult.Continue;
        if (_rankedArenas.Where(x => x?._player2?.PlayerController == playerController).FirstOrDefault() != null) return HookResult.Continue;

        playerController.ChangeTeam(CsTeam.Spectator);

        ArenaPlayer arenaPlayer = new ArenaPlayer(playerController);
        _waitingArenaPlayers.Enqueue(arenaPlayer);
        _logger.LogInformation($"Player {arenaPlayer.PlayerController.PlayerName} added to waiting queue.");
        arenaPlayer.PrintToChat($"{ChatColors.Gold}你被移动到了等待队列.");
        arenaPlayer.PrintToChat($"{ChatColors.Gold}聊天栏输入 {ChatColors.LightRed}!help{ChatColors.Gold} 查看信息.");
        arenaPlayer.PrintToChat($"{ChatColors.LightRed}官网{ChatColors.Gold} himeneko.cn");

        return HookResult.Continue;
    }

    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        SetupArenasIfNeeded();
        return HookResult.Continue;
    }

    // ---------------------- ROUND RELATED GAME EVENT HOOKS -----------------------------//

    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
    {
        Queue<ArenaPlayer> arenaWinners = new Queue<ArenaPlayer>();
        Queue<ArenaPlayer> arenaLosers = new Queue<ArenaPlayer>();

        _logger.LogInformation("Prestart triggered");

        // Get winner and loser from each arena and add to appropriate queue | Going from arena 1 down
        foreach (Arena arena in _rankedArenas)
        {
            ArenaResult arenaResult = arena.GetArenaResult();

            // If there was a winner, handle normally
            if (arenaResult.ResultType == ArenaResultType.Win)
            {
                arenaWinners.Enqueue(arenaResult.Winner!);
                arenaLosers.Enqueue(arenaResult.Loser!);
            }
            // If someone had no opponent, consider it a win so they move up
            else if (arenaResult.ResultType == ArenaResultType.NoOpponent)
            {
                arenaWinners.Enqueue(arenaResult.Winner!);
            }
        }

        Queue<ArenaPlayer> rankedPlayers = new Queue<ArenaPlayer>(); // Holds players going from top rank to lowest

        // Top 2 winners should be in arena 1
        if (arenaWinners.Count > 1)
        {
            var p1 = arenaWinners.Dequeue();
            var p2 = arenaWinners.Dequeue();
            rankedPlayers.Enqueue(p1);
            rankedPlayers.Enqueue(p2);
        }

        // Middle arenas have loser from higher arena, and winner from lower arena
        while(arenaWinners.Count > 0)
        {
            var player = arenaWinners.Dequeue();
            rankedPlayers.Enqueue(player);

            // If there arent any losers to add, just keep adding winners
            if (arenaLosers.Count > 0)
            {
                player = arenaLosers.Dequeue();
                rankedPlayers.Enqueue(player);
            }
        }

        // If there are any remaining losers, add them
        while(arenaLosers.Count > 0)
        {
            rankedPlayers.Enqueue(arenaLosers.Dequeue());
        }

        // Add waiting users to the back of the queue
        while(_waitingArenaPlayers.Count > 0)
        {
            ArenaPlayer arenaPlayer = _waitingArenaPlayers.Dequeue();
            rankedPlayers.Enqueue(arenaPlayer);
        }

        _logger.LogInformation("Ranked Queue: ");
        foreach(ArenaPlayer p in rankedPlayers)
        {
            _logger.LogInformation(p.PlayerController.PlayerName);
        }

        // Shuffle arenas (gives player varied spawnpoints)
        Shuffle(_rankedArenas);

        int currentArenaIndex = 0;
        while(currentArenaIndex < _rankedArenas.Count)
        {
            // If 2+ players in ranked queue, add both to current arena
            if (rankedPlayers.Count > 1)
            {
                ArenaPlayer player1 = rankedPlayers.Dequeue();
                ArenaPlayer player2 = rankedPlayers.Dequeue();
                _rankedArenas[currentArenaIndex].AddPlayers(player1, player2, currentArenaIndex + 1);
                currentArenaIndex += 1;
            }
            // If 1 player in ranked queue, add them to the current arena with no opponent
            else if (rankedPlayers.Count == 1)
            {
                ArenaPlayer player1 = rankedPlayers.Dequeue();
                _rankedArenas[currentArenaIndex].AddPlayers(player1, null, currentArenaIndex + 1);
                currentArenaIndex += 1;
            }
            // If no more players in ranked queue, set the arena to have no players
            else
            {
                _rankedArenas[currentArenaIndex++].AddPlayers(null, null, currentArenaIndex + 1);
            }
        }

        return HookResult.Continue;
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        foreach(Arena arena in _rankedArenas) arena.OnRoundEnd();
        return HookResult.Continue;
    }

    // ---------------------- PLAYER RELATED GAME EVENT HOOKS -----------------------------//

    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }

    public HookResult OnSwitchTeam(EventSwitchTeam @event, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }

    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        foreach(Arena arena in _rankedArenas) arena.OnPlayerDeath(@event.Userid);
        return HookResult.Continue;
    }

    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        foreach(Arena arena in _rankedArenas) arena.OnPlayerSpawn(@event.Userid);
        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    // ------------------------------ COMMANDS ------------------------ //

    // General user information in long form
    [ConsoleCommand("css_help", "Help")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnHelp(CCSPlayerController player, CommandInfo commandInfo)
    {
        PrintToChatCustom(player, "----------------- CS2 多人 1v1 ----------");
        PrintToChatCustom(player, "1. 你将从最低阶级竞技场开始.");
        PrintToChatCustom(player, "2. 胜利会让你晋升与更高阶玩家对战.");
        PrintToChatCustom(player, "3. 失败会使你降级和更低阶玩家对战.");
        PrintToChatCustom(player, "4. 谁获得最多的击杀，谁就是本场比赛的最强者.");
        PrintToChatCustom(player, "5. 如果打成平手，谁最后一击获胜.");
        PrintToChatCustom(player, "--- 对局类型完全随机.");
        PrintToChatCustom(player, "--- 尚不支持挑战某一个玩家.");
        PrintToChatCustom(player, "--- 目前也还未支持选择枪械.");
        PrintToChatCustom(player, $"--- 插件还处于开发状态，请等待更新.");
    }

    // Show current players in waiting queue
    [RequiresPermissions("#css/admin")]
    [ConsoleCommand("css_sq", "Show Players in Waiting Queue")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnShowQueue(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if(player == null || !player.IsValid) return;

        player.PrintToChat("当前队列: ");
        foreach(ArenaPlayer p in _waitingArenaPlayers)
        {
            player.PrintToChat(p.PlayerController.PlayerName);
        }
    }

    // Remove all players from arenas, reset waiting queue, then add all currently connected players to waiting queue
    [RequiresPermissions("#css/admin")]
    [ConsoleCommand("css_rq", "Requeue All Current Players")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRequeue(CCSPlayerController? player, CommandInfo commandInfo)
    {
        SetupArenasIfNeeded();
        _waitingArenaPlayers.Clear();
        foreach(Arena arena in _rankedArenas) arena.AddPlayers(null, null);

        foreach(CCSPlayerController playerController in Utilities.GetPlayers())
        {
            if (playerController.IsValid && playerController.Connected == PlayerConnectedState.PlayerConnected)
            {
                _waitingArenaPlayers.Enqueue(new ArenaPlayer(playerController));
            }
        }

        Server.PrintToChatAll("重置完成");
    }

    // Console logs information for all arenas with 1+ players
    [RequiresPermissions("#css/admin")]
    [ConsoleCommand("css_arenainfo", "Console Log Arena Info")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnArenaInfo(CCSPlayerController? player, CommandInfo commandInfo)
    {
        foreach(Arena arena in _rankedArenas)
        {
            arena.LogCurrentInfo();
        }
    }

    // Remove all existing arenas, get map spawns, create new arenas
    [RequiresPermissions("#css/admin")]
    [ConsoleCommand("resetarenas", "Re-fetch Map Spawns and Fully Re-instantiate all Areanas")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnAsize(CCSPlayerController? player, CommandInfo commandInfo)
    {
        List<Tuple<SpawnPoint, SpawnPoint>> arenasSpawns = getArenasSpawns();
        foreach(Arena arena in _rankedArenas) arena.AddPlayers(null, null); // Neccesary to prevent memory leaks?
        _rankedArenas.Clear();

        int count = 0;
        foreach (Tuple<SpawnPoint, SpawnPoint> arenaSpawns in arenasSpawns)
        {
            Arena arena = new Arena(_logger, arenaSpawns);
            _rankedArenas.Add(arena);
            count++;
        }

        _aimMapLoaded = true;

        if (player != null && player.IsValid)
        {
            player.PrintToChat($"已成功实例化 {_rankedArenas.Count} 竞技场.");
        }
    }

    [RequiresPermissions("#css/admin")]
    [ConsoleCommand("css_endround", "End the Current Round")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnEndRound(CCSPlayerController? player, CommandInfo commandInfo)
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        gameRules.TerminateRound(3.0f, RoundEndReason.CTsWin);
    }

    // ---------------------------------- UTIL ---------------------------------------//

    public void SetupArenasIfNeeded()
    {
        if (!_aimMapLoaded)
        {
            List<Tuple<SpawnPoint, SpawnPoint>> arenasSpawns = getArenasSpawns();
            _rankedArenas.Clear();

            int count = 0;
            foreach (Tuple<SpawnPoint, SpawnPoint> arenaSpawns in arenasSpawns)
            {
                Arena arena = new Arena(_logger, arenaSpawns);
                _rankedArenas.Add(arena);
                count++;
            }
            _aimMapLoaded = true;
        }
    }

    private List<Tuple<SpawnPoint, SpawnPoint>> getArenasSpawns()
    {
        // Get all ct and t SpawnPoints on map
        var ctSpawns = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist").ToList();
        var tSpawns = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").ToList();

        var spawnPairs = new List<Tuple<SpawnPoint, SpawnPoint>>();

        // Assumes map has one ct and one t spawn per individual arena | Iterate thru ct spawns, finding the closest t spawn to pair it with | O(n)^2 but oh well for now
        foreach (var ctSpawn in ctSpawns)
        {
            SpawnPoint? closestTSpawn = null;
            float closestDistance = float.MaxValue;

            foreach (var tSpawn in tSpawns)
            {
                var ctVec = ctSpawn.CBodyComponent!.SceneNode!.AbsOrigin;
                var tVec = tSpawn.CBodyComponent!.SceneNode!.AbsOrigin;

                float distance = DistanceTo(ctVec, tVec);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTSpawn = tSpawn;
                }
            }

            if (closestTSpawn != null)
            {
                spawnPairs.Add(new Tuple<SpawnPoint, SpawnPoint>(ctSpawn, closestTSpawn));
            }
        }
        return spawnPairs;
    }

    private float DistanceTo(Vector a, Vector b)
    {
        return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
    }

    // In-place shuffle
    public static void Shuffle<T>(IList<T> list)
    {
        Random rng = new Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    public void PrintToChatCustom(CCSPlayerController playerController, string text)
    {
        playerController.PrintToChat($" {ChatColors.Olive}  HIME | {ChatColors.Default}{text}");
    }
}
