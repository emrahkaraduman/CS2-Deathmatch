using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CounterStrikeSharp.API.Modules.Utils;
using System.Globalization;

namespace Deathmatch
{
    public partial class Deathmatch
    {
        public void PerformRespawn(CCSPlayerController player, CsTeam team, bool IsBot)
        {
            if (player == null || !player.IsValid || player.PlayerPawn.Value == null || player.PawnIsAlive || team == CsTeam.None || team == CsTeam.Spectator)
                return;

            var spawnsDictionary = team == CsTeam.Terrorist ? spawnPositionsT : spawnPositionsCT;
            var spawnsList = spawnsDictionary.ToList();
            if (!IsBot)
                spawnsList.RemoveAll(x => x.Key == playerData[player].LastSpawn);

            if (spawnsList.Count == 0)
            {
                player.Respawn();
                SendConsoleMessage("[Deathmatch] Spawns list is empty, you got something wrong!", ConsoleColor.Red);
                return;
            }

            if (GameRules().WarmupPeriod || !Config.Gameplay.CheckDistance)
            {
                var randomSpawn = spawnsDictionary.ElementAt(Random.Next(spawnsDictionary.Count));
                if (!IsBot)
                    playerData[player].LastSpawn = randomSpawn.Key;

                if (blockedSpawns.ContainsKey(player.Slot))
                {
                    var blockedSpawn = blockedSpawns[player.Slot];
                    if (!spawnsDictionary.ContainsKey(blockedSpawn.Item1))
                        spawnsDictionary.Add(blockedSpawn.Item1, blockedSpawn.Item2);

                    blockedSpawns[player.Slot] = (randomSpawn.Key, randomSpawn.Value);
                }
                else
                    blockedSpawns.Add(player.Slot, (randomSpawn.Key, randomSpawn.Value));

                player.Respawn();
                player.PlayerPawn.Value.Teleport(randomSpawn.Key, randomSpawn.Value, new Vector());
                if (spawnsDictionary.ContainsKey(randomSpawn.Key))
                    spawnsDictionary.Remove(randomSpawn.Key);
                return;
            }

            var Spawn = GetAvailableSpawn(player, spawnsList);
            if (!IsBot)
                playerData[player].LastSpawn = Spawn.Item1;

            if (blockedSpawns.ContainsKey(player.Slot))
            {
                var blockedSpawn = blockedSpawns[player.Slot];
                if (!spawnsDictionary.ContainsKey(blockedSpawn.Item1))
                    spawnsDictionary.Add(blockedSpawn.Item1, blockedSpawn.Item2);

                blockedSpawns[player.Slot] = (Spawn.Item1, Spawn.Item2);
            }
            else
                blockedSpawns.Add(player.Slot, (Spawn.Item1, Spawn.Item2));

            player.Respawn();
            player.PlayerPawn.Value.Teleport(Spawn.Item1, Spawn.Item2, new Vector());
            if (spawnsDictionary.ContainsKey(Spawn.Item1))
                spawnsDictionary.Remove(Spawn.Item1);
        }

        private (Vector, QAngle) GetAvailableSpawn(CCSPlayerController player, List<KeyValuePair<Vector, QAngle>> spawnsList)
        {
            var allPlayers = Utilities.GetPlayers();
            var playerPositions = allPlayers
                .Where(p => !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && p.PlayerPawn.IsValid && p.PawnIsAlive && p != player)
                .Select(p => p.PlayerPawn.Value!.AbsOrigin)
                .ToList();

            var availableSpawns = new List<KeyValuePair<Vector, QAngle>>();
            foreach (KeyValuePair<Vector, QAngle> spawn in spawnsList)
            {
                double closestDistance = 4000;
                foreach (var playerPos in playerPositions)
                {
                    if (playerPos == null)
                        continue;

                    double distance = GetDistance(playerPos, spawn.Key);
                    //Console.WriteLine($"Distance {distance} | {closestDistance}");
                    if (distance < closestDistance)
                    {
                        //Console.WriteLine($"ClosestDistance Distance {distance}");
                        closestDistance = distance;
                    }
                }
                if (closestDistance > CheckedEnemiesDistance)
                {
                    //Console.WriteLine($"closestDistance {closestDistance} > DistanceRespawn {Config.Gameplay.DistanceRespawn}");
                    availableSpawns.Add(spawn);
                }
            }

            if (availableSpawns.Count > 0)
            {
                //SendConsoleMessage($"[Deathmatch] Player {player.PlayerName} was respawned, available spawns found: {availableSpawns.Count})", ConsoleColor.DarkYellow);
                var randomAvailableSpawn = availableSpawns.ElementAt(Random.Next(availableSpawns.Count));
                return (randomAvailableSpawn.Key, randomAvailableSpawn.Value);
            }
            SendConsoleMessage($"[Deathmatch] Player {player.PlayerName} was respawned, but no available spawn point was found! Therefore, a random spawn was selected. (T {spawnPositionsT.Count()} : CT {spawnPositionsCT.Count()})", ConsoleColor.DarkYellow);
            var randomSpawn = spawnsList.ElementAt(Random.Next(spawnsList.Count));
            return (randomSpawn.Key, randomSpawn.Value);
        }

        public void AddNewSpawnPoint(string filepath, Vector posValue, QAngle angleValue, string team)
        {
            string FormatValue(float value)
            {
                return value.ToString("N2", CultureInfo.InvariantCulture);
            }

            string formattedPosValue = $"{FormatValue(posValue.X)} {FormatValue(posValue.Y)} {FormatValue(posValue.Z)}";
            string formattedAngleValue = $"{FormatValue(angleValue.X)} {FormatValue(angleValue.Y)} {FormatValue(angleValue.Z)}";

            //Server.PrintToChatAll($"Edited: {formattedPosValue} | {formattedAngleValue}");
            //Server.PrintToChatAll($"Default: {posValue} | {angleValue}");
            if (!File.Exists(filepath))
            {
                JObject newRow = new JObject
                {
                    { "team", team },
                    { "pos", formattedPosValue },
                    { "angle", formattedAngleValue }
                };

                JObject jsonData = new JObject
                {
                    { "spawnpoints", new JArray(newRow) }
                };
                File.WriteAllText(filepath, jsonData.ToString());
            }
            else
            {
                string jsonContent = File.ReadAllText(filepath);
                JObject jsonData = JsonConvert.DeserializeObject<JObject>(jsonContent)!;

                JObject newRow = new JObject
                {
                    { "team", team },
                    { "pos", formattedPosValue },
                    { "angle", formattedAngleValue }
                };

                JArray spawnpointsArray = (JArray)jsonData["spawnpoints"]!;
                spawnpointsArray.Add(newRow);

                string updatedJsonContent = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
                File.WriteAllText(filepath, updatedJsonContent);
            }
            LoadMapSpawns(ModuleDirectory + $"/spawns/{Server.MapName}.json", false);
            RemoveBeams();
            ShowAllSpawnPoints();
        }

        public bool RemoveSpawnPoint(string filepath, string posValue)
        {
            try
            {
                if (!File.Exists(filepath))
                {
                    return false;
                }
                string jsonContent = File.ReadAllText(filepath);
                JObject jsonData = JObject.Parse(jsonContent);

                JArray spawnpointsArray = (JArray)jsonData["spawnpoints"]!;
                RemoveSpawnpointByPos(spawnpointsArray, posValue);
                File.WriteAllText(filepath, jsonData.ToString());
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred while removing a spawn point: {ex.Message}");
            }
        }

        static void RemoveSpawnpointByPos(JArray spawnpointsArray, string posToRemove)
        {
            for (int i = spawnpointsArray.Count - 1; i >= 0; i--)
            {
                JObject spawnpoint = (JObject)spawnpointsArray[i];
                if (spawnpoint["pos"] != null && spawnpoint["pos"]!.ToString() == posToRemove)
                {
                    spawnpointsArray.RemoveAt(i);
                }
            }
        }
        public string GetNearestSpawnPoint(Vector? playerPos)
        {
            if (playerPos == null)
                return "Spawn point cannot be deleted! Your Position is not valid!";

            double lowestDistance = float.MaxValue;
            Vector? nearestSpawn = null;
            foreach (var ctSpawn in spawnPositionsCT.Keys)
            {
                double distance = GetDistance(playerPos, ctSpawn);
                if (distance < lowestDistance)
                {
                    lowestDistance = distance;
                    nearestSpawn = ctSpawn;
                }
            }
            foreach (var tSpawn in spawnPositionsT.Keys)
            {
                double distance = GetDistance(playerPos, tSpawn);
                if (distance < lowestDistance)
                {
                    lowestDistance = distance;
                    nearestSpawn = tSpawn;
                }
            }

            bool isDeleted = false;
            if (nearestSpawn != null)
                isDeleted = RemoveSpawnPoint(ModuleDirectory + $"/spawns/{Server.MapName}.json", $"{nearestSpawn}");

            if (isDeleted)
            {
                LoadMapSpawns(ModuleDirectory + $"/spawns/{Server.MapName}.json", false);
                RemoveBeams();
                ShowAllSpawnPoints();
                return $"The nearest Spawn point has been successfully deleted! {nearestSpawn}";
            }
            else
            {
                return "Spawn point cannot be deleted! (No spawn found)";
            }
        }
        public void ShowAllSpawnPoints()
        {
            foreach (var ctTeam in spawnPositionsCT.Keys)
            {
                CBeam beam = Utilities.CreateEntityByName<CBeam>("beam")!;
                if (beam == null)
                {
                    SendConsoleMessage($"[Deathmatch] Failed to create beam for CT", ConsoleColor.DarkYellow);
                    return;
                }

                var position = ctTeam;
                beam.Render = Color.Blue;
                beam.Width = 5.5f;
                position[2] += 50.00f;
                beam.Teleport(position, new QAngle(0, 0, 0), new Vector(0, 0, 0));
                position[2] -= 50.00f;
                beam.EndPos.X = position[0];
                beam.EndPos.Y = position[1];
                beam.EndPos.Z = position[2];

                beam.DispatchSpawn();
            }
            foreach (var tTeam in spawnPositionsT.Keys)
            {
                CBeam beam = Utilities.CreateEntityByName<CBeam>("beam")!;
                if (beam == null)
                {
                    SendConsoleMessage($"[Deathmatch] Failed to create beam for T", ConsoleColor.DarkYellow);
                    return;
                }
                var position = tTeam;
                beam.Render = Color.Orange;
                beam.Width = 5.5f;
                position[2] += 50.00f;
                beam.Teleport(position, new QAngle(0, 0, 0), new Vector(0, 0, 0));
                position[2] -= 50.00f;
                beam.EndPos.X = position[0];
                beam.EndPos.Y = position[1];
                beam.EndPos.Z = position[2];

                beam.DispatchSpawn();
            }
        }
        public static void RemoveMapDefaulSpawns()
        {
            if (!DefaultMapSpawnDisabled)
            {
                if (IsCasualGamemode)
                {
                    int iDefaultCTSpawns = 0;
                    int iDefaultTSpawns = 0;
                    var ctSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerTerrorist>("info_player_counterterrorist");
                    foreach (var entity in ctSpawns)
                    {
                        if (entity.IsValid)
                        {
                            entity.AcceptInput("SetDisabled");
                            iDefaultCTSpawns++;
                        }
                    }
                    var tSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerTerrorist>("info_player_terrorist");
                    foreach (var entity in tSpawns)
                    {
                        if (entity.IsValid)
                        {
                            entity.AcceptInput("SetDisabled");
                            iDefaultTSpawns++;
                        }
                    }
                    SendConsoleMessage($"[Deathmatch] Total {iDefaultTSpawns} T and {iDefaultCTSpawns} CT default Spawns disabled!", ConsoleColor.Green);
                }
                else
                {
                    int DMSpawns = 0;
                    var dmSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoDeathmatchSpawn>("info_deathmatch_spawn");
                    foreach (var entity in dmSpawns)
                    {
                        if (entity.IsValid)
                        {
                            entity.AcceptInput("SetDisabled");
                            DMSpawns++;
                        }
                    }
                    SendConsoleMessage($"[Deathmatch] Total {DMSpawns} default Spawns disabled!", ConsoleColor.Green);

                }
                DefaultMapSpawnDisabled = true;
                CreateCustomMapSpawns();
            }
        }
        public static void CreateCustomMapSpawns()
        {
            string infoPlayerCT = IsCasualGamemode ? "info_player_counterterrorist" : "info_deathmatch_spawn";
            string infoPlayerT = IsCasualGamemode ? "info_player_terrorist" : "info_deathmatch_spawn";

            foreach (var spawn in spawnPositionsCT)
            {
                var entity = Utilities.CreateEntityByName<SpawnPoint>(infoPlayerCT);
                if (entity == null)
                {
                    SendConsoleMessage($"[Deathmatch] Failed to create spawn point for CT", ConsoleColor.DarkYellow);
                    continue;
                }
                entity.Teleport(spawn.Key, spawn.Value, new Vector(0, 0, 0));
                entity.DispatchSpawn();
            }

            foreach (var spawn in spawnPositionsT)
            {
                var entity = Utilities.CreateEntityByName<SpawnPoint>(infoPlayerT);
                if (entity == null)
                {
                    SendConsoleMessage($"[Deathmatch] Failed to create spawn point for T", ConsoleColor.DarkYellow);
                    continue;
                }
                entity.Teleport(spawn.Key, spawn.Value, new Vector(0, 0, 0));
                entity.DispatchSpawn();
            }
        }

        public void LoadMapSpawns(string filePath, bool mapstart)
        {
            spawnPositionsCT.Clear();
            spawnPositionsT.Clear();
            if (Config.Gameplay.DefaultSpawns)
            {
                addDefaultSpawnsToList();
            }
            else
            {
                if (!File.Exists(filePath))
                {
                    SendConsoleMessage($"[Deathmatch] No spawn points found for this map! (Deathmatch/spawns/{Server.MapName}.json)", ConsoleColor.Red);
                    addDefaultSpawnsToList();
                }
                else
                {
                    SendConsoleMessage($"[Deathmatch] Loading Custom Map Spawns..", ConsoleColor.DarkYellow);

                    var jsonContent = File.ReadAllText(filePath);
                    JObject jsonData = JsonConvert.DeserializeObject<JObject>(jsonContent)!;

                    foreach (var teamData in jsonData["spawnpoints"]!)
                    {
                        string teamType = teamData["team"]!.ToString();
                        string pos = teamData["pos"]!.ToString();
                        string angle = teamData["angle"]!.ToString();

                        if (teamType == "ct")
                        {
                            spawnPositionsCT.Add(ParseVector(pos), ParseQAngle(angle));
                        }
                        else if (teamType == "t")
                        {
                            spawnPositionsT.Add(ParseVector(pos), ParseQAngle(angle));
                        }
                    }

                    SendConsoleMessage($"[Deathmatch] Total Loaded Custom Spawns: CT {spawnPositionsCT.Count} | T {spawnPositionsT.Count}", ConsoleColor.Green);
                    if (mapstart)
                        RemoveMapDefaulSpawns();
                }
            }
        }
        private static Vector ParseVector(string pos)
        {
            pos = pos.Replace(",", "");
            var values = pos.Split(' ');
            if (values.Length == 3 &&
                float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return new Vector(x, y, z);
            }
            return new Vector(0, 0, 0);
        }

        private static QAngle ParseQAngle(string angle)
        {
            angle = angle.Replace(",", "");
            var values = angle.Split(' ');
            if (values.Length == 3 &&
                float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return new QAngle(x, y, z);
            }

            return new QAngle(0, 0, 0);
        }

        private static double GetDistance(Vector v1, Vector v2)
        {
            double X = v1.X - v2.X;
            double Y = v1.Y - v2.Y;

            return Math.Sqrt(X * X + Y * Y);
        }

        public void addDefaultSpawnsToList()
        {
            if (IsCasualGamemode)
            {
                SendConsoleMessage($"[Deathmatch] Loading Default Map Spawns..", ConsoleColor.DarkYellow);
                foreach (var spawn in Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist"))
                {
                    if (spawn == null || spawn.AbsOrigin == null || spawn.AbsRotation == null)
                        continue;

                    spawnPositionsCT.Add(spawn.AbsOrigin, spawn.AbsRotation);
                }
                foreach (var spawn in Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist"))
                {
                    if (spawn == null || spawn.AbsOrigin == null || spawn.AbsRotation == null)
                        continue;
                    spawnPositionsT.Add(spawn.AbsOrigin, spawn.AbsRotation);
                }
            }
            else
            {
                SendConsoleMessage($"[Deathmatch] Loading Default Deathmatch Map Spawns..", ConsoleColor.DarkYellow);
                int randomizer = 0;
                foreach (var spawn in Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn"))
                {
                    randomizer++;
                    if (randomizer % 2 == 0)
                    {
                        if (spawn == null || spawn.AbsOrigin == null || spawn.AbsRotation == null)
                            continue;
                        spawnPositionsT.Add(spawn.AbsOrigin, spawn.AbsRotation);
                    }
                    else
                    {
                        if (spawn == null || spawn.AbsOrigin == null || spawn.AbsRotation == null)
                            continue;
                        spawnPositionsCT.Add(spawn.AbsOrigin, spawn.AbsRotation);
                    }
                }
            }
            SendConsoleMessage($"[Deathmatch] Total Loaded Spawns: CT {spawnPositionsCT.Count} | T {spawnPositionsT.Count}", ConsoleColor.Green);
        }
    }
}