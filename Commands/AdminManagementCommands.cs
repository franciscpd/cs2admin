using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using CS2Admin.Services;
using CS2Admin.Utils;

namespace CS2Admin.Commands;

public class AdminManagementCommands
{
    private readonly PluginConfig _config;
    private readonly AdminService _adminService;

    private static readonly string[] ValidFlags =
    [
        "@css/kick",
        "@css/ban",
        "@css/slay",
        "@css/chat",
        "@css/changemap",
        "@css/generic",
        "@css/root",
        "@css/vip",
        "@css/reservation"
    ];

    public AdminManagementCommands(PluginConfig config, AdminService adminService)
    {
        _config = config;
        _adminService = adminService;
    }

    public void RegisterCommands(BasePlugin plugin)
    {
        plugin.AddCommand("css_add_admin", "Add a new admin", OnAddAdminCommand);
        plugin.AddCommand("css_remove_admin", "Remove an admin", OnRemoveAdminCommand);
        plugin.AddCommand("css_list_admins", "List all admins", OnListAdminsCommand);
        plugin.AddCommand("css_add_group", "Add a new admin group", OnAddGroupCommand);
        plugin.AddCommand("css_remove_group", "Remove an admin group", OnRemoveGroupCommand);
        plugin.AddCommand("css_list_groups", "List all admin groups", OnListGroupsCommand);
        plugin.AddCommand("css_set_group", "Set admin's group", OnSetGroupCommand);
        plugin.AddCommand("css_reload_admins", "Reload admins from database", OnReloadAdminsCommand);
    }

    [RequiresPermissions("@css/root")]
    private void OnAddAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 3)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_add_admin <steamid|player> <flags>");
            command.ReplyToCommand($"{_config.ChatPrefix} Flags: @css/kick,@css/ban,@css/slay,@css/root,...");
            return;
        }

        var targetArg = command.GetArg(1);
        var flags = command.GetArg(2);

        // Validate flags
        var flagList = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var flag in flagList)
        {
            if (!ValidFlags.Contains(flag))
            {
                command.ReplyToCommand($"{_config.ChatPrefix} Invalid flag: {flag}");
                return;
            }
        }

        ulong steamId;
        string playerName;

        // Try to find online player first
        var player = PlayerFinder.Find(targetArg);
        if (player != null)
        {
            steamId = player.SteamID;
            playerName = player.PlayerName;
        }
        else if (ulong.TryParse(targetArg, out var parsedSteamId) && parsedSteamId > 76561197960265728)
        {
            steamId = parsedSteamId;
            playerName = "Unknown";
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Invalid player or SteamID.");
            return;
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_adminService.AddAdmin(steamId, playerName, flags, adminSteamId, adminName))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Admin added: {playerName} ({steamId}) with flags: {flags}");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Admin already exists for this SteamID.");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnRemoveAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_remove_admin <steamid|player>");
            return;
        }

        var targetArg = command.GetArg(1);

        ulong steamId;

        var player = PlayerFinder.Find(targetArg);
        if (player != null)
        {
            steamId = player.SteamID;
        }
        else if (ulong.TryParse(targetArg, out var parsedSteamId) && parsedSteamId > 76561197960265728)
        {
            steamId = parsedSteamId;
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Invalid player or SteamID.");
            return;
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_adminService.RemoveAdmin(steamId, adminSteamId, adminName))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Admin removed: {steamId}");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} No admin found for this SteamID.");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnListAdminsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var admins = _adminService.GetAllAdmins();

        if (admins.Count == 0)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} No admins found in database.");
            return;
        }

        command.ReplyToCommand($"{_config.ChatPrefix} Admins ({admins.Count}):");
        foreach (var admin in admins)
        {
            var groupInfo = !string.IsNullOrEmpty(admin.GroupName) ? $" [Group: {admin.GroupName}]" : "";
            command.ReplyToCommand($"  {admin.PlayerName} ({admin.SteamId}): {admin.Flags}{groupInfo}");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnAddGroupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 3)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_add_group <name> <flags> [immunity]");
            command.ReplyToCommand($"{_config.ChatPrefix} Example: css_add_group moderator @css/kick,@css/ban,@css/chat 50");
            return;
        }

        var name = command.GetArg(1);
        var flags = command.GetArg(2);
        var immunity = 0;

        if (command.ArgCount > 3 && int.TryParse(command.GetArg(3), out var parsedImmunity))
        {
            immunity = parsedImmunity;
        }

        // Validate flags
        var flagList = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var flag in flagList)
        {
            if (!ValidFlags.Contains(flag))
            {
                command.ReplyToCommand($"{_config.ChatPrefix} Invalid flag: {flag}");
                return;
            }
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_adminService.AddGroup(name, flags, immunity, adminSteamId, adminName))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Group created: {name} with flags: {flags}, immunity: {immunity}");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Group already exists with this name.");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnRemoveGroupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_remove_group <name>");
            return;
        }

        var name = command.GetArg(1);
        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_adminService.RemoveGroup(name, adminSteamId, adminName))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Group removed: {name}");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Group not found.");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnListGroupsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var groups = _adminService.GetAllGroups();

        if (groups.Count == 0)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} No groups found in database.");
            return;
        }

        command.ReplyToCommand($"{_config.ChatPrefix} Groups ({groups.Count}):");
        foreach (var group in groups)
        {
            command.ReplyToCommand($"  {group.Name}: {group.Flags} (immunity: {group.Immunity})");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnSetGroupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 3)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_set_group <steamid|player> <group_name>");
            return;
        }

        var targetArg = command.GetArg(1);
        var groupName = command.GetArg(2);

        ulong steamId;

        var player = PlayerFinder.Find(targetArg);
        if (player != null)
        {
            steamId = player.SteamID;
        }
        else if (ulong.TryParse(targetArg, out var parsedSteamId) && parsedSteamId > 76561197960265728)
        {
            steamId = parsedSteamId;
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Invalid player or SteamID.");
            return;
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_adminService.SetAdminGroup(steamId, groupName, adminSteamId, adminName))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Admin {steamId} assigned to group: {groupName}");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Admin or group not found.");
        }
    }

    [RequiresPermissions("@css/root")]
    private void OnReloadAdminsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        _adminService.LoadAdminsToGame();
        command.ReplyToCommand($"{_config.ChatPrefix} Admins reloaded from database.");
    }

    #region Chat Command Handlers

    public bool HandleAddAdmin(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 2)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .add_admin <steamid|player> <flags>");
            return true;
        }

        var targetArg = args[0];
        var flags = args[1];

        var flagList = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var flag in flagList)
        {
            if (!ValidFlags.Contains(flag))
            {
                player.PrintToChat($"{_config.ChatPrefix} Invalid flag: {flag}");
                return true;
            }
        }

        ulong steamId;
        string playerName;

        var target = PlayerFinder.Find(targetArg);
        if (target != null)
        {
            steamId = target.SteamID;
            playerName = target.PlayerName;
        }
        else if (ulong.TryParse(targetArg, out var parsedSteamId) && parsedSteamId > 76561197960265728)
        {
            steamId = parsedSteamId;
            playerName = "Unknown";
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Invalid player or SteamID.");
            return true;
        }

        if (_adminService.AddAdmin(steamId, playerName, flags, player.SteamID, player.PlayerName))
        {
            player.PrintToChat($"{_config.ChatPrefix} Admin added: {playerName} ({steamId})");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Admin already exists.");
        }

        return true;
    }

    public bool HandleRemoveAdmin(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .remove_admin <steamid|player>");
            return true;
        }

        var targetArg = args[0];
        ulong steamId;

        var target = PlayerFinder.Find(targetArg);
        if (target != null)
        {
            steamId = target.SteamID;
        }
        else if (ulong.TryParse(targetArg, out var parsedSteamId) && parsedSteamId > 76561197960265728)
        {
            steamId = parsedSteamId;
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Invalid player or SteamID.");
            return true;
        }

        if (_adminService.RemoveAdmin(steamId, player.SteamID, player.PlayerName))
        {
            player.PrintToChat($"{_config.ChatPrefix} Admin removed.");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Admin not found.");
        }

        return true;
    }

    public bool HandleListAdmins(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        var admins = _adminService.GetAllAdmins();

        if (admins.Count == 0)
        {
            player.PrintToChat($"{_config.ChatPrefix} No admins found.");
            return true;
        }

        player.PrintToChat($"{_config.ChatPrefix} Admins ({admins.Count}):");
        foreach (var admin in admins.Take(10))
        {
            player.PrintToChat($"  {admin.PlayerName} ({admin.SteamId})");
        }

        if (admins.Count > 10)
        {
            player.PrintToChat($"  ... and {admins.Count - 10} more. Use console for full list.");
        }

        return true;
    }

    public bool HandleAddGroup(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 2)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .add_group <name> <flags> [immunity]");
            return true;
        }

        var name = args[0];
        var flags = args[1];
        var immunity = args.Length > 2 && int.TryParse(args[2], out var i) ? i : 0;

        var flagList = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var flag in flagList)
        {
            if (!ValidFlags.Contains(flag))
            {
                player.PrintToChat($"{_config.ChatPrefix} Invalid flag: {flag}");
                return true;
            }
        }

        if (_adminService.AddGroup(name, flags, immunity, player.SteamID, player.PlayerName))
        {
            player.PrintToChat($"{_config.ChatPrefix} Group created: {name}");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Group already exists.");
        }

        return true;
    }

    public bool HandleRemoveGroup(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .remove_group <name>");
            return true;
        }

        if (_adminService.RemoveGroup(args[0], player.SteamID, player.PlayerName))
        {
            player.PrintToChat($"{_config.ChatPrefix} Group removed.");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Group not found.");
        }

        return true;
    }

    public bool HandleListGroups(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        var groups = _adminService.GetAllGroups();

        if (groups.Count == 0)
        {
            player.PrintToChat($"{_config.ChatPrefix} No groups found.");
            return true;
        }

        player.PrintToChat($"{_config.ChatPrefix} Groups ({groups.Count}):");
        foreach (var group in groups)
        {
            player.PrintToChat($"  {group.Name} (immunity: {group.Immunity})");
        }

        return true;
    }

    public bool HandleSetGroup(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 2)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .set_group <steamid|player> <group_name>");
            return true;
        }

        var targetArg = args[0];
        var groupName = args[1];

        ulong steamId;

        var target = PlayerFinder.Find(targetArg);
        if (target != null)
        {
            steamId = target.SteamID;
        }
        else if (ulong.TryParse(targetArg, out var parsedSteamId) && parsedSteamId > 76561197960265728)
        {
            steamId = parsedSteamId;
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Invalid player or SteamID.");
            return true;
        }

        if (_adminService.SetAdminGroup(steamId, groupName, player.SteamID, player.PlayerName))
        {
            player.PrintToChat($"{_config.ChatPrefix} Admin assigned to group: {groupName}");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Admin or group not found.");
        }

        return true;
    }

    public bool HandleReloadAdmins(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        _adminService.LoadAdminsToGame();
        player.PrintToChat($"{_config.ChatPrefix} Admins reloaded.");
        return true;
    }

    #endregion
}
