﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Menu;
using Newtonsoft.Json;

namespace Deathmatch;

[MinimumApiVersion(216)]
public partial class Deathmatch : BasePlugin, IPluginConfig<DeathmatchConfig>
{
    public override string ModuleName => "Deathmatch Core";
    public override string ModuleAuthor => "Nocky";
    public override string ModuleVersion => "1.1.2";

    public void OnConfigParsed(DeathmatchConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        GetCSWeaponDataFromKeyFunc = new(GameData.GetSignature("GetCSWeaponDataFromKey"));
        CCSPlayer_CanAcquireFunc = new(GameData.GetSignature("CCSPlayer_CanAcquire"));
        CCSPlayer_CanAcquireFunc.Hook(OnWeaponCanAcquire, HookMode.Pre);
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);

        LoadCustomModes();
        LoadWeaponsRestrict();

        string[] Shortcuts = Config.CustomCommands.CustomShortcuts.Split(',');
        string[] WSelect = Config.CustomCommands.WeaponSelectCmds.Split(',');
        string[] DeathmatchMenus = Config.CustomCommands.DeatmatchMenuCmds.Split(',');
        foreach (var weapon in Shortcuts)
        {
            string[] Value = weapon.Split(':');
            if (Value.Length == 2)
            {
                AddCustomCommands(Value[1], Value[0], 1);
            }
        }
        foreach (var cmd in WSelect)
            AddCustomCommands(cmd, "", 2);
        foreach (var cmd in DeathmatchMenus)
            AddCustomCommands(cmd, "", 3);
        foreach (string radioName in RadioMessagesList)
            AddCommandListener(radioName, OnPlayerRadioMessage);
        AddCommandListener("autobuy", OnRandomWeapons);

        RegisterListener<Listeners.OnMapStart>(mapName =>
        {
            g_bDefaultMapSpawnDisabled = false;
            Server.NextFrame(() =>
            {
                SetupCustomMode(Config.Gameplay.MapStartMode.ToString());
                RemoveEntities();
                SetupDeathMatchConfigValues();
                SetupDeathmatchMenus();
                LoadMapSpawns(ModuleDirectory + $"/spawns/{mapName}.json", true);
                LoadCustomConfigFile();

                if (Config.Gameplay.IsCustomModes)
                {
                    AddTimer(1.0f, () =>
                    {
                        if (!GameRules().WarmupPeriod && ActiveMode != null)
                        {
                            g_iModeTimer++;
                            g_iRemainingTime = ActiveMode.Interval - g_iModeTimer;
                            if (g_iRemainingTime == 0)
                            {
                                SetupCustomMode(GetModeType().ToString());
                            }

                        }
                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
                }
                if (Config.General.ForceMapEnd)
                {
                    bool RoundTerminated = false;
                    var timelimit = ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>() * 60;
                    AddTimer(10.0f, () =>
                    {
                        var gameStart = GameRules().GameStartTime;
                        var currentTime = Server.CurrentTime;
                        var timeleft = timelimit - (currentTime - gameStart);
                        if (timeleft <= 0 && !RoundTerminated)
                        {
                            RoundTerminated = true;
                            GameRules().TerminateRound(0.1f, RoundEndReason.RoundDraw);
                        }

                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
                }
            });
        });
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (g_bIsActiveEditor)
            {
                foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && AdminManager.PlayerHasPermissions(p, "@css/root")))
                {
                    string CTSpawns = $"<font class='fontSize-m' color='cyan'>CT Spawns:</font> <font class='fontSize-m' color='green'>{spawnPositionsCT.Count}</font>";
                    string TSpawns = $"<font class='fontSize-m' color='orange'>T Spawns:</font> <font class='fontSize-m' color='green'>{spawnPositionsT.Count}</font>";
                    p.PrintToCenterHtml($"<font class='fontSize-l' color='red'>Spawns Editor</font><br>{CTSpawns}<br>{TSpawns}<br>");
                }
            }
            else
            {
                foreach (var p in Utilities.GetPlayers().Where(p => playerData.ContainsPlayer(p) && playerData[p].HudMessages))
                {
                    if (ActiveMode != null && !string.IsNullOrEmpty(ActiveMode.CenterMessageText) && MenuManager.GetActiveMenu(p) == null)
                    {
                        p.PrintToCenterHtml($"{ActiveMode.CenterMessageText}");
                    }
                    if (g_iRemainingTime <= Config.Gameplay.NewModeCountdown && Config.Gameplay.NewModeCountdown > 0)
                    {
                        if (g_iRemainingTime == 0)
                        {
                            p.PrintToCenter($"{Localizer["Hud.NewModeStarted"]}");
                        }
                        else
                        {
                            p.PrintToCenter($"{Localizer["Hud.NewModeStarting", g_iRemainingTime]}");
                        }
                    }
                }
            }
        });
    }
    public override void Unload(bool hotReload)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        CCSPlayer_CanAcquireFunc?.Unhook(OnWeaponCanAcquire, HookMode.Pre);
        playerData.Clear();
        AllowedPrimaryWeaponsList.Clear();
        AllowedSecondaryWeaponsList.Clear();
    }
    public void SetupCustomMode(string modeId)
    {
        ActiveMode = CustomModes[modeId];
        bool bNewmode = true;

        if (ActiveMode.SecondaryWeapons != null && ActiveMode.SecondaryWeapons.Count() > 0)
            AllowedSecondaryWeaponsList = new(ActiveMode.SecondaryWeapons);
        else
            AllowedSecondaryWeaponsList.Clear();

        if (ActiveMode.PrimaryWeapons != null && ActiveMode.PrimaryWeapons.Count() > 0)
            AllowedPrimaryWeaponsList = new(ActiveMode.PrimaryWeapons);
        else
            AllowedPrimaryWeaponsList.Clear();

        if (modeId.Equals(ActiveCustomMode.ToString()))
            bNewmode = false;

        ActiveCustomMode = int.Parse(modeId);
        SetupDeathmatchConfiguration(ActiveMode, bNewmode);
    }

    public void SetupDeathmatchConfiguration(ModeData mode, bool isNewMode)
    {
        g_iModeTimer = 0;

        if (isNewMode)
            Server.PrintToChatAll($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NewModeStarted", mode.Name]}");

        Server.ExecuteCommand($"mp_free_armor {mode.Armor};mp_damage_headshot_only {mode.OnlyHS};mp_ct_default_primary \"\";mp_t_default_primary \"\";mp_ct_default_secondary \"\";mp_t_default_secondary \"\"");

        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.PawnIsAlive))
        {
            p.RemoveWeapons();
            GivePlayerWeapons(p, true);
            if (mode.Armor != 0)
            {
                string armor = mode.Armor == 1 ? "item_kevlar" : "item_assaultsuit";
                p.GiveNamedItem(armor);
            }
            if (!p.IsBot)
            {
                if (!string.IsNullOrEmpty(Config.SoundSettings.NewModeSound))
                    p.ExecuteClientCommand("play " + Config.SoundSettings.NewModeSound);
                p.GiveNamedItem("weapon_knife");
            }
            if (Config.Gameplay.RespawnPlayersAtNewMode)
                p.Respawn();
        }
    }
    public void LoadCustomConfigFile()
    {
        string path = Server.GameDirectory + "/csgo/cfg/deathmatch/";
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (!File.Exists(path + "deathmatch.cfg"))
        {
            var content = @"
// Things you can customize and add your own cvars
mp_timelimit 30
mp_maxrounds 0
sv_disable_radar 1
sv_alltalk 1
mp_warmuptime 20
mp_freezetime 1
mp_death_drop_grenade 0
mp_death_drop_gun 0
mp_death_drop_healthshot 0
mp_drop_grenade_enable 0
mp_death_drop_c4 0
mp_death_drop_taser 0
mp_defuser_allocation 0
mp_solid_teammates 1
mp_give_player_c4 0
mp_playercashawards 0
mp_teamcashawards 0
cash_team_bonus_shorthanded 0
mp_autokick 0
mp_match_restart_delay 10

//Do not change or delete!!
mp_max_armor 0
mp_weapons_allow_typecount -1
mp_hostages_max 0
mp_weapons_allow_zeus 0
mp_buy_allow_grenades 0
            ";

            using (StreamWriter writer = new StreamWriter(path + "deathmatch.cfg"))
            {
                writer.Write(content);
            }
        }
        Server.ExecuteCommand("exec deathmatch/deathmatch.cfg");
    }
    public void LoadCustomModes()
    {
        string filePath = Server.GameDirectory + "/csgo/addons/counterstrikesharp/configs/plugins/Deathmatch/Deathmatch.json";
        if (File.Exists(filePath))
        {
            try
            {
                var jsonData = File.ReadAllText(filePath);
                dynamic deserializedJson = JsonConvert.DeserializeObject(jsonData)!;
                var modes = deserializedJson["Custom Modes"].ToObject<Dictionary<string, ModeData>>();
                CustomModes = new Dictionary<string, ModeData>(modes);
                SendConsoleMessage($"[Deathmatch] Loaded {CustomModes.Count()} Custom Modes", ConsoleColor.Green);

            }
            catch (Exception ex)
            {
                SendConsoleMessage($"[Deathmatch] An error occurred while loading Custom Modes: {ex.Message}", ConsoleColor.Red);
                throw new Exception($"An error occurred while loading Custom Modes: {ex.Message}");
            }
        }
    }

    public void LoadWeaponsRestrict()
    {
        string filePath = Server.GameDirectory + "/csgo/addons/counterstrikesharp/configs/plugins/Deathmatch/Deathmatch.json";
        if (File.Exists(filePath))
        {
            try
            {
                var jsonData = File.ReadAllText(filePath);
                dynamic deserializedJson = JsonConvert.DeserializeObject(jsonData)!;
                var weapons = deserializedJson["Weapons Restrict"]["Weapons"].ToObject<Dictionary<string, Dictionary<string, Dictionary<RestrictType, RestrictData>>>>();
                RestrictedWeapons = new(weapons);
                SendConsoleMessage($"[Deathmatch] Total Restricted Weapons: {RestrictedWeapons.Count()}", ConsoleColor.Green);
                /*foreach (var item in RestrictedWeapons)
                {
                    SendConsoleMessage(item.Key, ConsoleColor.Magenta);
                    foreach (var mode in item.Value)
                    {
                        SendConsoleMessage(mode.Key, ConsoleColor.Magenta);
                        foreach (var type in mode.Value)
                        {
                            SendConsoleMessage($"type - {type.Key}", ConsoleColor.Magenta);
                            var data = type.Value;
                            SendConsoleMessage($"data T - {data.T}", ConsoleColor.Magenta);
                            SendConsoleMessage($"data CT - {data.CT}", ConsoleColor.Magenta);
                            SendConsoleMessage($"data GLOBAL - {data.Global}", ConsoleColor.Magenta);
                        }
                    }
                }*/
            }
            catch (Exception ex)
            {
                SendConsoleMessage($"[Deathmatch] An error occurred while loading Weapons Restrictions: {ex.Message}", ConsoleColor.Red);
                throw new Exception($"An error occurred while loading Weapons Restrictions: {ex.Message}");
            }
        }
    }

    public void SetupDeathMatchConfigValues()
    {
        var gameType = ConVar.Find("game_type")!.GetPrimitiveValue<int>();

        IsCasualGamemode = gameType != 1;

        var iHideSecond = Config.General.HideRoundSeconds ? 1 : 0;
        var time = ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>();
        var iFFA = Config.Gameplay.IsFFA ? 1 : 0;
        Server.ExecuteCommand($"mp_teammates_are_enemies {iFFA};sv_hide_roundtime_until_seconds {iHideSecond};mp_roundtime_defuse {time};mp_roundtime {time};mp_roundtime_deployment {time};mp_roundtime_hostage {time};mp_respawn_on_death_ct 1;mp_respawn_on_death_t 1");

        if (Config.Gameplay.AllowBuyMenu)
            Server.ExecuteCommand("mp_buy_anywhere 1;mp_buytime 60000;mp_buy_during_immunity 0");
        else
            Server.ExecuteCommand("mp_buy_anywhere 0;mp_buytime 0;mp_buy_during_immunity 0");

        if (!IsCasualGamemode)
        {
            var TeamMode = Config.Gameplay.IsFFA ? 0 : 1;
            Server.ExecuteCommand($"mp_dm_teammode {TeamMode}; mp_dm_bonus_length_max 0;mp_dm_bonus_length_min 0;mp_dm_time_between_bonus_max 9999;mp_dm_time_between_bonus_min 9999;mp_respawn_immunitytime 0");
        }
    }
    public int GetModeType()
    {
        if (Config.Gameplay.IsCustomModes)
        {
            if (Config.Gameplay.RandomSelectionOfModes)
            {
                Random random = new Random();
                int iRandomMode;
                do
                {
                    iRandomMode = random.Next(0, CustomModes.Count);
                } while (iRandomMode == ActiveCustomMode);
                return iRandomMode;
            }
            else
            {
                if (ActiveCustomMode + 1 != CustomModes.Count && ActiveCustomMode + 1 < CustomModes.Count)
                    return ActiveCustomMode + 1;
                return 0;
            }
        }
        return Config.Gameplay.MapStartMode;
    }
    public static void SendConsoleMessage(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}