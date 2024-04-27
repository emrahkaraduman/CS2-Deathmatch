using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace Deathmatch
{
    public partial class Deathmatch
    {
        public void SetupPlayerWeapons(CCSPlayerController player, string weaponName, CommandInfo info)
        {
            if (string.IsNullOrEmpty(weaponName))
            {
                string primaryWeapons = "";
                string secondaryWeapons = "";
                if (AllowedPrimaryWeaponsList.Count != 0)
                {
                    foreach (string weapon in AllowedPrimaryWeaponsList)
                    {
                        if (string.IsNullOrEmpty(primaryWeapons))
                        {
                            primaryWeapons = $"{ChatColors.Green} {Localizer[weapon]}";
                        }
                        else
                        {
                            primaryWeapons = $"{primaryWeapons}{ChatColors.Default}, {ChatColors.Green}{Localizer[weapon]}";
                        }
                    }
                    info.ReplyToCommand($"{Localizer["Chat.ListOfAllowedWeapons"]}");
                    info.ReplyToCommand($"{ChatColors.DarkRed}• {primaryWeapons.ToUpper()}");
                }
                if (AllowedSecondaryWeaponsList.Count != 0)
                {
                    foreach (string weapon in AllowedSecondaryWeaponsList)
                    {
                        if (string.IsNullOrEmpty(secondaryWeapons))
                        {
                            secondaryWeapons = $"{ChatColors.Green} {Localizer[weapon]}";
                        }
                        else
                        {
                            secondaryWeapons = $"{secondaryWeapons}{ChatColors.Default}, {ChatColors.Green}{Localizer[weapon]}";
                        }
                    }
                    info.ReplyToCommand($"{Localizer["Chat.ListOfAllowedSecondaryWeapons"]}");
                    info.ReplyToCommand($"{ChatColors.DarkRed}• {secondaryWeapons.ToUpper()}");
                }
                info.ReplyToCommand($"{Localizer["Chat.Prefix"]} /gun <weapon name>");
                return;
            }

            string replacedweaponName = "";
            string matchingValues = "";
            int matchingCount = 0;
            if (weaponSelectMapping.ContainsKey(weaponName))
            {
                weaponName = weaponSelectMapping[weaponName];
                matchingCount = 1;
            }
            else
            {
                foreach (string weapon in PrimaryWeaponsList.Concat(SecondaryWeaponsList))
                {
                    if (weapon.Contains(weaponName))
                    {
                        replacedweaponName = weapon;
                        matchingCount++;
                        string localizerWeaponName = Localizer[replacedweaponName];
                        if (matchingCount == 1)
                        {
                            matchingValues = $"{ChatColors.Green}{localizerWeaponName}";
                        }
                        else if (matchingCount > 1)
                        {
                            matchingValues = $"{matchingValues}{ChatColors.Default}, {ChatColors.Green}{localizerWeaponName}";
                        }
                    }
                }

                if (matchingCount != 0)
                {
                    weaponName = replacedweaponName;
                }
            }

            if (matchingCount > 1)
            {
                info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MultipleWeaponsSelected"]} {ChatColors.Default}( {matchingValues} {ChatColors.Default})");
                return;
            }
            else if (matchingCount == 0)
            {
                if (!string.IsNullOrEmpty(Config.SoundSettings.CantEquipSound))
                    player.ExecuteClientCommand("play " + Config.SoundSettings.CantEquipSound);
                info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponNotFound", weaponName]}");
                return;
            }
            bool IsVIP = AdminManager.PlayerHasPermissions(player, Config.PlayersSettings.VIPFlag);
            if (AllowedPrimaryWeaponsList.Contains(weaponName))
            {
                string localizerWeaponName = Localizer[weaponName];
                if (weaponName == playerData[player].PrimaryWeapon)
                {
                    if (!string.IsNullOrEmpty(Config.SoundSettings.CantEquipSound))
                        player.ExecuteClientCommand("play " + Config.SoundSettings.CantEquipSound);
                    info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponsIsAlreadySet", localizerWeaponName]}");
                    return;
                }
                if (CheckIsWeaponRestricted(weaponName, IsVIP, player.Team))
                {
                    if (!string.IsNullOrEmpty(Config.SoundSettings.CantEquipSound))
                        player.ExecuteClientCommand("play " + Config.SoundSettings.CantEquipSound);

                    var restrictInfo = GetRestrictData(weaponName, IsVIP, player.Team);
                    info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponIsRestricted", localizerWeaponName, restrictInfo.Item1, restrictInfo.Item2]}");
                    return;
                }
                playerData[player].PrimaryWeapon = weaponName;
                info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.PrimaryWeaponSet", localizerWeaponName]}");
                if (player.PawnIsAlive && IsHaveWeaponFromSlot(player, 1) != 1)
                {
                    player.GiveNamedItem(weaponName);
                }
                else if (Config.Gameplay.SwitchWeapons && player.PawnIsAlive)
                {
                    string weapon = GetWeaponFromSlot(player, 2);

                    player.RemoveWeapons();
                    player.GiveNamedItem("weapon_knife");
                    player.GiveNamedItem(weaponName);
                    if (!string.IsNullOrEmpty(weapon))
                        player.GiveNamedItem(weapon);
                }
                return;
            }
            else if (AllowedSecondaryWeaponsList.Contains(weaponName))
            {
                string localizerWeaponName = Localizer[weaponName];
                if (weaponName == playerData[player].SecondaryWeapon)
                {
                    if (!string.IsNullOrEmpty(Config.SoundSettings.CantEquipSound))
                        player.ExecuteClientCommand("play " + Config.SoundSettings.CantEquipSound);
                    info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponsIsAlreadySet", localizerWeaponName]}");
                    return;
                }
                if (CheckIsWeaponRestricted(weaponName, IsVIP, player.Team))
                {
                    if (!string.IsNullOrEmpty(Config.SoundSettings.CantEquipSound))
                        player.ExecuteClientCommand("play " + Config.SoundSettings.CantEquipSound);

                    var restrictInfo = GetRestrictData(weaponName, IsVIP, player.Team);
                    info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponIsRestricted", localizerWeaponName, restrictInfo.Item1, restrictInfo.Item2]}");
                    return;
                }
                playerData[player].SecondaryWeapon = weaponName;
                info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.SecondaryWeaponSet", localizerWeaponName]}");
                if (player.PawnIsAlive && IsHaveWeaponFromSlot(player, 2) != 2)
                {
                    player.GiveNamedItem(weaponName);
                }
                else if (Config.Gameplay.SwitchWeapons && player.PawnIsAlive)
                {
                    string weapon = GetWeaponFromSlot(player, 1);

                    player.RemoveWeapons();
                    player.GiveNamedItem("weapon_knife");
                    player.GiveNamedItem(weaponName);
                    if (!string.IsNullOrEmpty(weapon))
                        player.GiveNamedItem(weapon);
                }
                return;
            }
            else
            {
                if (!string.IsNullOrEmpty(Config.SoundSettings.CantEquipSound))
                    player.ExecuteClientCommand("play " + Config.SoundSettings.CantEquipSound);
                string localizerWeaponName = Localizer[weaponName];
                info.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponIsDisabled", localizerWeaponName]}");
                return;
            }
        }
        public void GivePlayerWeapons(CCSPlayerController player, bool bNewMode)
        {
            if (playerData.ContainsPlayer(player))
            {
                player.InGameMoneyServices!.Account = Config.Gameplay.AllowBuyMenu ? 16000 : 0;
                if (!bNewMode)
                {
                    var timer = AdminManager.PlayerHasPermissions(player, Config.PlayersSettings.VIPFlag) ? Config.PlayersSettings.ProtectionTimeVIP : Config.PlayersSettings.ProtectionTime;
                    if (timer > 0.1)
                    {
                        playerData[player].SpawnProtection = true;
                        AddTimer(timer, () =>
                        {
                            playerData[player].SpawnProtection = false;
                        }, TimerFlags.STOP_ON_MAPCHANGE);
                    }

                    if (!IsCasualGamemode && !blockRandomWeaponsIntegeration.Contains(player))
                    {
                        blockRandomWeaponsIntegeration.Add(player);
                        AddTimer(0.25f, () => blockRandomWeaponsIntegeration.Remove(player), TimerFlags.STOP_ON_MAPCHANGE);
                    }
                }

                int slot = IsHaveWeaponFromSlot(player, 0);
                if (slot == 1 || slot == 2 || ActiveMode == null)
                    return;

                bool IsVIP = AdminManager.PlayerHasPermissions(player, Config.PlayersSettings.VIPFlag);
                if (AllowedPrimaryWeaponsList.Count != 0)
                {
                    string PrimaryWeapon = playerData[player].PrimaryWeapon;
                    if (ActiveMode.RandomWeapons)
                    {
                        PrimaryWeapon = GetRandomWeaponFromList(AllowedPrimaryWeaponsList);
                    }
                    else if (string.IsNullOrEmpty(PrimaryWeapon) || !AllowedPrimaryWeaponsList.Contains(PrimaryWeapon))
                    {
                        PrimaryWeapon = AllowedPrimaryWeaponsList.Count switch
                        {
                            1 => AllowedPrimaryWeaponsList[0],
                            _ => Config.Gameplay.DefaultModeWeapons switch
                            {
                                2 => GetRandomWeaponFromList(AllowedPrimaryWeaponsList),
                                1 => AllowedPrimaryWeaponsList[0],
                                _ => ""
                            }
                        };
                    }
                    else
                    {
                        if (CheckIsWeaponRestricted(PrimaryWeapon, IsVIP, player.Team))
                        {
                            playerData[player].PrimaryWeapon = "";
                            string localizerWeaponName = Localizer[PrimaryWeapon];
                            var restrictInfo = GetRestrictData(PrimaryWeapon, IsVIP, player.Team);
                            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponIsRestricted", localizerWeaponName, restrictInfo.Item1, restrictInfo.Item2]}");

                            PrimaryWeapon = Config.Gameplay.DefaultModeWeapons switch
                            {
                                2 => GetRandomWeaponFromList(AllowedPrimaryWeaponsList),
                                1 => AllowedPrimaryWeaponsList[0],
                                _ => ""
                            };
                        }
                    }

                    if (string.IsNullOrEmpty(PrimaryWeapon))
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.SetupWeaponsCommand"]}");
                    }
                    else
                    {
                        player.GiveNamedItem(PrimaryWeapon);
                    }
                }

                if (AllowedSecondaryWeaponsList.Count != 0)
                {
                    string SecondaryWeapon = playerData[player].SecondaryWeapon;
                    if (ActiveMode.RandomWeapons)
                    {
                        SecondaryWeapon = GetRandomWeaponFromList(AllowedSecondaryWeaponsList);
                    }
                    else if (string.IsNullOrEmpty(SecondaryWeapon) || !AllowedSecondaryWeaponsList.Contains(SecondaryWeapon))
                    {
                        SecondaryWeapon = AllowedSecondaryWeaponsList.Count switch
                        {
                            1 => AllowedSecondaryWeaponsList[0],
                            _ => Config.Gameplay.DefaultModeWeapons switch
                            {
                                2 => GetRandomWeaponFromList(AllowedSecondaryWeaponsList),
                                1 => AllowedSecondaryWeaponsList[0],
                                _ => ""
                            }
                        };
                    }
                    else
                    {
                        if (CheckIsWeaponRestricted(SecondaryWeapon, IsVIP, player.Team))
                        {
                            playerData[player].SecondaryWeapon = "";
                            string localizerWeaponName = Localizer[SecondaryWeapon];

                            var restrictInfo = GetRestrictData(SecondaryWeapon, IsVIP, player.Team);
                            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.WeaponIsRestricted", localizerWeaponName, restrictInfo.Item1, restrictInfo.Item2]}");

                            SecondaryWeapon = Config.Gameplay.DefaultModeWeapons switch
                            {
                                2 => GetRandomWeaponFromList(AllowedSecondaryWeaponsList),
                                1 => AllowedSecondaryWeaponsList[0],
                                _ => ""
                            };
                        }
                    }

                    if (string.IsNullOrEmpty(SecondaryWeapon))
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.SetupWeaponsCommand"]}");
                    }
                    else
                    {
                        player.GiveNamedItem(SecondaryWeapon);
                    }
                }
                if (ActiveMode.Utilities != null && ActiveMode.Utilities.Count() > 0)
                {
                    foreach (var item in ActiveMode.Utilities)
                        player.GiveNamedItem(item);
                }
                return;
            }

            AddTimer(0.2f, () =>
            {
                if (player == null || !player.IsValid)
                    return;

                if (player.InGameMoneyServices != null)
                    player.InGameMoneyServices.Account = 0;

                int slot = IsHaveWeaponFromSlot(player, slot: 0);
                if (slot == 1 || slot == 2)
                    return;

                if (AllowedPrimaryWeaponsList.Count != 0)
                {
                    player.GiveNamedItem(GetRandomWeaponFromList(AllowedPrimaryWeaponsList));
                }
                if (AllowedSecondaryWeaponsList.Count != 0)
                {
                    player.GiveNamedItem(GetRandomWeaponFromList(AllowedSecondaryWeaponsList));
                }
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        public static bool IsPlayerValid(CCSPlayerController player)
        {
            if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV || player.SteamID.ToString().Length != 17)
                return false;
            return true;
        }

        private static bool GetPrefsValue(CCSPlayerController player, int preference)
        {
            switch (preference)
            {
                case 1:
                    return playerData[player].KillSound;
                case 2:
                    return playerData[player].HSKillSound;
                case 3:
                    return playerData[player].KnifeKillSound;
                case 4:
                    return playerData[player].HitSound;
                case 5:
                    return playerData[player].OnlyHS;
                default:
                    return playerData[player].HudMessages;
            }
        }
        private void SwitchPrefsValue(CCSPlayerController player, int preference)
        {
            string changedValue;
            string Preference;
            switch (preference)
            {
                case 1:
                    playerData[player].KillSound = !playerData[player].KillSound;
                    changedValue = playerData[player].KillSound ? Localizer["Menu.Enabled"] : Localizer["Menu.Disabled"];
                    Preference = Localizer["Prefs.KillSound"];
                    break;
                case 2:
                    playerData[player].HSKillSound = !playerData[player].HSKillSound;
                    changedValue = playerData[player].HSKillSound ? Localizer["Menu.Enabled"] : Localizer["Menu.Disabled"];
                    Preference = Localizer["Prefs.HeadshotKillSound"];
                    break;
                case 3:
                    playerData[player].KnifeKillSound = !playerData[player].KnifeKillSound;
                    changedValue = playerData[player].KnifeKillSound ? Localizer["Menu.Enabled"] : Localizer["Menu.Disabled"];
                    Preference = Localizer["Prefs.KnifeKillSound"];
                    break;
                case 4:
                    playerData[player].HitSound = !playerData[player].HitSound;
                    changedValue = playerData[player].HitSound ? Localizer["Menu.Enabled"] : Localizer["Menu.Disabled"];
                    Preference = Localizer["Prefs.HitSound"];
                    break;
                case 5:
                    playerData[player].OnlyHS = !playerData[player].OnlyHS;
                    changedValue = playerData[player].OnlyHS ? Localizer["Menu.Enabled"] : Localizer["Menu.Disabled"];
                    Preference = Localizer["Prefs.OnlyHS"];
                    break;
                default:
                    playerData[player].HudMessages = !playerData[player].HudMessages;
                    changedValue = playerData[player].HudMessages ? Localizer["Menu.Enabled"] : Localizer["Menu.Disabled"];
                    Preference = Localizer["Prefs.HudMessages"];
                    break;
            }
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Prefs.ValueChanged", Preference, changedValue]}");
        }

        //https://github.com/K4ryuu/K4-System/blob/dev/src/Plugin/PluginCache.cs
        public class PlayerCache<T> : Dictionary<int, T>
        {
            public T this[CCSPlayerController controller]
            {
                get
                {
                    if (controller is null || !controller.IsValid || controller.SteamID.ToString().Length != 17)
                    {
                        throw new ArgumentException("Invalid player controller");
                    }

                    if (controller.IsBot || controller.IsHLTV)
                    {
                        throw new ArgumentException("Player controller is BOT or HLTV");
                    }

                    if (!ContainsKey(controller.Slot))
                    {
                        throw new KeyNotFoundException($"Player with ID {controller.Slot} not found in cache");
                    }

                    if (TryGetValue(controller.Slot, out T? value))
                    {
                        return value;
                    }

                    return default(T)!;
                }
                set
                {
                    if (controller is null || !controller.IsValid || !controller.PlayerPawn.IsValid || controller.SteamID.ToString().Length != 17)
                    {
                        throw new ArgumentException("Invalid player controller");
                    }

                    if (controller.IsBot || controller.IsHLTV)
                    {
                        throw new ArgumentException("Player controller is BOT or HLTV");
                    }

                    this[controller.Slot] = value;
                }
            }

            public bool ContainsPlayer(CCSPlayerController player)
            {
                if (player == null || !player.IsValid || !player.PlayerPawn.IsValid || player.SteamID.ToString().Length != 17)
                    return false;

                if (player.IsBot || player.IsHLTV)
                    return false;

                return ContainsKey(player.Slot);
            }

            public bool RemovePlayer(CCSPlayerController player)
            {
                if (player == null || !player.IsValid || !player.PlayerPawn.IsValid || player.SteamID.ToString().Length != 17)
                {
                    throw new ArgumentException("Invalid player controller");
                }

                if (player.IsBot || player.IsHLTV)
                {
                    throw new ArgumentException("Player controller is BOT or HLTV");
                }

                return Remove(player.Slot);
            }
        }
    }
}