using SoulsFormats;
using SoulsFormats.KF4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static SoulsFormats.MSB.Shape.Composite;
using static SoulsFormats.NVM;

namespace MapBuddy.Action
{
    internal class EntityID
    {
        Logger logger = new Logger();
        Util util = new Util();

        Dictionary<string, string> map_dict = new Dictionary<string, string>();

        DCX.Type compressionType = DCX.Type.DCX_DFLT_10000_44_9;

        bool incompleteChange;

        public EntityID(string map_selection, string path, bool isAssetChange, bool isEnemyChange, bool isPlayerChange, bool overrideExisting, ulong range_start_id, ulong range_end_id)
        {
            map_dict = util.GetMapSelection(map_selection, path, logger);

            // First pass: collect existing IDs from all MSBs into a global set.
            var globalUsedIds = new HashSet<ulong>();
            foreach (KeyValuePair<string, string> entry in map_dict)
            {
                string map_path = entry.Value;
                MSBE msb = MSBE.Read(map_path);

                AddExistingIdsToSet(msb, globalUsedIds);

                // Release msb as we don't need to keep them in memory for the collection pass
                msb = null;
            }

            // Second pass: process each MSB one at a time, update globalUsedIds as IDs are assigned
            foreach (KeyValuePair<string, string> entry in map_dict)
            {
                string map_name = entry.Key;
                string map_path = entry.Value;

                logger.AddToDebugLog($"Editing {map_name}.");

                string[] map_indexes = map_name.Replace("m", "").Split("_");
                string entity_prefix = map_indexes[0] + map_indexes[1];

                MSBE msb = MSBE.Read(map_path);

                incompleteChange = false;
                msb = AddUniqueEntityID(msb, map_name, entity_prefix, isAssetChange, isEnemyChange, isPlayerChange, overrideExisting, range_start_id, range_end_id, globalUsedIds);

                msb.Write(map_path, compressionType);

                if (incompleteChange)
                {
                    MessageBox.Show($"Applied changes for {map_name} were incomplete, as entity ID range was insufficient to cover all entities.", "Information", MessageBoxButtons.OK);
                }

                logger.AddToDebugLog($"Finished editing {map_name}.");
                logger.WriteLog();

                // Release msb to free memory before processing the next file
                msb = null;
            }

            MessageBox.Show("Applied unique Entity ID to specified type.", "Information", MessageBoxButtons.OK);
        }

        // Added parameter globalUsedIds so assigned IDs are unique across all processed MSBs
        // Added parameter useSavedFlags to exclude IDs whose last 4 digits fall within disallowed ranges
        public MSBE AddUniqueEntityID(MSBE msb, string map, string entity_id_prefix, bool isAssetChange, bool isEnemyChange, bool isPlayerChange, bool overrideExisting, ulong range_start_id, ulong range_end_id, HashSet<ulong> globalUsedIds)
        {
            // Get count for Enumerable
            ulong range_diff = range_end_id - range_start_id;
            logger.AddToDebugLog($"Start: {range_start_id}, End: {range_end_id}");
            // Middle strings should be empty by default
            string start_middle_str = "";
            string end_middle_str = "";

            // Adjust start middle string if input is below 1000
            if (range_start_id <= 999 && range_start_id >= 100)
            {
                start_middle_str = "0";
            }
            else if (range_start_id <= 99 && range_start_id >= 10)
            {
                start_middle_str = "00";
            }
            else if (range_start_id <= 9 && range_start_id >= 0)
            {
                start_middle_str = "000";
            }

            // Adjust end middle string if input is below 1000
            if (range_end_id <= 999 && range_end_id >= 100)
            {
                end_middle_str = "0";
            }
            else if (range_end_id <= 99 && range_end_id >= 10)
            {
                end_middle_str = "00";
            }
            else if (range_end_id <= 9 && range_end_id >= 0)
            {
                end_middle_str = "000";
            }

            string start_id_str = $"{entity_id_prefix}{start_middle_str}{range_start_id}";
            string end_id_str = $"{entity_id_prefix}{end_middle_str}{range_end_id}";
            ulong start_id = Convert.ToUInt64(start_id_str);
            ulong end_id = Convert.ToUInt64(end_id_str);
            logger.AddToDebugLog($"FinalStart: {start_id_str}, FinalEnd: {start_id_str}, Prefix: {entity_id_prefix}, Start_Mid: {start_middle_str}, End_Mid: {end_middle_str}");
            // Single-pass allocator state
            ulong nextCandidate = start_id;
            bool exhausted = false;

            // Local function that returns next valid id or null if none
            ulong? GetNextValidId()
            {
                while (nextCandidate <= end_id)
                {
                    ulong candidate = nextCandidate;
                    nextCandidate++;

                    if (globalUsedIds.Contains(candidate))
                        continue;

                    return candidate;
                }

                exhausted = true;
                return null;
            }

            // Apply entity ID change and update globalUsedIds as we assign
            if (isEnemyChange)
            {
                foreach (MSBE.Part.Enemy entity in msb.Parts.Enemies)
                {
                    if (entity.EntityID == 0 || overrideExisting == true)
                    {
                        ulong? id = GetNextValidId();
                        if (id.HasValue)
                        {
                            entity.EntityID = (uint)Convert.ToUInt64(id.Value); // Convert to UInt to fit MSB type
                            globalUsedIds.Add(id.Value);

                            logger.AddToLog($"Added {entity.EntityID} to {entity.Name}.");
                            logger.AddToIdLog($"{entity.EntityID}");
                            logger.AddToCsvLog(entity.Name, id.Value, map);
                        }
                        else
                        {
                            incompleteChange = true;
                            logger.AddToLog($"No valid Entity ID available to assign to {entity.Name} with set range.");
                        }
                    }
                }
            }
            if (isAssetChange)
            {
                foreach (MSBE.Part.Asset entity in msb.Parts.Assets)
                {
                    if (entity.EntityID == 0 || overrideExisting == true)
                    {
                        ulong? id = GetNextValidId();
                        if (id.HasValue)
                        {
                            entity.EntityID = (uint)Convert.ToUInt64(id.Value);
                            globalUsedIds.Add(id.Value);

                            logger.AddToLog($"Added {entity.EntityID} to {entity.Name}.");
                            logger.AddToIdLog($"{entity.EntityID}");
                            logger.AddToCsvLog(entity.Name, id.Value, map);
                        }
                        else
                        {
                            incompleteChange = true;
                            logger.AddToLog($"No valid Entity ID available to assign to {entity.Name} with set range.");
                        }
                    }
                }
            }
            if (isPlayerChange)
            {
                foreach (MSBE.Part.Player entity in msb.Parts.Players)
                {
                    if (entity.EntityID == 0 || overrideExisting == true)
                    {
                        ulong? id = GetNextValidId();
                        if (id.HasValue)
                        {
                            entity.EntityID = (uint)Convert.ToUInt64(id.Value);
                            globalUsedIds.Add(id.Value);

                            logger.AddToLog($"Added {entity.EntityID} to {entity.Name}.");
                            logger.AddToIdLog($"{entity.EntityID}");
                            logger.AddToCsvLog(entity.Name, id.Value, map);
                        }
                        else
                        {
                            incompleteChange = true;
                            logger.AddToLog($"No valid Entity ID available to assign to {entity.Name} with set range.");
                        }
                    }
                }
            }

            return msb;
        }

        private void AddExistingIdsToSet(MSBE msb, HashSet<ulong> set)
        {
            // Parts
            foreach (MSBE.Part.Enemy entity in msb.Parts.Enemies)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.Asset entity in msb.Parts.Assets)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.Player entity in msb.Parts.Players)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.Collision entity in msb.Parts.Collisions)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.ConnectCollision entity in msb.Parts.ConnectCollisions)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.MapPiece entity in msb.Parts.MapPieces)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.DummyAsset entity in msb.Parts.DummyAssets)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Part.DummyEnemy entity in msb.Parts.DummyEnemies)
                set.Add(Convert.ToUInt64(entity.EntityID));

            // Events
            foreach (MSBE.Event.Generator entity in msb.Events.Generators)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.Mount entity in msb.Events.Mounts)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.Navmesh entity in msb.Events.Navmeshes)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.ObjAct entity in msb.Events.ObjActs)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.Other entity in msb.Events.Others)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.PatrolInfo entity in msb.Events.PatrolInfo)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.PlatoonInfo entity in msb.Events.PlatoonInfo)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.PseudoMultiplayer entity in msb.Events.PseudoMultiplayers)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.RetryPoint entity in msb.Events.RetryPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.SignPool entity in msb.Events.SignPools)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Event.Treasure entity in msb.Events.Treasures)
                set.Add(Convert.ToUInt64(entity.EntityID));

            // Regions
            foreach (MSBE.Region.AutoDrawGroupPoint entity in msb.Regions.AutoDrawGroupPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.BuddySummonPoint entity in msb.Regions.BuddySummonPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.Connection entity in msb.Regions.Connections)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.Dummy entity in msb.Regions.Dummies)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.EnvironmentMapEffectBox entity in msb.Regions.EnvironmentMapEffectBoxes)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.EnvironmentMapOutput entity in msb.Regions.EnvironmentMapOutputs)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.EnvironmentMapPoint entity in msb.Regions.EnvironmentMapPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.FallPreventionRemoval entity in msb.Regions.FallPreventionRemovals)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.FastTravelRestriction entity in msb.Regions.FastTravelRestriction)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.GroupDefeatReward entity in msb.Regions.GroupDefeatRewards)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.Hitset entity in msb.Regions.Hitsets)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.HorseRideOverride entity in msb.Regions.HorseRideOverrides)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.InvasionPoint entity in msb.Regions.InvasionPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MapNameOverride entity in msb.Regions.MapNameOverrides)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MapPoint entity in msb.Regions.MapPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MapPointDiscoveryOverride entity in msb.Regions.MapPointDiscoveryOverrides)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MapPointParticipationOverride entity in msb.Regions.MapPointParticipationOverrides)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.Message entity in msb.Regions.Messages)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MountJump entity in msb.Regions.MountJumps)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MountJumpFall entity in msb.Regions.MountJumpFalls)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MufflingBox entity in msb.Regions.MufflingBoxes)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MufflingPlane entity in msb.Regions.MufflingPlanes)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.MufflingPortal entity in msb.Regions.MufflingPortals)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.NavmeshCutting entity in msb.Regions.NavmeshCuttings)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.Other entity in msb.Regions.Others)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.PatrolRoute entity in msb.Regions.PatrolRoutes)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.PatrolRoute22 entity in msb.Regions.PatrolRoute22s)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.PlayArea entity in msb.Regions.PlayAreas)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.SFX entity in msb.Regions.SFX)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.Sound entity in msb.Regions.Sounds)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.SoundRegion entity in msb.Regions.SoundRegions)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.SpawnPoint entity in msb.Regions.SpawnPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.WeatherCreateAssetPoint entity in msb.Regions.WeatherCreateAssetPoints)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.WeatherOverride entity in msb.Regions.WeatherOverrides)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.WindArea entity in msb.Regions.WindAreas)
                set.Add(Convert.ToUInt64(entity.EntityID));
            foreach (MSBE.Region.WindSFX entity in msb.Regions.WindSFX)
                set.Add(Convert.ToUInt64(entity.EntityID));
        }
    }
}
