using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Story", "TeamDMA", "0.0.1")]
    [Description("Creates stories for players to go on to earn rewards")]
    public class Story : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin HumanNPC;
        [PluginReference] Plugin ServerRewards;
        [PluginReference] Plugin BetterChat;
        [PluginReference] Plugin Grid;

        ConfigData configData;

        StoryData storyData;
        PlayerData playerData;
        NPCData vendors;
        ItemNames itemNames;
        private DynamicConfigFile Story_Data;
        private DynamicConfigFile Player_Data;
        private DynamicConfigFile Story_Vendors;
        private DynamicConfigFile Item_Names;

        private Dictionary<ulong, PlayerStoryData> PlayerProgress;
        private Dictionary<StoryType, Dictionary<string, StoryEntry>> StoryDict;

        private Dictionary<string, ItemDefinition> ItemDefs;
        private Dictionary<string, string> DisplayNames = new Dictionary<string, string>();

        private Dictionary<ulong, StoryCreator> ActiveCreations = new Dictionary<ulong, StoryCreator>();
        private Dictionary<ulong, StoryCreator> ActiveEditors = new Dictionary<ulong, StoryCreator>();

        private Dictionary<StoryType, List<string>> AllObjectives = new Dictionary<StoryType, List<string>>();

        private Dictionary<uint, Dictionary<ulong, int>> HeliAttackers = new Dictionary<uint, Dictionary<ulong, int>>();

        private Dictionary<ulong, List<string>> OpenUI = new Dictionary<ulong, List<string>>();
        private Dictionary<uint, ulong> Looters = new Dictionary<uint, ulong>();

        private List<ulong> StatsMenu = new List<ulong>();
        private List<ulong> OpenMenuBind = new List<ulong>();

        static string UIMain = "UIMain";
        static string UIPanel = "UIPanel";
        static string UIEntry = "UIEntry";

        private string textPrimary;
        private string textSecondary;
        #endregion

        #region Classes
        class PlayerStoryData
        {
            public Dictionary<string, PlayerStoryInfo> Stories = new Dictionary<string, PlayerStoryInfo>();
            public List<StoryInfo> RequiredItems = new List<StoryInfo>();
            public ActiveStory CurrentStory = new ActiveStory();
            public List<string> DoneStories = new List<string>();
        }
        class PlayerStoryInfo
        {
            public StoryStatus Status;
            public StoryType Type;
            public int AmountCollected = 0;
            public bool RewardClaimed = false;
            public double ResetTime = 0;
            public string VendorID = null;
            public string TargetID = null;
        }
        class StoryEntry
        {
            public string StoryName;
            public string Description;
            public string Objective;
            public string ObjectiveName;
            public int AmountRequired;
            public bool ItemDeduction;
            public string RequiredQuest = null;
            public int StoryLine = -1;
            public int StoryNumber = -1;
            public NPCInfo Vendor;
            public NPCInfo Target;
            public List<RewardItem> Rewards;
        }
        class NPCInfo
        {
            public float x;
            public float z;
            public string ID;
            public string Name;
        }
        class ActiveStory
        {
            public string VendorID;
            public string TargetID;
        }
        class StoryInfo
        {
            public string ShortName;
            public StoryType Type;
        }
        class RewardItem
        {
            public bool isRP = false;
            public string DisplayName;
            public string ShortName;
            public int ID;
            public float Amount;
            public bool BP;
            public ulong Skin;
        }
        class StoryCreator
        {
            public StoryType type;
            public StoryEntry entry;
            public StoryInfo storyInfo;
            public RewardItem item;
            public string oldEntry;
            public int partNum;
        }
        class ItemNames
        {
            public Dictionary<string, string> DisplayNames = new Dictionary<string, string>();
        }
        enum StoryType
        {
            Kill,
            Craft,
            Gather,
            Loot,
            Walk
        }
        enum StoryStatus
        {
            Pending,
            Completed,
            Open
        }
        #endregion

        #region UI Creation
        class QUI
        {
            public static bool disableFade;
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                if (disableFade)
                    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                if (disableFade)
                    fadein = 0;
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
            static public void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            static public void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                if (disableFade)
                    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            Story_Data = Interface.Oxide.DataFileSystem.GetFile("Story/story_data");
            Player_Data = Interface.Oxide.DataFileSystem.GetFile("Story/story_players");
            Story_Vendors = Interface.Oxide.DataFileSystem.GetFile("Story/story_vendors");
            Item_Names = Interface.Oxide.DataFileSystem.GetFile("Story/story_itemnames");
            lang.RegisterMessages(Localization, this);
        }
        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();

            QUI.disableFade = configData.DisableUI_FadeIn;
            textPrimary = $"<color={configData.Colors.TextColor_Primary}>";
            textSecondary = $"<color={configData.Colors.TextColor_Secondary}>";

            ItemDefs = ItemManager.itemList.ToDictionary(i => i.shortname);
            FillObjectiveList();
            timer.Once(900, () => SaveLoop());
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
            SavePlayerData();
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (configData.KeybindOptions.Autoset_KeyBind)
            {
                if (!string.IsNullOrEmpty(configData.KeybindOptions.KeyBind_Key))
                {
                    player.Command("bind " + configData.KeybindOptions.KeyBind_Key + " QUI_OpenQuestMenu");
                }
            }
        }

        #region Objective Hooks
        //Kill
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == null || info == null) return;
                string entname = entity?.ShortPrefabName;
                if (entname == "testridablehorse")
                {
                    entname = "horse";
                }
                BasePlayer player = null;

                if (info.InitiatorPlayer != null)
                    player = info.InitiatorPlayer;
                else if (entity.GetComponent<BaseHelicopter>() != null)
                    player = BasePlayer.FindByID(GetLastAttacker(entity.net.ID));

                if (player != null)
                {
                    if (entity.ToPlayer() != null && entity.ToPlayer() == player) return;
                    if (hasStories(player.userID) && isStoryItem(player.userID, entname, StoryType.Kill))
                        ProcessProgress(player, StoryType.Kill, entname);
                }
            }
            catch (Exception ex)
            {
            }
        }
        void OnEntityTakeDamage(BaseCombatEntity victim, HitInfo info)
        {
            if (victim.GetComponent<BaseHelicopter>() != null && info?.Initiator?.ToPlayer() != null)
            {
                var heli = victim.GetComponent<BaseHelicopter>();
                var player = info.Initiator.ToPlayer();
                NextTick(() =>
                {
                    if (heli == null) return;
                    if (!HeliAttackers.ContainsKey(heli.net.ID))
                        HeliAttackers.Add(heli.net.ID, new Dictionary<ulong, int>());
                    if (!HeliAttackers[heli.net.ID].ContainsKey(player.userID))
                        HeliAttackers[heli.net.ID].Add(player.userID, 0);
                    HeliAttackers[heli.net.ID][player.userID]++;
                });
            }
        }
        // Gather
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity?.ToPlayer();
            if (player != null)
                if (hasStories(player.userID) && isStoryItem(player.userID, item.info.shortname, StoryType.Gather))
                    ProcessProgress(player, StoryType.Gather, item.info.shortname, item.amount);
        }
        void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item) => OnDispenserGather(dispenser, entity, item);

        void OnGrowableGather(GrowableEntity growable, Item item, BasePlayer player)
        {
            if (player != null)
                if (hasStories(player.userID) && isStoryItem(player.userID, item.info.shortname, StoryType.Gather))
                    ProcessProgress(player, StoryType.Gather, item.info.shortname, item.amount);
        }
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (player != null)
                if (hasStories(player.userID) && isStoryItem(player.userID, item.info.shortname, StoryType.Gather))
                    ProcessProgress(player, StoryType.Gather, item.info.shortname, item.amount);
        }
        //Craft
        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            var player = task.owner;
            if (player != null)
                if (hasStories(player.userID) && isStoryItem(player.userID, item.info.shortname, StoryType.Craft))
                    ProcessProgress(player, StoryType.Craft, item.info.shortname, item.amount);
        }
        //Loot
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (Looters.ContainsKey(item.uid))
            {
                if (container.playerOwner != null)
                {
                    if (Looters[item.uid] != container.playerOwner.userID)
                    {
                        if (hasStories(container.playerOwner.userID) && isStoryItem(container.playerOwner.userID, item.info.shortname, StoryType.Loot))
                        {
                            ProcessProgress(container.playerOwner, StoryType.Loot, item.info.shortname, item.amount);
                            Looters.Remove(item.uid);
                        }
                    }
                }
            }
            else if (container.playerOwner != null) Looters.Add(item.uid, container.playerOwner.userID);
        }
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            ulong id = 0U;
            if (container.entityOwner != null)
                id = container.entityOwner.OwnerID;
            else if (container.playerOwner != null)
                id = container.playerOwner.userID;

            if (!Looters.ContainsKey(item.uid))
                Looters.Add(item.uid, id);
        }
        // Delivery and Vendors
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (player == null || npc == null) return;
            CheckPlayerEntry(player);
            var npcID = npc.UserIDString;

            if (vendors.StoryVendors.ContainsKey(npcID))
            {
                if (hasStories(player.userID) && PlayerProgress[player.userID].CurrentStory.TargetID == npc.UserIDString)
                {
                    AcceptStory(player, npcID, 1);
                    return;
                }
                if (hasStories(player.userID) && string.IsNullOrEmpty(PlayerProgress[player.userID].CurrentStory.TargetID))
                {
                    AcceptStory(player, npcID);       
                    return;
                }
                else SendMSG(player, LA("storyInprog", player.UserIDString), LA("Story", player.UserIDString));
            }
        }
        #endregion

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChat) return null;

            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return null;

            if (ActiveEditors.ContainsKey(player.userID) || ActiveCreations.ContainsKey(player.userID))
            {
                StoryChat(player, arg.Args);

                return false;
            }
            return null;
        }
        object OnBetterChat(Dictionary<string, object> dict)
        {
            var player = (dict["Player"] as IPlayer).Object as BasePlayer;
            if (player == null) return null;
            string message = dict["Message"].ToString();
            if (ActiveEditors.ContainsKey(player.userID) || ActiveCreations.ContainsKey(player.userID))
            {
                StoryChat(player, message.Split(' '));
                return false;
            }
            return dict;
        }
        void StoryChat(BasePlayer player, string[] arg)
        {
            bool isEditing = false;
            bool isCreating = false;
            StoryCreator Creator = new StoryCreator();
            StoryEntry StoryEnt = new StoryEntry();

            if (ActiveEditors.ContainsKey(player.userID))
            {
                isEditing = true;
                Creator = ActiveEditors[player.userID];
                StoryEnt = Creator.entry;
            }
            else if (ActiveCreations.ContainsKey(player.userID))
            {
                isCreating = true;
                Creator = ActiveCreations[player.userID];
                StoryEnt = Creator.entry;
            }

            if (!isEditing && !isCreating)
                return;

            var args = string.Join(" ", arg);
            if (args.Contains("exit"))
            {
                ExitStory(player, isCreating);
                return;
            }

            if (args.Contains("story item"))
            {
                var item = GetItem(player);
                if (item != null)
                {
                    StoryEnt.Rewards.Add(item);
                    Creator.partNum++;
                    if (isCreating)
                        StoryHelp(player, 7);
                    else if (isEditing)
                    {
                        SaveRewardsEdit(player);
                        StoryHelp(player, 10);
                    }
                }
                else SendMSG(player, $"{LA("noAItem", player.UserIDString)}'story item'", LA("QC", player.UserIDString));

                return;
            }

            switch (Creator.partNum)
            {
                case 0:
                    foreach (var type in storyData.StoryDict)
                    {
                        if (type.Value.ContainsKey(args))
                        {
                            SendMSG(player, LA("nameExists", player.UserIDString), LA("QC", player.UserIDString));
                            return;
                        }
                    }
                    StoryEnt.StoryName = args;
                    SendMSG(player, args, "Name:");
                    Creator.partNum++;
                    if (isCreating)
                        if (Creator.type == StoryType.Walk)
                        {
                            Creator.entry.Objective = "walk";
                            Creator.entry.ObjectiveName = "Walk";
                            Creator.partNum = 3;
                            StoryHelp(player, 3);
                        }
                        else
                            StoryHelp(player, 1);
                    else StoryHelp(player, 6);
                    return;
                case 2:
                    {
                        int amount;
                        if (!int.TryParse(arg[0], out amount))
                        {
                            SendMSG(player, LA("objAmount", player.UserIDString), LA("QC", player.UserIDString));
                            return;
                        }
                        StoryEnt.AmountRequired = amount;
                        SendMSG(player, args, LA("OA", player.UserIDString));
                        Creator.partNum++;
                        if (isCreating)
                            StoryHelp(player, 3);
                        else StoryHelp(player, 6);
                    }
                    return;
                case 3:
                    {
                        StoryEnt.Description = args;
                        SendMSG(player, args, LA("Desc", player.UserIDString));
                        Creator.partNum++;
                        if (isCreating)
                            StoryHelp(player, 4);
                        else StoryHelp(player, 6);
                    }
                    return;
                case 5:
                    { 
                        int amount;
                        if (!int.TryParse(arg[0], out amount))
                        {
                            SendMSG(player, LA("noRA", player.UserIDString), LA("QC", player.UserIDString));
                            return;
                        }
                        Creator.item.Amount = amount;
                        StoryEnt.Rewards.Add(Creator.item);
                        Creator.item = new RewardItem();
                        SendMSG(player, args, LA("RA", player.UserIDString));
                        Creator.partNum++;
                        if (isCreating)
                            StoryHelp(player, 7);
                        else if (isEditing)
                        {
                            SaveRewardsEdit(player);
                        }
                        return;
                    }
                case 13:
                    {
                        int number;
                        if (!int.TryParse(arg[0], out number))
                        {
                            SendMSG(player, LA("slNumber", player.UserIDString), LA("QC", player.UserIDString));
                            return;
                        }
                        StoryEnt.StoryLine = number;
                        SendMSG(player, args, LA("SL", player.UserIDString));
                        Creator.partNum++;
                        if (isCreating)
                            StoryHelp(player, 14);
                        else StoryHelp(player, 6);
                    }
                    return;
                case 14:
                    {
                        int number;
                        if (!int.TryParse(arg[0], out number))
                        {
                            SendMSG(player, LA("sNumber", player.UserIDString), LA("QC", player.UserIDString));
                            return;
                        }
                        foreach (var entries in storyData.StoryDict.Values)
                        {
                            foreach (var sNumber in entries)
                            {
                                if (sNumber.Value.StoryNumber == number)
                                {
                                    SendMSG(player, LA("numberExists", player.UserIDString), LA("QC", player.UserIDString));
                                    return;
                                }
                            }
                        }
                        StoryEnt.StoryNumber = number;
                        SendMSG(player, args, LA("SN", player.UserIDString));
                        Creator.partNum++;
                        if (isCreating)
                            StoryHelp(player, 15);
                        else StoryHelp(player, 6);
                    }
                    return;
                default:
                    break;
            }
        }
        #endregion

        #region External Calls

        //TODO

        #endregion

        #region Objective Lists
        private void FillObjectiveList()
        {
            AllObjectives.Add(StoryType.Loot, new List<string>());
            AllObjectives.Add(StoryType.Craft, new List<string>());
            AllObjectives.Add(StoryType.Kill, new List<string>());
            AllObjectives.Add(StoryType.Gather, new List<string>());
            AllObjectives.Add(StoryType.Walk, new List<string>());
            GetAllCraftables();
            GetAllItems();
            GetAllKillables();
            GetAllResources();
            //GetAllNPCs();
            foreach (var category in AllObjectives)
                category.Value.Sort();

            if (itemNames.DisplayNames == null || itemNames.DisplayNames.Count < 1)
            {
                foreach (var item in ItemDefs)
                {
                    if (!DisplayNames.ContainsKey(item.Key))
                        DisplayNames.Add(item.Key, item.Value.displayName.translated);
                }
                SaveDisplayNames();
            }
            else DisplayNames = itemNames.DisplayNames;
        }
        private void GetAllItems()
        {
            foreach (var item in ItemManager.itemList)
                AllObjectives[StoryType.Loot].Add(item.shortname);
        }
        private void GetAllCraftables()
        {
            foreach (var bp in ItemManager.bpList)
                if (bp.userCraftable)
                    AllObjectives[StoryType.Craft].Add(bp.targetItem.shortname);
        }
        private List<NPCInfo> GetAllNPCs()
        {
            var result = HumanNPC.Call("ListAllNPC");
            List<BasePlayer> listAllNPCs = (List<BasePlayer>)result;

            List<NPCInfo> AllNPCs = new List<NPCInfo>();

            if(listAllNPCs.Any() && listAllNPCs != null)
            {
                foreach (BasePlayer npcPlayer in listAllNPCs)
                {
                    if(npcPlayer != null)
                    {
                        NPCInfo npcInfo = new NPCInfo();
                        npcInfo.ID = npcPlayer.UserIDString;
                        npcInfo.Name = npcPlayer.displayName;
                        npcInfo.x = npcPlayer.transform.position.x;
                        npcInfo.z = npcPlayer.transform.position.y;
                        AllNPCs.Add(npcInfo);
                    }

                }
                return AllNPCs;
            }
            else
            {
                Puts("Couldn't load all npcs.");
                return null;
            }
        }
        private void GetAllResources()
        {
            AllObjectives[StoryType.Gather] = new List<string>
            {
                "wood",
                "stones",
                "metal.ore",
                "hq.metal.ore",
                "sulfur.ore",
                "cloth",
                "bone.fragments",
                "crude.oil",
                "fat.animal",
                "leather",
                "skull.wolf",
                "skull.human",
                "chicken.raw",
                "mushroom",
                "meat.boar",
                "bearmeat",
                "humanmeat.raw",
                "wolfmeat.raw"
            };
        }
        private void GetAllKillables()
        {
            AllObjectives[StoryType.Kill] = new List<string>
            {
                "bear",
                "boar",
                "bradleyapc",
                "chicken",
                "horse",
                "stag",
                "wolf",
                "autoturret_deployed",
                "patrolhelicopter",
                "player",
                "scientist",
                "murderer"
            };
            DisplayNames.Add("bear", "Bear");
            DisplayNames.Add("boar", "Boar");
            DisplayNames.Add("bradleyapc", "BradleyAPC");
            DisplayNames.Add("chicken", "Chicken");
            DisplayNames.Add("horse", "Horse");
            DisplayNames.Add("stag", "Stag");
            DisplayNames.Add("wolf", "Wolf");
            DisplayNames.Add("autoturret_deployed", "Auto-Turret");
            DisplayNames.Add("patrolhelicopter", "Helicopter");
            DisplayNames.Add("player", "Player");
            DisplayNames.Add("scientist", "Scientist");
            DisplayNames.Add("murderer", "Murderer");
        }
        #endregion

        #region Functions
        private void ProcessProgress(BasePlayer player, StoryType storyType, string type, int amount = 0)
        {
            if (string.IsNullOrEmpty(type)) return;
            var data = PlayerProgress[player.userID];
            if (data.RequiredItems.Count > 0)
            {
                foreach (var entry in data.Stories.Where(x => x.Value.Status == StoryStatus.Pending))
                {
                    var story = GetStory(entry.Key);
                    if (story != null)
                    {
                        if (type == story.Objective)
                        {
                            if (amount > 0)
                            {
                                var amountRequired = story.AmountRequired - entry.Value.AmountCollected;
                                if (amount > amountRequired)
                                    amount = amountRequired;
                                entry.Value.AmountCollected += amount;

                                if (story.ItemDeduction)
                                    TakeStoryItem(player, type, amount);
                            }
                            else entry.Value.AmountCollected++;

                            if (entry.Value.AmountCollected >= story.AmountRequired)
                                CompleteStory(player, entry.Key);
                            return;
                        }
                    }
                }
            }
        }
        private void TakeStoryItem(BasePlayer player, string item, int amount)
        {
            if (ItemDefs.ContainsKey(item))
            {
                var itemDef = ItemDefs[item];
                NextTick(() => player.inventory.Take(null, itemDef.itemid, amount));
            }
            else PrintWarning($"Unable to find definition for: {item}.");
        }
        private void CompleteStory(BasePlayer player, string storyName)
        {
            var data = PlayerProgress[player.userID].Stories[storyName];
            var items = PlayerProgress[player.userID].RequiredItems;
            var quest = GetStory(storyName);
            if (quest != null)
            {
                data.Status = StoryStatus.Completed;
                data.ResetTime = 1; // muss raus

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].ShortName == quest.Objective && items[i].Type == data.Type)
                    {
                        items.Remove(items[i]);
                        break;
                    }
                }
                SendMSG(player, "", $"{LA("qComple", player.UserIDString)} {storyName}. {LA("claRew", player.UserIDString)}");
            }
        }
        private ItemDefinition FindItemDefinition(string shortname)
        {
            ItemDefinition itemDefinition;
            return ItemDefs.TryGetValue(shortname, out itemDefinition) ? itemDefinition : null;
        }
        private string GetRewardString(List<RewardItem> entry)
        {
            var rewards = "";
            int i = 1;
            foreach (var item in entry)
            {
                rewards = rewards + $"{(int)item.Amount}x {item.DisplayName}";
                if (i < entry.Count)
                    rewards = rewards + ", ";
                i++;
            }
            return rewards;
        }
        private bool GiveReward(BasePlayer player, List<RewardItem> rewards)
        {
            foreach (var reward in rewards)
            {
                if (reward.isRP && ServerRewards)
                {
                    ServerRewards.Call("AddPoints", player.userID, (int)reward.Amount);
                }
                else
                {
                    if (string.IsNullOrEmpty(reward.ShortName)) return true;
                    var definition = FindItemDefinition(reward.ShortName);
                    if (definition != null)
                    {
                        var item = ItemManager.Create(definition, (int)reward.Amount, reward.Skin);
                        if (item != null)
                        {
                            player.inventory.GiveItem(item, player.inventory.containerMain);
                        }
                    }
                    else PrintWarning($"Quests: Error building item {reward.ShortName} for {player.displayName}");
                }
            }
            return true;
        }
        private void ReturnItems(BasePlayer player, string itemname, int amount)
        {
            if (amount > 0)
            {
                var definition = FindItemDefinition(itemname);
                if (definition != null)
                {
                    var item = ItemManager.Create(definition, amount);
                    if (item != null)
                    {
                        player.inventory.GiveItem(item);
                        PopupMessage(player, $"{LA("qCancel", player.UserIDString)} {item.amount}x {item.info.displayName.translated} {LA("rewRet", player.UserIDString)}");
                    }
                }
            }
        }
        private RewardItem GetItem(BasePlayer player)
        {
            Item item = player.GetActiveItem();
            if (item == null) return null;
            var newItem = new RewardItem
            {
                Amount = item.amount,
                DisplayName = DisplayNames[item.info.shortname],
                ID = item.info.itemid,
                ShortName = item.info.shortname,
                Skin = item.skin
            };
            return newItem;
        }
        private bool hasStories(ulong player)
        {
            try
            {
                if (PlayerProgress.ContainsKey(player))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                Puts($"Error checking stories for {player}");
                return false;
            }
        }
        private bool isStoryItem(ulong player, string name, StoryType type)
        {
            var data = PlayerProgress[player].RequiredItems;
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].ShortName == name && data[i].Type == type)
                    return true;
            }
            return false;
        }
        private void CheckPlayerEntry(BasePlayer player)
        {
            if (!PlayerProgress.ContainsKey(player.userID))
                PlayerProgress.Add(player.userID, new PlayerStoryData());
        }
        private object GetStoryType(string name)
        {
            foreach (var entry in StoryDict)
                if (entry.Value.ContainsKey(name))
                    return entry.Key;
            return null;
        }
        private StoryEntry GetStory(string name)
        {
            var type = GetStoryType(name);
            if (type != null)
            {
                foreach (var entry in storyData.StoryDict[(StoryType)type])
                {
                    if (entry.Key == name)
                        return entry.Value;
                }
            }
            PrintWarning($"Error retrieving story info for: {name}");
            return null;
        }
        private void SaveStory(BasePlayer player, bool isCreating)
        {
            StoryCreator Creator;
            StoryEntry StoryEnt;

            if (isCreating)
                Creator = ActiveCreations[player.userID];
            else Creator = ActiveEditors[player.userID];
            StoryEnt = Creator.entry;

            if (!vendors.StoryVendors.ContainsKey(StoryEnt.Target.ID))
            {
                vendors.StoryVendors.Add(StoryEnt.Target.ID, StoryEnt.Target);
            }
            if (!vendors.StoryVendors.ContainsKey(StoryEnt.Vendor.ID))
            {
                vendors.StoryVendors.Add(StoryEnt.Vendor.ID, StoryEnt.Vendor);
            }

            if (isCreating)
            {
                storyData.StoryDict[Creator.type].Add(StoryEnt.StoryName, StoryEnt);
                ActiveCreations.Remove(player.userID);
            }
            else
            {
                storyData.StoryDict[Creator.type].Remove(Creator.oldEntry);
                storyData.StoryDict[Creator.type].Add(StoryEnt.StoryName, StoryEnt);
                ActiveEditors.Remove(player.userID);
            }
            DestroyUI(player);
            SaveVendorData();
            SaveStoryData();
            SendMSG(player, $"{LA("saveQ", player.UserIDString)} {StoryEnt.StoryName}", LA("QC", player.UserIDString));
        }
        private void SaveRewardsEdit(BasePlayer player)
        {
            StoryCreator Creator = ActiveEditors[player.userID];
            StoryEntry StoryEnt = Creator.entry;
            storyData.StoryDict[Creator.type].Remove(Creator.entry.StoryName);
            storyData.StoryDict[Creator.type].Add(StoryEnt.StoryName, StoryEnt);

            DestroyUI(player);
            SaveStoryData();
            StoryHelp(player, 10);
            SendMSG(player, $"{LA("saveQ", player.UserIDString)} {StoryEnt.StoryName}", LA("QC", player.UserIDString));
        }
        private void ExitStory(BasePlayer player, bool isCreating)
        {
            if (isCreating)
                ActiveCreations.Remove(player.userID);
            else ActiveEditors.Remove(player.userID);

            SendMSG(player, LA("QCCancel", player.UserIDString), LA("QC", player.UserIDString));
            DestroyUI(player);
        }
        private void RemoveStory(string storyName)
        {
            var Story = GetStory(storyName);
            if (Story == null) return;
            var Type = (StoryType)GetStoryType(storyName);
            storyData.StoryDict[Type].Remove(storyName);

            foreach (var player in PlayerProgress)
            {
                if (player.Value.Stories.ContainsKey(storyName))
                    player.Value.Stories.Remove(storyName);
            }
            if (vendors.StoryVendors.ContainsKey(Story.Objective))
                vendors.StoryVendors.Remove(Story.Objective);

            SaveStoryData();
            SaveVendorData();
        }
        private ulong GetLastAttacker(uint id)
        {
            int hits = 0;
            ulong majorityPlayer = 0U;
            if (HeliAttackers.ContainsKey(id))
            {
                foreach (var score in HeliAttackers[id])
                {
                    if (score.Value > hits)
                        majorityPlayer = score.Key;
                }
            }
            return majorityPlayer;
        }
        private string GetTypeDescription(StoryType type)
        {
            switch (type)
            {
                case StoryType.Kill:
                    return LA("KillOBJ");
                case StoryType.Craft:
                    return LA("CraftOBJ");
                case StoryType.Gather:
                    return LA("GatherOBJ");
                case StoryType.Loot:
                    return LA("LootOBJ");
                case StoryType.Walk:
                    return LA("WalkOBJ");
            }
            return "";
        }
        private StoryType ConvertStringToType(string type)
        {
            switch (type)
            {
                case "gather":
                case "Gather":
                    return StoryType.Gather;
                case "loot":
                case "Loot":
                    return StoryType.Loot;
                case "craft":
                case "Craft":
                    return StoryType.Craft;
                case "walk":
                case "Walk":
                    return StoryType.Walk;
                default:
                    return StoryType.Kill;
            }
        }
        private string isNPCRegistered(string ID)
        {
            if (vendors.StoryVendors.ContainsKey(ID)) return LA("aSVReg");
            return null;
        }
        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private BasePlayer FindEntity(BasePlayer player)
        {
            var currentRot = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            var rayResult = Ray(player, currentRot);
            if (rayResult is BasePlayer)
            {
                var ent = rayResult as BasePlayer;
                return ent;
            }
            return null;
        }
        private object Ray(BasePlayer player, Vector3 Aim)
        {
            var hits = Physics.RaycastAll(player.transform.position + new Vector3(0f, 1.5f, 0f), Aim);
            float distance = 50f;
            object target = null;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BaseEntity>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BaseEntity>();
                    }
                }
            }
            return target;
        }
        private void SetVendorName()
        {
            foreach (var npc in vendors.StoryVendors)
            {
                var player = BasePlayer.FindByID(ulong.Parse(npc.Key));
                if (player != null)
                {
                    player.displayName = npc.Value.Name;
                }
            }
        }
        private void RemoveVendor(BasePlayer player, string ID, bool isStory)
        {
            if (isStory)
            {
                vendors.StoryVendors.Remove(ID);

                foreach (var user in PlayerProgress)
                {
                    if (user.Value.Stories.ContainsKey(ID))
                        user.Value.Stories.Remove(ID);
                }
            }
            DeleteNPCMenu(player);
            PopupMessage(player, $"You have successfully removed the npc with ID: {ID}");
            SaveVendorData();
        }
        private string GetRandomNPC(string ID) // useless gerade
        {
            List<string> npcIDs = vendors.StoryVendors.Keys.ToList();
            List<string> withoutSelected = npcIDs;
            if (withoutSelected.Contains(ID))
                withoutSelected.Remove(ID);
            var randNum = UnityEngine.Random.Range(0, withoutSelected.Count); // was ...count - 1 -> not working
            return withoutSelected[randNum];
        }
        private string LA(string key, string userID = null) => lang.GetMessage(key, this, userID);
        #endregion

        #region UI
        private void CreateMenu(BasePlayer player)
        {
            var MenuElement = QUI.CreateElementContainer(UIMain, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0 0", "0.12 1");
            QUI.CreatePanel(ref MenuElement, UIMain, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.05 0.01", "0.95 0.99", true);
            QUI.CreateLabel(ref MenuElement, UIMain, "", $"{textPrimary}Story</color>", 30, "0.05 0.9", "0.95 1");
            int i = 0;
            CreateMenuButton(ref MenuElement, UIMain, LA("Your Quests", player.UserIDString), "QUI_ChangeElement personal", i); i++;

            if (player.IsAdmin)
            {
                QUI.CreateButton(ref MenuElement, UIMain, QUI.Color(configData.Colors.Button_Accept.Color, configData.Colors.Button_Accept.Alpha), LA("Create Quest", player.UserIDString), 18, "0.1 0.225", "0.9 0.28", "QUI_ChangeElement creation");
                QUI.CreateButton(ref MenuElement, UIMain, QUI.Color(configData.Colors.Button_Pending.Color, configData.Colors.Button_Pending.Alpha), LA("Edit Quest", player.UserIDString), 18, "0.1 0.16", "0.9 0.215", "QUI_ChangeElement editor");
                QUI.CreateButton(ref MenuElement, UIMain, QUI.Color(configData.Colors.Button_Cancel.Color, configData.Colors.Button_Cancel.Alpha), LA("Delete Quest", player.UserIDString), 18, "0.1 0.095", "0.9 0.15", "QUI_DeleteQuest");
            }

            QUI.CreateButton(ref MenuElement, UIMain, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Close", player.UserIDString), 18, "0.1 0.03", "0.9 0.085", "QUI_DestroyAll");
            CuiHelper.AddUi(player, MenuElement);
        }
        private void CreateEmptyMenu(BasePlayer player)
        {
            var MenuElement = QUI.CreateElementContainer(UIMain, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0 0", "0.12 1");
            QUI.CreatePanel(ref MenuElement, UIMain, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.05 0.01", "0.95 0.99", true);
            QUI.CreateLabel(ref MenuElement, UIMain, "", $"{textPrimary}Story</color>", 30, "0.05 0.9", "0.95 1");
            CreateMenuButton(ref MenuElement, UIMain, LA("Your Quests", player.UserIDString), "QUI_ChangeElement personal", 4);

            QUI.CreateButton(ref MenuElement, UIMain, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Close", player.UserIDString), 18, "0.1 0.03", "0.9 0.085", "QUI_DestroyAll");
            CuiHelper.AddUi(player, MenuElement);
        }
        private void CreateMenuButton(ref CuiElementContainer container, string panelName, string buttonname, string command, int number)
        {
            Vector2 dimensions = new Vector2(0.8f, 0.055f);
            Vector2 origin = new Vector2(0.1f, 0.75f);
            Vector2 offset = new Vector2(0, (0.01f + dimensions.y) * number);

            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;

            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), buttonname, 18, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void ListElement(BasePlayer player, StoryType type, int page = 0)
        {
            DestroyEntries(player);
            var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);
            QUI.CreateLabel(ref Main, UIPanel, "", GetTypeDescription(type), 16, "0.1 0.93", "0.9 0.99");
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.015", type.ToString().ToUpper(), 200, "0.01 0.01", "0.99 0.99");
            var stories = StoryDict[type];
            if (stories.Count > 16)
            {
                var maxpages = (stories.Count - 1) / 16 + 1;
                if (page < maxpages - 1)
                    QUI.CreateButton(ref Main, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Next", player.UserIDString), 16, "0.86 0.94", "0.97 0.98", $"QUI_ChangeElement listpage {type} {page + 1}");
                if (page > 0)
                    QUI.CreateButton(ref Main, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Back", player.UserIDString), 16, "0.03 0.94", "0.14 0.98", $"QUI_ChangeElement listpage {type} {page - 1}");
            }
            int maxentries = (16 * (page + 1));
            if (maxentries > stories.Count)
                maxentries = stories.Count;
            int rewardcount = 16 * page;
            List<string> questNames = new List<string>();
            foreach (var entry in StoryDict[type])
                questNames.Add(entry.Key);

            if (stories.Count == 0)
                QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("noQ", player.UserIDString)} {type.ToString().ToLower()} {LA("quests", player.UserIDString)} </color>", 24, "0 0.82", "1 0.9");

            CuiHelper.AddUi(player, Main);

            int i = 0;
            for (int n = rewardcount; n < maxentries; n++)
            {
                CreateStoryEntry(player, stories[questNames[n]], i);
                i++;
            }
        }
        private List<StoryEntry> SortStoriesAfterLineAndNumber(List<StoryEntry> givenList)
        {
            List<StoryEntry> sortedList = givenList.OrderBy(o => o.StoryNumber).ToList();
            sortedList = sortedList.OrderBy(o => o.StoryLine).ToList();
            return sortedList;
        }
        private StoryEntry CheckForAvailableStory(string npcID, BasePlayer player)
        {
            DestroyEntries(player);

            List<StoryEntry> allAvailStories = new List<StoryEntry>();

            foreach (var entry in storyData.StoryDict.Values)
            {
                foreach (var entryInEntry in entry.Values)
                {
                    if (entryInEntry.Vendor.ID == npcID)
                    {
                        allAvailStories.Add(entryInEntry);
                    }
                }
            }

            allAvailStories = SortStoriesAfterLineAndNumber(allAvailStories);

            foreach (var tmpOrder in allAvailStories)
            {
                Puts("Quest: " + tmpOrder.StoryName + ", Storyline: " + tmpOrder.StoryLine + ", Number: " + tmpOrder.StoryNumber);
            }


            if (allAvailStories.Count != 0)
            {
                foreach(var npcStories in allAvailStories)
                {
                    if(!PlayerProgress[player.userID].DoneStories.Contains(npcStories.StoryName) && (PlayerProgress[player.userID].DoneStories.Contains(npcStories.RequiredQuest) || npcStories.RequiredQuest == null))
                    {
                        return npcStories;
                    }
                }        
            }
            return null;
        }
        private void CreateStoryEntry(BasePlayer player, StoryEntry entry, int num)
        {
            Vector2 posMin = CalcQuestPos(num);
            Vector2 dimensions = new Vector2(0.21f, 0.22f);
            Vector2 posMax = posMin + dimensions;

            var panelName = UIEntry + num;
            AddUIString(player, panelName);

            var questEntry = QUI.CreateElementContainer(panelName, "0 0 0 0", $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}");
            QUI.CreatePanel(ref questEntry, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), $"0 0", $"1 1");

            string buttonCommand = "";
            string buttonText = "";
            string buttonColor = "";
            StoryStatus status = StoryStatus.Open;
            var prog = PlayerProgress[player.userID].Stories;
            if (prog.ContainsKey(entry.StoryName))
            {
                status = prog[entry.StoryName].Status;
                switch (prog[entry.StoryName].Status)
                {
                    case StoryStatus.Pending:

                        buttonColor = QUI.Color(configData.Colors.Button_Pending.Color, configData.Colors.Button_Pending.Alpha);
                        buttonText = LA("Pending", player.UserIDString);
                        break;
                    case StoryStatus.Completed:
                        buttonColor = QUI.Color(configData.Colors.Button_Completed.Color, configData.Colors.Button_Completed.Alpha);
                        buttonText = LA("Completed", player.UserIDString);
                        break;
                }
            }
            else
            {
                buttonColor = QUI.Color(configData.Colors.Button_Accept.Color, configData.Colors.Button_Accept.Alpha);
                buttonText = LA("Accept Quest", player.UserIDString);
                buttonCommand = $"QUI_AcceptStory {entry.StoryName}";
            }
            QUI.CreateButton(ref questEntry, panelName, buttonColor, buttonText, 14, $"0.72 0.83", $"0.98 0.97", buttonCommand);

            string rewards = GetRewardString(entry.Rewards);
            string questInfo = $"{textPrimary}{LA("Status:", player.UserIDString)}</color> {status}";
            questInfo = questInfo + $"\n{textPrimary}{LA("Description:", player.UserIDString)} </color>{textSecondary}{entry.Description}</color>";
            questInfo = questInfo + $"\n{textPrimary}{LA("Objective:", player.UserIDString)} </color>{textSecondary}{entry.ObjectiveName}</color>";
            questInfo = questInfo + $"\n{textPrimary}{LA("Amount Required:", player.UserIDString)} </color>{textSecondary}{entry.AmountRequired}</color>";
            questInfo = questInfo + $"\n{textPrimary}{LA("Reward:", player.UserIDString)} </color>{textSecondary}{rewards}</color>";

            QUI.CreateLabel(ref questEntry, panelName, "", $"{entry.StoryName}", 16, $"0.02 0.8", "0.72 0.95", TextAnchor.MiddleLeft);
            QUI.CreateLabel(ref questEntry, panelName, buttonColor, questInfo, 14, $"0.02 0.01", "0.98 0.78", TextAnchor.UpperLeft);

            CuiHelper.AddUi(player, questEntry);
        }
        private void PlayerStats(BasePlayer player, int page = 0)
        {
            DestroyEntries(player);
            var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);
            QUI.CreateLabel(ref Main, UIPanel, "", LA("yqDesc", player.UserIDString), 16, "0.1 0.93", "0.9 0.99");
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.015", LA("STATS", player.UserIDString), 200, "0.01 0.01", "0.99 0.99");

            var stats = PlayerProgress[player.userID];
            if (stats.Stories.Count > 16)
            {
                var maxpages = (stats.Stories.Count - 1) / 16 + 1;
                if (page < maxpages - 1)
                    QUI.CreateButton(ref Main, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Next", player.UserIDString), 16, "0.86 0.94", "0.97 0.98", $"QUI_ChangeElement statspage {page + 1}");
                if (page > 0)
                    QUI.CreateButton(ref Main, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Back", player.UserIDString), 16, "0.03 0.94", "0.14 0.098", $"QUI_ChangeElement statspage {page - 1}");
            }
            int maxentries = (16 * (page + 1));
            if (maxentries > stats.Stories.Count)
                maxentries = stats.Stories.Count;
            int rewardcount = 16 * page;
            List<string> questNames = new List<string>();
            foreach (var entry in stats.Stories)
                questNames.Add(entry.Key);

            if (stats.Stories.Count == 0)
                QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("noQDSaved", player.UserIDString)}</color>", 24, "0 0.82", "1 0.9");

            CuiHelper.AddUi(player, Main);

            int i = 0;
            for (int n = rewardcount; n < maxentries; n++)
            {
                var Quest = GetStory(questNames[n]);
                if (Quest == null) continue;
                CreateStatEntry(player, Quest, i);
                i++;
            }
        }
        private void CreateStatEntry(BasePlayer player, StoryEntry entry, int num)
        {
            Vector2 posMin = CalcQuestPos(num);
            Vector2 dimensions = new Vector2(0.21f, 0.22f);
            Vector2 posMax = posMin + dimensions;

            var panelName = UIEntry + num;
            AddUIString(player, panelName);

            var questEntry = QUI.CreateElementContainer(panelName, "0 0 0 0", $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}");
            QUI.CreatePanel(ref questEntry, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), $"0 0", $"1 1");

            string statusColor = "";
            StoryStatus status = StoryStatus.Open;
            var prog = PlayerProgress[player.userID].Stories;
            if (prog.ContainsKey(entry.StoryName))
            {
                status = prog[entry.StoryName].Status;
                switch (prog[entry.StoryName].Status)
                {
                    case StoryStatus.Pending:
                        statusColor = QUI.Color(configData.Colors.Button_Pending.Color, configData.Colors.Button_Pending.Alpha);
                        break;
                    case StoryStatus.Completed:
                        statusColor = QUI.Color(configData.Colors.Button_Completed.Color, configData.Colors.Button_Completed.Alpha);
                        break;
                }
            }

            if (status != StoryStatus.Completed)
                QUI.CreateButton(ref questEntry, panelName, QUI.Color(configData.Colors.Button_Cancel.Color, configData.Colors.Button_Cancel.Alpha), LA("Cancel Quest", player.UserIDString), 16, $"0.75 0.83", $"0.97 0.97", $"QUI_CancelQuest {entry.StoryName}");
            if (status == StoryStatus.Completed && !prog[entry.StoryName].RewardClaimed)
                QUI.CreateButton(ref questEntry, panelName, statusColor, LA("Claim Reward", player.UserIDString), 16, $"0.75 0.83", $"0.97 0.97", $"QUI_ClaimReward {entry.StoryName}");
            string questStatus = status.ToString();
            if (status == StoryStatus.Completed && prog[entry.StoryName].RewardClaimed)
            {
                if (prog[entry.StoryName].ResetTime < GrabCurrentTime())
                    QUI.CreateButton(ref questEntry, panelName, statusColor, LA("Remove", player.UserIDString), 16, $"0.75 0.83", $"0.97 0.97", $"QUI_RemoveCompleted {entry.StoryName}");
                else
                {
                    TimeSpan dateDifference = TimeSpan.FromSeconds(prog[entry.StoryName].ResetTime - GrabCurrentTime());
                    var days = dateDifference.Days;
                    var hours = dateDifference.Hours;
                    hours += (days * 24);
                    var mins = dateDifference.Minutes;
                    string remaining = string.Format("{0:00}h :{1:00}m", hours, mins);
                    questStatus = $"{LA("Cooldown:", player.UserIDString)} {remaining}";
                }

            }
            var rewards = GetRewardString(entry.Rewards);
            var percent = Math.Round(Convert.ToDouble((float)prog[entry.StoryName].AmountCollected / (float)entry.AmountRequired), 4);
            //Puts($"Collected: {prog[entry.QuestName].AmountCollected.ToString()}, Required: {entry.AmountRequired.ToString()}, Pct: {percent.ToString()}");
            string stats = $"{textPrimary}{LA("Status:", player.UserIDString)}</color> {questStatus}";
            stats += $"\n{textPrimary}{LA("Quest Type:", player.UserIDString)} </color> {textSecondary}{prog[entry.StoryName].Type}</color>";
            stats += $"\n{textPrimary}{LA("Description:", player.UserIDString)} </color>{textSecondary}{entry.Description}</color>";
            stats += $"\n{textPrimary}{LA("Objective:", player.UserIDString)} </color>{textSecondary}{entry.AmountRequired}x {entry.ObjectiveName}</color>";
            stats += $"\n{textPrimary}{LA("Collected:", player.UserIDString)} </color>{textSecondary}{prog[entry.StoryName].AmountCollected}</color> {textPrimary}({percent * 100}%)</color>";
            stats += $"\n{textPrimary}{LA("Reward:", player.UserIDString)} </color>{textSecondary}{rewards}</color>";

            QUI.CreateLabel(ref questEntry, panelName, "", $"{entry.StoryName}", 18, $"0.02 0.8", "0.8 0.95", TextAnchor.UpperLeft);
            QUI.CreateLabel(ref questEntry, panelName, "", stats, 14, $"0.02 0.01", "0.98 0.78", TextAnchor.UpperLeft);

            CuiHelper.AddUi(player, questEntry);
        }
        //TODO: für walk
        private void PlayerStory(BasePlayer player)
        {
            DestroyEntries(player);
            /*var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);
            QUI.CreateLabel(ref Main, UIPanel, "", GetTypeDescription(StoryType.Walk), 16, "0.1 0.93", "0.9 0.99");
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.015", LA("DELIVERY", player.UserIDString), 200, "0.01 0.01", "0.99 0.99");

            var npcid = PlayerProgress[player.userID].CurrentStory.VendorID;
            var targetid = PlayerProgress[player.userID].CurrentStory.TargetID;
            if (string.IsNullOrEmpty(npcid))
                QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("noADM", player.UserIDString)}</color>", 24, "0 0.82", "1 0.9");
            else
            {
                var quest = vendors.StoryVendors[npcid];
                var target = vendors.StoryVendors[targetid];
                if (quest != null && target != null)
                {
                    var distance = Vector2.Distance(new Vector2(quest.Info.x, quest.Info.z), new Vector2(target.Info.x, target.Info.z));
                    var rewardAmount = distance * quest.Multiplier;
                    if (rewardAmount < 1) rewardAmount = 1;
                    var briefing = $"{textPrimary}{quest.Info.Name}\n\n</color>";
                    briefing = briefing + $"{textSecondary}{quest.Description}</color>\n\n";
                    briefing = briefing + $"{textPrimary}{LA("Destination:", player.UserIDString)} </color>{textSecondary}{target.Info.Name} ({(string)Grid.Call("getGrid", new Vector3(target.Info.x, 0, target.Info.z))})\nX {target.Info.x}, Z {target.Info.z}</color>\n";
                    briefing = briefing + $"{textPrimary}{LA("Distance:", player.UserIDString)} </color>{textSecondary}{distance}M</color>\n";
                    briefing = briefing + $"{textPrimary}{LA("Reward:", player.UserIDString)} </color>{textSecondary}{(int)rewardAmount}x {quest.Reward.DisplayName}</color>";
                    QUI.CreateLabel(ref Main, UIPanel, "", briefing, 20, "0.15 0.2", "0.85 1", TextAnchor.MiddleLeft);

                    QUI.CreateButton(ref Main, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Cancel", player.UserIDString), 18, "0.2 0.05", "0.35 0.1", $"QUI_CancelStory");
                }
            }
            CuiHelper.AddUi(player, Main);*/
        }
        private void CreationMenu(BasePlayer player)
        {
            DestroyEntries(player);
            var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);

            int i = 0;
            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("selCreat", player.UserIDString)}</color>", 20, "0.25 0.8", "0.75 0.9");
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.025", LA("CREATOR", player.UserIDString), 200, "0.01 0.01", "0.99 0.99");
            if (HumanNPC)
            {
                CreateNewQuestButton(ref Main, UIPanel, LA("Kill", player.UserIDString), "QUI_NewQuest kill", i); i++;
                CreateNewQuestButton(ref Main, UIPanel, LA("Gather", player.UserIDString), "QUI_NewQuest gather", i); i++;
                CreateNewQuestButton(ref Main, UIPanel, LA("Loot", player.UserIDString), "QUI_NewQuest loot", i); i++;
                CreateNewQuestButton(ref Main, UIPanel, LA("Craft", player.UserIDString), "QUI_NewQuest craft", i); i++;
                CreateNewQuestButton(ref Main, UIPanel, LA("Walking", player.UserIDString), "QUI_NewQuest walk", i); i++;
            }
            CuiHelper.AddUi(player, Main);
        }
        //TODO:
        private void StoryHelp(BasePlayer player, int page = 0)
        {
            DestroyEntries(player);
            StoryCreator quest = null;
            if (ActiveCreations.ContainsKey(player.userID))
                quest = ActiveCreations[player.userID];
            else if (ActiveEditors.ContainsKey(player.userID))
                quest = ActiveEditors[player.userID];
            if (quest == null) return;

            var HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9");
            QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");

            switch (page)
            {
                case 0:

                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelMen", player.UserIDString)}.\n</color> {textSecondary}{LA("creHelFol", player.UserIDString)}.\n\n{LA("creHelExi", player.UserIDString)} </color>{textPrimary}'exit'\n\n\n\n{LA("creHelName", player.UserIDString)}</color>", 20, "0 0", "1 1");
                    break;
                case 1:
                    var MenuMain = QUI.CreateElementContainer(UIMain, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0 0", "1 1", true);
                    QUI.CreatePanel(ref MenuMain, UIMain, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99");
                    QUI.CreateLabel(ref MenuMain, UIMain, "", $"{textPrimary}{LA("creHelObj", player.UserIDString)}</color>", 20, "0.25 0.85", "0.75 0.95");
                    CuiHelper.AddUi(player, MenuMain);
                    CreateObjectiveMenu(player);
                    return;
                case 2:
                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelRA", player.UserIDString)}</color>", 20, "0.25 0.4", "0.75 0.6");
                    break;
                case 3:
                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelQD", player.UserIDString)}</color>", 20, "0.25 0.4", "0.75 0.6");
                    break;
                case 4:
                    {
                        HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9");
                        QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98", true);
                        QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelRT", player.UserIDString)}</color>", 20, "0.25 0.8", "0.75 1");
                        int i = 0; 
                        if (ServerRewards) CreateRewardTypeButton(ref HelpMain, UIPanel, $"{LA("RP", player.UserIDString)} (ServerRewards)", "QUI_RewardType rp", i); i++;
                        CreateRewardTypeButton(ref HelpMain, UIPanel, LA("Item", player.UserIDString), "QUI_RewardType item", i); i++;
                    }
                    break;
                case 5:
                    if (quest.item.isRP)
                        QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelRewA", player.UserIDString)}</color>", 20, "0.25 0.4", "0.75 0.6");
                    else
                    {
                        HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.3 0.8", "0.7 0.97");
                        QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
                        QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelIH", player.UserIDString)} 'story item'</color>", 20, "0.1 0", "0.9 1");
                    }
                    break;
                case 7:
                    HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9", true);
                    QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelAR", player.UserIDString)}</color>", 20, "0.1 0", "0.9 1");
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Yes", player.UserIDString), 18, "0.6 0.05", "0.8 0.15", $"QUI_AddReward");
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("No", player.UserIDString), 18, "0.2 0.05", "0.4 0.15", $"QUI_RewardFinish");
                    break;
                case 8:
                    if (quest.type != StoryType.Kill)
                    {
                        HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9", true);
                        QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
                        QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelID", player.UserIDString)}</color>", 20, "0.1 0", "0.9 1");
                        QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Yes", player.UserIDString), 18, "0.6 0.05", "0.8 0.15", $"QUI_ItemDeduction 1");
                        QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("No", player.UserIDString), 18, "0.2 0.05", "0.4 0.15", $"QUI_ItemDeduction 0");
                    }
                    else { StoryHelp(player, 9); return; }
                    break;
                case 9: // Creating the NPC (vendor)
                    MenuMain = QUI.CreateElementContainer(UIMain, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0 0", "1 1", true);
                    QUI.CreatePanel(ref MenuMain, UIMain, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99");
                    QUI.CreateLabel(ref MenuMain, UIMain, "", $"{textPrimary}{LA("delHelNewNPC_vendor", player.UserIDString)}</color>", 20, "0.25 0.85", "0.75 0.95");
                    CuiHelper.AddUi(player, MenuMain);
                    CreateNPCMenu(player, true);
                    return;
                case 11: // Creating the NPC (target)
                    MenuMain = QUI.CreateElementContainer(UIMain, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0 0", "1 1", true);
                    QUI.CreatePanel(ref MenuMain, UIMain, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99");
                    QUI.CreateLabel(ref MenuMain, UIMain, "", $"{textPrimary}{LA("delHelNewNPC_target", player.UserIDString)}</color>", 20, "0.25 0.85", "0.75 0.95");
                    CuiHelper.AddUi(player, MenuMain);
                    CreateNPCMenu(player, false);
                    return;
                case 12: // attaching required quest
                    MenuMain = QUI.CreateElementContainer(UIMain, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0 0", "1 1", true);
                    QUI.CreatePanel(ref MenuMain, UIMain, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99");
                    QUI.CreateLabel(ref MenuMain, UIMain, "", $"{textPrimary}{LA("delHelReqQuest", player.UserIDString)}</color>", 20, "0.25 0.85", "0.75 0.95");
                    CuiHelper.AddUi(player, MenuMain);
                    CreateQuestMenu(player);
                    return;
                case 13: // attaching StoryLine
                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("delHelStoryLine", player.UserIDString)}</color>", 20, "0.25 0.4", "0.75 0.6");
                    quest.partNum = 13;
                    break;
                case 14: // attaching Story number
                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("delHelStoryNumber", player.UserIDString)}</color>", 20, "0.25 0.4", "0.75 0.6");
                    break;
                case 10:
                    {
                        HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9");
                        QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98", true);
                        QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelNewRew", player.UserIDString)}</color>", 20, "0.25 0.8", "0.75 1");
                        QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("addNewRew", player.UserIDString), 18, "0.7 0.04", "0.95 0.12", $"QUI_AddReward");
                        QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Back", player.UserIDString), 18, "0.05 0.04", "0.3 0.12", $"QUI_EndEditing");

                        int i = 0;
                        foreach (var entry in ActiveEditors[player.userID].entry.Rewards)
                        {
                            CreateDelEditButton(ref HelpMain, 0.1f, UIPanel, $"{entry.Amount}x {entry.DisplayName}", i, "", 0.35f);
                            CreateDelEditButton(ref HelpMain, 0.72f, UIPanel, LA("Remove", player.UserIDString), i, $"QUI_RemoveReward {entry.Amount} {entry.DisplayName}");
                            i++;
                        }
                    }
                    break;
                default:
                    HelpMain = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9", true);
                    QUI.CreatePanel(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
                    QUI.CreateLabel(ref HelpMain, UIPanel, "", $"{textPrimary}{LA("creHelSQ", player.UserIDString)}</color>", 20, "0.1 0.8", "0.9 0.95");
                    string questDetails = $"{textPrimary}{LA("Quest Type:", player.UserIDString)}</color> {textSecondary}{quest.type}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Name:", player.UserIDString)}</color> {textSecondary}{quest.entry.StoryName}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Description:", player.UserIDString)}</color> {textSecondary}{quest.entry.Description}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Objective:", player.UserIDString)}</color> {textSecondary}{quest.entry.ObjectiveName}</color>";
                    if (quest.type != StoryType.Walk) questDetails = questDetails + $"\n{textPrimary}{LA("Required Amount:", player.UserIDString)}</color> {textSecondary}{quest.entry.AmountRequired}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Start NPC:", player.UserIDString)}</color> {textSecondary}{quest.entry.Vendor.Name}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Target NPC:", player.UserIDString)}</color> {textSecondary}{quest.entry.Target.Name}</color>";
                    if (quest.type != StoryType.Kill) questDetails = questDetails + $"\n{textPrimary}{LA("Item Deduction:", player.UserIDString)}</color> {textSecondary}{quest.entry.ItemDeduction}</color>";

                    string reqQuest = quest.entry.RequiredQuest;
                    if (quest.entry.RequiredQuest == null) reqQuest = "None";

                    questDetails = questDetails + $"\n{textPrimary}{LA("Req. Quest:", player.UserIDString)}</color> {textSecondary}{reqQuest}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Storyline:", player.UserIDString)}</color> {textSecondary}{quest.entry.StoryLine.ToString()}</color>";
                    questDetails = questDetails + $"\n{textPrimary}{LA("Story number:", player.UserIDString)}</color> {textSecondary}{quest.entry.StoryNumber.ToString()}</color>";

                    var rewards = GetRewardString(quest.entry.Rewards);

                    questDetails = questDetails + $"\n{textPrimary}{LA("Reward:", player.UserIDString)}</color> {textSecondary}{rewards}</color>";

                    QUI.CreateLabel(ref HelpMain, UIPanel, "", questDetails, 15, "0.1 0.2", "0.9 0.75", TextAnchor.MiddleLeft);
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Save Quest", player.UserIDString), 18, "0.6 0.05", "0.8 0.15", $"QUI_SaveQuest");
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Cancel", player.UserIDString), 18, "0.2 0.05", "0.4 0.15", $"QUI_ExitQuest");
                    break;
            }
            CuiHelper.AddUi(player, HelpMain);
        }
        private void CreateObjectiveMenu(BasePlayer player, int page = 0)
        {
            DestroyEntries(player);
            var HelpMain = QUI.CreateElementContainer(UIPanel, "0 0 0 0", "0 0", "1 1");
            StoryType type;
            if (ActiveCreations.ContainsKey(player.userID))
                type = ActiveCreations[player.userID].type;
            else type = ActiveEditors[player.userID].type;
            var objCount = AllObjectives[type].Count;
            if (objCount > 100)
            {
                var maxpages = (objCount - 1) / 96 + 1;
                if (page < maxpages - 1)
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Next", player.UserIDString), 18, "0.84 0.05", "0.97 0.1", $"QUI_ChangeElement objpage {page + 1}");
                if (page > 0)
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Back", player.UserIDString), 18, "0.03 0.05", "0.16 0.1", $"QUI_ChangeElement objpage {page - 1}");
            }
            int maxentries = (96 * (page + 1));
            if (maxentries > objCount)
                maxentries = objCount;
            int rewardcount = 96 * page;

            int i = 0;
            for (int n = rewardcount; n < maxentries; n++)
            {
                CreateObjectiveEntry(ref HelpMain, UIPanel, AllObjectives[type][n], i);
                i++;
            }
            CuiHelper.AddUi(player, HelpMain);
        }
        private List<StoryEntry> GetAllQuests()
        {
            List<StoryEntry> tmpList = new List<StoryEntry>();
            foreach (var entry in storyData.StoryDict.Values)
            {
                foreach (var entryInEntry in entry.Values)
                {
                    tmpList.Add(entryInEntry);
                }
            }
            return tmpList;
        }
        private List<string> GetAllQuestNames()
        {
            List<string> tmpList = new List<string>();
            foreach (var entry in storyData.StoryDict.Values)
            {
                foreach (var entryInEntry in entry.Values)
                {
                    tmpList.Add(entryInEntry.StoryName);
                }
            }
            return tmpList;
        }
        private void CreateQuestMenu(BasePlayer player, int page = 0)
        {
            DestroyEntries(player);

            List<string> questNames = GetAllQuestNames(); // get quests-list

            var HelpMain = QUI.CreateElementContainer(UIPanel, "0 0 0 0", "0 0", "1 1");

            var questCount = questNames.Count;

            if (questCount > 100)
            {
                var maxpages = (questCount - 1) / 96 + 1;
                if (page < maxpages - 1)
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Next", player.UserIDString), 18, "0.84 0.05", "0.97 0.1", $"QUI_ChangeElement questpage {page + 1}");
                if (page > 0)
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Back", player.UserIDString), 18, "0.03 0.05", "0.16 0.1", $"QUI_ChangeElement questpage {page - 1}");
            }
            int maxentries = (96 * (page + 1));
            if (maxentries > questCount)
                maxentries = questCount;
            int rewardcount = 96 * page;

            int i = 0;
            for (int n = rewardcount; n < maxentries; n++)
            {
                CreateQuestEntry(ref HelpMain, UIPanel, questNames[n], i);
                i++;
            }
            CreateQuestEntry(ref HelpMain, UIPanel, "None", i);
            CuiHelper.AddUi(player, HelpMain);
        }
        private void CreateNPCMenu(BasePlayer player, bool vendor = true, int page = 0)
        {
            DestroyEntries(player);

            List<NPCInfo> AllNPCs = GetAllNPCs(); // load npc-list
            if (AllNPCs == null) return;

            var HelpMain = QUI.CreateElementContainer(UIPanel, "0 0 0 0", "0 0", "1 1");

            var npcCount = AllNPCs.Count;

            if (npcCount > 100)
            {
                var maxpages = (npcCount - 1) / 96 + 1;
                if (page < maxpages - 1)
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Next", player.UserIDString), 18, "0.84 0.05", "0.97 0.1", $"QUI_ChangeElement npcpage {page + 1} {vendor}");
                if (page > 0)
                    QUI.CreateButton(ref HelpMain, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Back", player.UserIDString), 18, "0.03 0.05", "0.16 0.1", $"QUI_ChangeElement npcpage {page - 1} {vendor}");
            }
            int maxentries = (96 * (page + 1));
            if (maxentries > npcCount)
                maxentries = npcCount;
            int rewardcount = 96 * page;

            int i = 0;
            for (int n = rewardcount; n < maxentries; n++)
            {
                CreateNPCEntry(ref HelpMain, UIPanel, AllNPCs[n], i, vendor);
                i++;
            }
            CuiHelper.AddUi(player, HelpMain);
        }
        //TODO:
        private void AcceptStory(BasePlayer player, string npcID, int page = 0)
        {
            switch (page)
            {
                case 0: // quest vorschlagen
                    {
                        var nextQuest = CheckForAvailableStory(npcID, player);           
                        if (nextQuest != null)
                        {
                            Puts("Es gibt Stories");

                            var distance = Vector2.Distance(new Vector2(nextQuest.Vendor.x, nextQuest.Vendor.z), new Vector2(nextQuest.Target.x, nextQuest.Target.z));
                            var briefing = $"{textPrimary}{nextQuest.StoryName}\n\n</color>";
                            briefing = briefing + $"{textSecondary}{nextQuest.Description}</color>\n\n";
                            briefing = briefing + $"{textPrimary}{LA("Destination:", player.UserIDString)} </color>{textSecondary}{nextQuest.Target.Name} ({(string)Grid.Call("getGrid", new Vector3(nextQuest.Target.x, 0, nextQuest.Target.z))})\nX {nextQuest.Target.x}, Z {nextQuest.Target.z}</color>\n";
                            briefing = briefing + $"{textPrimary}{LA("Distance:", player.UserIDString)} </color>{textSecondary}{distance}M</color>\n";
                            briefing = briefing + $"{textPrimary}{LA("Reward:", player.UserIDString)} </color>{textSecondary}{GetRewardString(nextQuest.Rewards)}</color>";

                            var VendorUI = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9", true);
                            QUI.CreatePanel(ref VendorUI, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
                            QUI.CreateLabel(ref VendorUI, UIPanel, "", briefing, 20, "0.15 0.2", "0.85 1", TextAnchor.MiddleLeft);

                            QUI.CreateButton(ref VendorUI, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Accept", player.UserIDString), 18, "0.6 0.05", "0.8 0.15", $"QUI_AcceptStory {nextQuest.StoryName} {npcID}");
                            QUI.CreateButton(ref VendorUI, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Decline", player.UserIDString), 18, "0.2 0.05", "0.4 0.15", $"QUI_DestroyAll");
                            CuiHelper.AddUi(player, VendorUI);
                        }
                    }
                    return;
                case 1: // quest abgeben
                    {
                        var VendorUI = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.4 0.3", "0.95 0.9", true);
                        QUI.CreatePanel(ref VendorUI, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
                        QUI.CreateLabel(ref VendorUI, UIPanel, "", $"{textPrimary} {LA("delComplMSGStory", player.UserIDString)}</color>", 22, "0 0", "1 1");
                        QUI.CreateButton(ref VendorUI, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Claim", player.UserIDString), 18, "0.6 0.05", "0.8 0.15", $"QUI_FinishStory {npcID}");
                        QUI.CreateButton(ref VendorUI, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Cancel", player.UserIDString), 18, "0.2 0.05", "0.4 0.15", $"QUI_DestroyAll");
                        CuiHelper.AddUi(player, VendorUI);
                    }
                    return;
                default:
                    return;

            }
        }
        private void DeletionEditMenu(BasePlayer player, string page, string command)
        {
            DestroyEntries(player);
            var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.025", page, 200, "0.01 0.01", "0.99 0.99");

            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("Kill", player.UserIDString)}</color>", 20, "0 0.87", "0.2 0.92");
            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("Gather", player.UserIDString)}</color>", 20, "0.2 0.87", "0.4 0.92");
            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("Loot", player.UserIDString)}</color>", 20, "0.4 0.87", "0.6 0.92");
            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("Craft", player.UserIDString)}</color>", 20, "0.6 0.87", "0.8 0.92");
            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("Walk", player.UserIDString)}</color>", 20, "0.8 0.87", "1 0.92");
            //if (command == "QUI_ConfirmDelete") QUI.CreateButton(ref Main, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), $"{textPrimary}{LA("Delete NPC", player.UserIDString)}</color>", 18, "0.8 0.94", "0.98 0.98", "QUI_DeleteNPCMenu");

            int killNum = 0;
            int gatherNum = 0;
            int lootNum = 0;
            int craftNum = 0;
            int walkNum = 0;
            foreach (var entry in storyData.StoryDict[StoryType.Kill])
            {
                CreateDelEditButton(ref Main, 0.023f, UIPanel, entry.Key, killNum, command);
                killNum++;
            }
            foreach (var entry in storyData.StoryDict[StoryType.Gather])
            {
                CreateDelEditButton(ref Main, 0.223f, UIPanel, entry.Key, gatherNum, command);
                gatherNum++;
            }
            foreach (var entry in storyData.StoryDict[StoryType.Loot])
            {
                CreateDelEditButton(ref Main, 0.423f, UIPanel, entry.Key, lootNum, command);
                lootNum++;
            }
            foreach (var entry in storyData.StoryDict[StoryType.Craft])
            {
                CreateDelEditButton(ref Main, 0.623f, UIPanel, entry.Key, craftNum, command);
                craftNum++;
            }
            foreach (var entry in storyData.StoryDict[StoryType.Walk])
            {
                CreateDelEditButton(ref Main, 0.823f, UIPanel, entry.Key, walkNum, command);
                walkNum++;
            }
            CuiHelper.AddUi(player, Main);
        }
        private BasePlayer FindPlayerByID(ulong userid)
        {
            var allBasePlayer = Resources.FindObjectsOfTypeAll<BasePlayer>();
            foreach (BasePlayer player in allBasePlayer)
            {
                if (player.userID == userid) return player;
            }
            return null;
        }
        private void DeleteNPCMenu(BasePlayer player)
        {
            DestroyEntries(player);
            var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.025", LA("REMOVER", player.UserIDString), 200, "0.01 0.01", "0.99 0.99");

            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("Story Vendors", player.UserIDString)}</color>", 20, "0 0.87", "0.5 0.92");

            int StoryNum = 0;
            foreach (var entry in vendors.StoryVendors)
            {
                CreateDelVendorButton(ref Main, 0.535f, UIPanel, entry.Value.Name, StoryNum, $"QUI_RemoveVendor {entry.Key}");
                StoryNum++;
            }
            CuiHelper.AddUi(player, Main);
        }
        private void ConfirmDeletion(BasePlayer player, string questName)
        {
            var ConfirmDelete = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.2 0.4", "0.8 0.8", true);
            QUI.CreatePanel(ref ConfirmDelete, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
            QUI.CreateLabel(ref ConfirmDelete, UIPanel, "", $"{textPrimary}{LA("confDel", player.UserIDString)} {questName}</color>", 20, "0.1 0.6", "0.9 0.9");
            QUI.CreateButton(ref ConfirmDelete, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Yes", player.UserIDString), 18, "0.6 0.2", "0.8 0.3", $"QUI_DeleteQuest {questName}");
            QUI.CreateButton(ref ConfirmDelete, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("No", player.UserIDString), 18, "0.2 0.2", "0.4 0.3", $"QUI_DeleteQuest reject");

            CuiHelper.AddUi(player, ConfirmDelete);
        }
        private void ConfirmCancellation(BasePlayer player, string questName)
        {
            var ConfirmDelete = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.2 0.4", "0.8 0.8", true);
            QUI.CreatePanel(ref ConfirmDelete, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.02", "0.99 0.98");
            QUI.CreateLabel(ref ConfirmDelete, UIPanel, "", $"{textPrimary}{LA("confCan", player.UserIDString)} {questName}</color>\n{textSecondary}{LA("confCan2", player.UserIDString)}</color>", 20, "0.1 0.6", "0.9 0.9");
            QUI.CreateButton(ref ConfirmDelete, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("Yes", player.UserIDString), 18, "0.6 0.2", "0.8 0.3", $"QUI_ConfirmCancel {questName}");
            QUI.CreateButton(ref ConfirmDelete, UIPanel, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), LA("No", player.UserIDString), 18, "0.2 0.2", "0.4 0.3", $"QUI_ConfirmCancel reject");

            CuiHelper.AddUi(player, ConfirmDelete);
        }
        private void StoryEditorMenu(BasePlayer player)
        {
            DestroyEntries(player);
            var Main = QUI.CreateElementContainer(UIPanel, QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.12 0", "1 1");
            QUI.CreatePanel(ref Main, UIPanel, QUI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha), "0.01 0.01", "0.99 0.99", true);
            QUI.CreateLabel(ref Main, UIPanel, "1 1 1 0.025", LA("EDITOR", player.UserIDString), 200, "0.01 0.01", "0.99 0.99");

            var type = ActiveEditors[player.userID].type;

            int i = 0;
            QUI.CreateLabel(ref Main, UIPanel, "", $"{textPrimary}{LA("chaEdi", player.UserIDString)}</color>", 20, "0.25 0.8", "0.75 0.9");
            CreateNewQuestButton(ref Main, UIPanel, LA("Name", player.UserIDString), "QUI_EditQuestVar name", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Description", player.UserIDString), "QUI_EditQuestVar description", i); i++;
            if (type != StoryType.Walk)
            {
                CreateNewQuestButton(ref Main, UIPanel, LA("Objective", player.UserIDString), "QUI_EditQuestVar objective", i);
                i++;
            }
            CreateNewQuestButton(ref Main, UIPanel, LA("Amount", player.UserIDString), "QUI_EditQuestVar amount", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Reward", player.UserIDString), "QUI_EditQuestVar reward", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Start NPC", player.UserIDString), "QUI_EditQuestVar startnpc", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Target NPC", player.UserIDString), "QUI_EditQuestVar targetnpc", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Req. Quest", player.UserIDString), "QUI_EditQuestVar reqquest", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Storyline", player.UserIDString), "QUI_EditQuestVar storyline", i); i++;
            CreateNewQuestButton(ref Main, UIPanel, LA("Story number", player.UserIDString), "QUI_EditQuestVar storynumber", i); i++;

            CuiHelper.AddUi(player, Main);
        }      
        private void CreateQuestEntry(ref CuiElementContainer container, string panelName, string storyName, int number, bool vendor = false)
        {
            var pos = CalcEntryPos(number);
            string questName = storyName;
            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), questName, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"QUI_SelectReqQuest {questName}");
        }
        private void CreateNPCEntry(ref CuiElementContainer container, string panelName, NPCInfo npcInfo, int number, bool vendor = false)
        {
            var pos = CalcEntryPos(number);
            string npcID = npcInfo.ID.ToString();
            string buttonText = npcInfo.Name + "\n" + npcID;
            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), buttonText, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"QUI_SelectNpc {npcID} {vendor}");
        }
        private void CreateObjectiveEntry(ref CuiElementContainer container, string panelName, string name, int number)
        {
            var pos = CalcEntryPos(number);
            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), name, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"QUI_SelectObj {name}");
        }
        private void CreateNewQuestButton(ref CuiElementContainer container, string panelName, string buttonname, string command, int number)
        {
            Vector2 dimensions = new Vector2(0.2f, 0.07f);
            Vector2 origin = new Vector2(0.4f, 0.7f);
            Vector2 offset = new Vector2(0, (0.01f + dimensions.y) * number);

            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;

            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), buttonname, 18, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateRewardTypeButton(ref CuiElementContainer container, string panelName, string buttonname, string command, int number)
        {
            Vector2 dimensions = new Vector2(0.36f, 0.1f);
            Vector2 origin = new Vector2(0.32f, 0.7f);
            Vector2 offset = new Vector2(0, (0.01f + dimensions.y) * number);

            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;

            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), buttonname, 18, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateDelEditButton(ref CuiElementContainer container, float xPos, string panelName, string buttonname, int number, string command, float width = 0.15f)
        {
            Vector2 dimensions = new Vector2(width, 0.05f);
            Vector2 origin = new Vector2(xPos, 0.8f);
            Vector2 offset = new Vector2(0, (-0.01f - dimensions.y) * number);

            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), buttonname, 14, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", $"{command} {buttonname}");
        }
        private void CreateDelVendorButton(ref CuiElementContainer container, float xPos, string panelName, string buttonname, int number, string command)
        {
            if (number > 15) xPos += 0.25f;
            Vector2 dimensions = new Vector2(0.18f, 0.05f);
            Vector2 origin = new Vector2(xPos, 0.8f);
            Vector2 offset = new Vector2(0, (-0.01f - dimensions.y) * number);

            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            QUI.CreateButton(ref container, panelName, QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), buttonname, 14, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void PopupMessage(BasePlayer player, string msg)
        {
            CuiHelper.DestroyUi(player, "PopupMsg");
            var element = QUI.CreateElementContainer("PopupMsg", QUI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha), "0.25 0.85", "0.75 0.95");
            QUI.CreatePanel(ref element, "PopupMsg", QUI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha), "0.005 0.04", "0.995 0.96");
            QUI.CreateLabel(ref element, "PopupMsg", "", $"{textPrimary}{msg}</color>", 22, "0 0", "1 1");
            CuiHelper.AddUi(player, element);
            timer.Once(3, () => CuiHelper.DestroyUi(player, "PopupMsg"));
        }
        private Vector2 CalcQuestPos(int number)
        {
            Vector2 position = new Vector2(0.1325f, 0.71f);
            Vector2 dimensions = new Vector2(0.21f, 0.22f);
            float offsetY = 0f;
            float offsetX = 0;
            if (number >= 0 && number < 4)
            {
                offsetX = (0.005f + dimensions.x) * number;
            }
            if (number > 3 && number < 8)
            {
                offsetX = (0.005f + dimensions.x) * (number - 4);
                offsetY = (-0.008f - dimensions.y) * 1;
            }
            if (number > 7 && number < 12)
            {
                offsetX = (0.005f + dimensions.x) * (number - 8);
                offsetY = (-0.008f - dimensions.y) * 2;
            }
            if (number > 11 && number < 16)
            {
                offsetX = (0.005f + dimensions.x) * (number - 12);
                offsetY = (-0.008f - dimensions.y) * 3;
            }
            return new Vector2(position.x + offsetX, position.y + offsetY);
        }
        private float[] CalcEntryPos(int number)
        {
            Vector2 position = new Vector2(0.014f, 0.8f);
            Vector2 dimensions = new Vector2(0.12f, 0.055f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 8)
            {
                offsetX = (0.002f + dimensions.x) * number;
            }
            if (number > 7 && number < 16)
            {
                offsetX = (0.002f + dimensions.x) * (number - 8);
                offsetY = (-0.0055f - dimensions.y) * 1;
            }
            if (number > 15 && number < 24)
            {
                offsetX = (0.002f + dimensions.x) * (number - 16);
                offsetY = (-0.0055f - dimensions.y) * 2;
            }
            if (number > 23 && number < 32)
            {
                offsetX = (0.002f + dimensions.x) * (number - 24);
                offsetY = (-0.0055f - dimensions.y) * 3;
            }
            if (number > 31 && number < 40)
            {
                offsetX = (0.002f + dimensions.x) * (number - 32);
                offsetY = (-0.0055f - dimensions.y) * 4;
            }
            if (number > 39 && number < 48)
            {
                offsetX = (0.002f + dimensions.x) * (number - 40);
                offsetY = (-0.0055f - dimensions.y) * 5;
            }
            if (number > 47 && number < 56)
            {
                offsetX = (0.002f + dimensions.x) * (number - 48);
                offsetY = (-0.0055f - dimensions.y) * 6;
            }
            if (number > 55 && number < 64)
            {
                offsetX = (0.002f + dimensions.x) * (number - 56);
                offsetY = (-0.0055f - dimensions.y) * 7;
            }
            if (number > 63 && number < 72)
            {
                offsetX = (0.002f + dimensions.x) * (number - 64);
                offsetY = (-0.0055f - dimensions.y) * 8;
            }
            if (number > 71 && number < 80)
            {
                offsetX = (0.002f + dimensions.x) * (number - 72);
                offsetY = (-0.0055f - dimensions.y) * 9;
            }
            if (number > 79 && number < 88)
            {
                offsetX = (0.002f + dimensions.x) * (number - 80);
                offsetY = (-0.0055f - dimensions.y) * 10;
            }
            if (number > 87 && number < 96)
            {
                offsetX = (0.002f + dimensions.x) * (number - 88);
                offsetY = (-0.0055f - dimensions.y) * 11;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }
        private void AddUIString(BasePlayer player, string name)
        {
            if (!OpenUI.ContainsKey(player.userID))
                OpenUI.Add(player.userID, new List<string>());
            OpenUI[player.userID].Add(name);
        }
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIMain);
            DestroyEntries(player);
        }
        private void DestroyEntries(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIPanel);
            if (OpenUI.ContainsKey(player.userID))
            {
                foreach (var entry in OpenUI[player.userID])
                    CuiHelper.DestroyUi(player, entry);
                OpenUI.Remove(player.userID);
            }
        }
        #endregion

        #region UI Commands
        [ConsoleCommand("QUI_AcceptStory")]
        private void cmdAcceptStory(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string questName = string.Join(" ", arg.Args);
            CheckPlayerEntry(player);
            var data = PlayerProgress[player.userID].Stories;
            if (!data.ContainsKey(questName))
            {
                Puts("questname nicht in Stories.");
                var type = GetStoryType(questName);
                Puts(type.ToString());
                if (type != null)
                {
                    Puts("Story nicht type null");
                    var quest = StoryDict[(StoryType)type][questName];
                    data.Add(questName, new PlayerStoryInfo { Status = StoryStatus.Pending, Type = (StoryType)type });
                    PlayerProgress[player.userID].RequiredItems.Add(new StoryInfo { ShortName = quest.Objective, Type = (StoryType)type });  

                    var vendorID = arg.Args[0];
                    var targetID = quest.Target.ID;
                    PlayerProgress[player.userID].CurrentStory = new ActiveStory { VendorID = vendorID, TargetID = targetID };

                    SavePlayerData();

                    PopupMessage(player, $"{LA("qAccep", player.UserIDString)} {questName}");
                    DestroyEntries(player);
                    DestroyUI(player);
                    return;
                }
            }
        }
        [ConsoleCommand("QUI_CancelStory")]
        private void cmdCancelStory(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!string.IsNullOrEmpty(PlayerProgress[player.userID].CurrentStory.TargetID))
            {
                PlayerProgress[player.userID].CurrentStory = new ActiveStory();
                DestroyUI(player);
                PopupMessage(player, LA("canConfStory", player.UserIDString));
            }
        }
        //TODO:
        [ConsoleCommand("QUI_FinishStory")]
        private void cmdFinishStory(ConsoleSystem.Arg arg)
        {
           /* var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            if (PlayerProgress[player.userID].CurrentStory != null && PlayerProgress[player.userID].CurrentStory.TargetID == arg.GetString(0))
            {
                var npcID = PlayerProgress[player.userID].CurrentStory.VendorID;
                var quest = vendors.StoryVendors[npcID];

                var reward = quest.Reward;
                reward.Amount = 1;
                if (GiveReward(player, new List<RewardItem> { reward }))
                {
                    var rewards = GetRewardString(new List<RewardItem> { reward });
                    PopupMessage(player, $"{LA("rewRec", player.UserIDString)} {rewards}");
                    PlayerProgress[player.userID].CurrentStory = new ActiveStory();
                }
                DestroyUI(player);
            }*/
        }
        [ConsoleCommand("QUI_ChangeElement")]
        private void cmdChangeElement(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CheckPlayerEntry(player);
            var panelName = arg.GetString(0);
            switch (panelName)
            {
                case "kill":
                    ListElement(player, StoryType.Kill);
                    return;
                case "gather":
                    ListElement(player, StoryType.Gather);
                    return;
                case "loot":
                    ListElement(player, StoryType.Loot);
                    return;
                case "craft":
                    ListElement(player, StoryType.Craft);
                    return;
                case "walk":
                    ListElement(player, StoryType.Walk);
                    return;
                case "personal":
                    PlayerStats(player);
                    return;
                case "editor":
                    if (player.IsAdmin)
                        DeletionEditMenu(player, LA("EDITOR", player.UserIDString), "QUI_EditQuest");
                    return;
                case "creation":
                    if (player.IsAdmin)
                    {
                        if (ActiveCreations.ContainsKey(player.userID))
                            ActiveCreations.Remove(player.userID);
                        CreationMenu(player);
                    }
                    return;
                case "objpage":
                    if (player.IsAdmin)
                    {
                        var pageNumber = arg.GetString(1);
                        CreateObjectiveMenu(player, int.Parse(pageNumber));
                    }
                    return;
                case "npcpage":
                    if (player.IsAdmin)
                    {
                        var pageNumber = arg.GetString(1);
                        bool vendorBool = Boolean.Parse(arg.GetString(2));
                        CreateNPCMenu(player, vendorBool, int.Parse(pageNumber));
                    }
                    return;
                case "questpage":
                    if (player.IsAdmin)
                    {
                        var pageNumber = arg.GetString(1);
                        CreateQuestMenu(player, int.Parse(pageNumber));
                    }
                    return;
                case "listpage":
                    {
                        var pageNumber = arg.GetString(2);
                        var type = ConvertStringToType(arg.GetString(1));
                        ListElement(player, type, int.Parse(pageNumber));
                    }
                    return;
                case "statspage":
                    {
                        var pageNumber = arg.GetString(1);
                        PlayerStats(player, int.Parse(pageNumber));
                    }
                    return;
            }
        }
        [ConsoleCommand("QUI_DestroyAll")]
        private void cmdDestroyAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (StatsMenu.Contains(player.userID))
                StatsMenu.Remove(player.userID);
            if (ActiveCreations.ContainsKey(player.userID))
                ActiveCreations.Remove(player.userID);
            if (ActiveEditors.ContainsKey(player.userID))
                ActiveEditors.Remove(player.userID);
            if (OpenMenuBind.Contains(player.userID))
                OpenMenuBind.Remove(player.userID);
            DestroyUI(player);
        }
        [ConsoleCommand("QUI_NewQuest")]
        private void cmdNewQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                var questType = arg.GetString(0);
                var Type = ConvertStringToType(questType);
                ActiveCreations.Add(player.userID, new StoryCreator { type = Type, entry = new StoryEntry { Rewards = new List<RewardItem>() }, item = new RewardItem() });
                DestroyUI(player);
                StoryHelp(player);
            }
        }
        [ConsoleCommand("QUI_SelectReqQuest")]
        private void cmdSelectReqQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            try
            {
                if (player.IsAdmin)
                {
                    var selQuest = arg.GetString(0);

                    StoryCreator Creator;
                    bool isEditing = false;
                    bool isCreating = false;

                    if (ActiveCreations.ContainsKey(player.userID))
                    {
                        isCreating = true;
                        Creator = ActiveCreations[player.userID];
                    }
                    else
                    {
                        isEditing = true;
                        Creator = ActiveEditors[player.userID];
                    }

                    if(selQuest != "None")
                    {
                        Creator.entry.RequiredQuest = selQuest;
                    }
                    else
                    {
                        Creator.entry.RequiredQuest = null;
                        Puts("Quest none");
                    }

                    Creator.partNum++;
                    DestroyUI(player);

                    if (isCreating)
                    {
                        StoryHelp(player, 13);
                    }
                    else if (isEditing)
                    {
                        StoryHelp(player, 6);
                    }
                }
            }
            catch (Exception e)
            {
                Puts("Error in cmdSelectNpc: " + e.ToString());
            }
        }
        [ConsoleCommand("QUI_SelectNpc")]
        private void cmdSelectNpc(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            try
            {
                if (player.IsAdmin)
                {
                    var selNPC = arg.GetString(0); // NPC ID

                    bool type = Boolean.Parse(arg.GetString(1)); // vendor or target     
                    StoryCreator Creator;
                    bool isEditing = false;
                    bool isCreating = false;

                    ulong npcPlayerID;

                    if (!ulong.TryParse(selNPC, out npcPlayerID)) return;

                    BasePlayer npcPlayer = FindPlayerByID(npcPlayerID);

                    if (ActiveCreations.ContainsKey(player.userID))
                    {
                        isCreating = true;
                        Creator = ActiveCreations[player.userID];
                    }
                    else
                    {
                        isEditing = true;
                        Creator = ActiveEditors[player.userID];
                    }
           
                    switch (type)
                    {
                        case true:
                            Creator.entry.Vendor = new NPCInfo();
                            Creator.entry.Vendor.ID = selNPC;
                            Creator.entry.Vendor.Name = npcPlayer.displayName;
                            Creator.entry.Vendor.x = npcPlayer.transform.position.x;
                            Creator.entry.Vendor.z = npcPlayer.transform.position.z;
                            break;
                        case false:
                            Creator.entry.Target = new NPCInfo();
                            Creator.entry.Target.ID = selNPC;
                            Creator.entry.Target.Name = npcPlayer.displayName;
                            Creator.entry.Target.x = npcPlayer.transform.position.x;
                            Creator.entry.Target.z = npcPlayer.transform.position.z;
                            break;
                        default:
                            break;
                    }
           
                    Creator.partNum++;
                    DestroyUI(player);

                    if (isCreating)
                    {
                        if (type) // if vendor
                        {
                            StoryHelp(player, 11);
                        }
                        else if (!type)
                        {
                            StoryHelp(player, 12);
                        }               
                    }
                    else if (isEditing)
                    {
                        StoryHelp(player, 6);
                    }

                    Puts(Creator.partNum.ToString());
                }
            }
            catch(Exception e)
            {
                Puts("Error in cmdSelectNpc: " + e.ToString());
            }
        }
        [ConsoleCommand("QUI_SelectObj")]
        private void cmdSelectObj(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                var questItem = string.Join(" ", arg.Args);
                StoryCreator Creator;
                if (ActiveCreations.ContainsKey(player.userID))
                    Creator = ActiveCreations[player.userID];
                else Creator = ActiveEditors[player.userID];

                Creator.entry.Objective = questItem;
                if (DisplayNames.ContainsKey(questItem))
                    Creator.entry.ObjectiveName = DisplayNames[questItem];
                else
                    Creator.entry.ObjectiveName = questItem;

                Creator.partNum++;
                DestroyUI(player);

                StoryHelp(player, 2); // amount etc.
                
            }
        }
        [ConsoleCommand("QUI_RewardType")]
        private void cmdRewardType(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                var rewardType = arg.GetString(0);
                StoryCreator Creator;

                if (ActiveCreations.ContainsKey(player.userID))
                    Creator = ActiveCreations[player.userID];
                else Creator = ActiveEditors[player.userID];

                bool isRP = false;
                string name = "";

                switch (rewardType)
                {
                    case "rp":
                        isRP = true;
                        name = LA("RP", player.UserIDString);
                        break;
                    default:
                        break;
                }
                Creator.partNum = 5;

                Creator.item.isRP = isRP;
                Creator.item.DisplayName = name;
                StoryHelp(player, 5);
            }
        }
        [ConsoleCommand("QUI_ClaimReward")]
        private void cmdClaimReward(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            var questName = string.Join(" ", arg.Args);
            var quest = GetStory(questName);
            if (quest == null) return;

            if (IsStoryCompleted(player.userID, questName))
            {
                if (GiveReward(player, quest.Rewards))
                {
                    var rewards = GetRewardString(quest.Rewards);
                    PopupMessage(player, $"{LA("rewRec", player.UserIDString)} {rewards}");
                    PlayerProgress[player.userID].Stories[questName].RewardClaimed = true;
                    PlayerProgress[player.userID].DoneStories.Add(questName); // add done stories to Database
                }
                else
                {
                    PopupMessage(player, LA("rewError", player.UserIDString));
                }
            }
            PlayerStats(player);
        }
        bool IsStoryCompleted(ulong playerId, string questName = "") => !string.IsNullOrEmpty(questName) && PlayerProgress[playerId].Stories[questName].Status == StoryStatus.Completed;

        [ConsoleCommand("QUI_CancelQuest")]
        private void cmdCancelQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var questName = string.Join(" ", arg.Args);
            DestroyUI(player);
            ConfirmCancellation(player, questName);
        }
        [ConsoleCommand("QUI_ItemDeduction")]
        private void cmdItemDeduction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                StoryCreator Creator;
                if (ActiveCreations.ContainsKey(player.userID))
                    Creator = ActiveCreations[player.userID];
                else Creator = ActiveEditors[player.userID];
                switch (arg.Args[0])
                {
                    case "0":
                        Creator.entry.ItemDeduction = false;
                        break;
                    default:
                        Creator.entry.ItemDeduction = true;
                        break;
                }
                StoryHelp(player, 9);
            }
        }
        [ConsoleCommand("QUI_ConfirmCancel")]
        private void cmdConfirmCancel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var questName = string.Join(" ", arg.Args);
            if (questName.Contains("reject"))
            {
                DestroyUI(player);
                if (StatsMenu.Contains(player.userID))
                    CreateEmptyMenu(player);
                else CreateMenu(player);
                PlayerStats(player);
                return;
            }
            var quest = GetStory(questName);
            if (quest == null) return;
            var info = PlayerProgress[player.userID];
            var items = info.RequiredItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ShortName == questName && items[i].Type == info.Stories[questName].Type)
                {
                    items.Remove(items[i]);
                    break;
                }
            }
            var type = (StoryType)GetStoryType(questName);
            if (type != StoryType.Walk && type != StoryType.Kill)
            {
                string questitem = quest.Objective;
                int amount = info.Stories[questName].AmountCollected;
                if (quest.ItemDeduction)
                    ReturnItems(player, questitem, amount);
            }
            PlayerProgress[player.userID].Stories.Remove(questName);

            if (StatsMenu.Contains(player.userID))
                CreateEmptyMenu(player);
            else CreateMenu(player);

            PlayerStats(player);
        }
        [ConsoleCommand("QUI_RemoveCompleted")]
        private void cmdRemoveCompleted(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var questName = string.Join(" ", arg.Args);
            var quest = GetStory(questName);
            if (quest == null) return;
            var info = PlayerProgress[player.userID];
            var items = info.RequiredItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ShortName == questName && items[i].Type == info.Stories[questName].Type)
                {
                    items.Remove(items[i]);
                    break;
                }
            }
            PlayerProgress[player.userID].Stories.Remove(questName);
            PlayerStats(player);
        }
        [ConsoleCommand("QUI_DeleteQuest")]
        private void cmdDeleteQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    DeletionEditMenu(player, LA("REMOVER", player.UserIDString), "QUI_ConfirmDelete");
                    return;
                }
                if (arg.Args.Length == 1 && arg.Args[0] == "reject")
                {
                    DestroyUI(player);
                    CreateMenu(player);
                    DeletionEditMenu(player, LA("REMOVER", player.UserIDString), "QUI_ConfirmDelete");
                    return;
                }
                var questName = string.Join(" ", arg.Args);
                RemoveStory(questName);
                DestroyUI(player);
                CreateMenu(player);
                DeletionEditMenu(player, LA("REMOVER", player.UserIDString), "QUI_ConfirmDelete");
            }
        }
        [ConsoleCommand("QUI_DeleteNPCMenu")]
        private void cmdDeleteNPCMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                DeleteNPCMenu(player);
            }
        }
        [ConsoleCommand("QUI_RemoveVendor")]
        private void cmdRemoveVendor(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                var ID = arg.Args[0];
                foreach (var npc in vendors.StoryVendors)
                {
                    if (npc.Key == ID)
                    {
                        RemoveVendor(player, ID, true);
                        return;
                    }
                }
            }
        }
        [ConsoleCommand("QUI_ConfirmDelete")]
        private void cmdConfirmDelete(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                var questName = string.Join(" ", arg.Args);
                DestroyUI(player);
                ConfirmDeletion(player, questName);
            }
        }
        [ConsoleCommand("QUI_EditQuest")]
        private void cmdEditQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                if (ActiveEditors.ContainsKey(player.userID))
                    ActiveEditors.Remove(player.userID);
                ActiveEditors.Add(player.userID, new StoryCreator());

                var questName = string.Join(" ", arg.Args);
                var Quest = GetStory(questName);
                if (Quest == null) return;
                ActiveEditors[player.userID].entry = Quest;
                ActiveEditors[player.userID].oldEntry = Quest.StoryName;
                ActiveEditors[player.userID].type = (StoryType)GetStoryType(questName);
                ActiveEditors[player.userID].item = new RewardItem();
                StoryEditorMenu(player);
            }
        }
        [ConsoleCommand("QUI_EditQuestVar")]
        private void cmdEditQuestVar(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                if (ActiveEditors.ContainsKey(player.userID))
                {
                    var Creator = ActiveEditors[player.userID];

                    DestroyUI(player);
                    switch (arg.Args[0].ToLower())
                    {
                        case "name":
                            StoryHelp(player, 0);
                            break;
                        case "description":
                            Creator.partNum = 3;
                            StoryHelp(player, 3);
                            break;
                        case "objective":
                            Creator.partNum = 1;
                            StoryHelp(player, 1);
                            break;
                        case "amount":
                            Creator.partNum = 2;
                            StoryHelp(player, 2);
                            break;
                        case "reward":
                            Creator.partNum = 4;
                            StoryHelp(player, 10);
                            break;
                        case "startnpc":
                            Creator.partNum = 9;
                            StoryHelp(player, 9);
                            break;
                        case "targetnpc":
                            Creator.partNum = 11;
                            StoryHelp(player, 11);
                            break;
                        case "reqquest":
                            Creator.partNum = 12;
                            StoryHelp(player, 12);
                            break;
                        case "storyline":
                            Creator.partNum = 13;
                            StoryHelp(player, 13);
                            break;
                        case "storynumber":
                            Creator.partNum = 14;
                            StoryHelp(player, 14);
                            break;
                        default:
                            return;
                    }
                }
            }
        }
        [ConsoleCommand("QUI_RemoveReward")]
        private void cmdEditReward(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                StoryCreator Creator = ActiveEditors[player.userID];
                var amount = arg.Args[0];
                var dispName = arg.Args[1];
                foreach (var entry in Creator.entry.Rewards)
                {
                    if (entry.Amount == float.Parse(amount) && entry.DisplayName == dispName)
                    {
                        Creator.entry.Rewards.Remove(entry);
                        break;
                    }
                }
                SaveRewardsEdit(player);
            }
        }
        [ConsoleCommand("QUI_EndEditing")]
        private void cmdEndEditing(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                CreateMenu(player);
                DeletionEditMenu(player, LA("EDITOR", player.UserIDString), "QUI_EditQuest");
            }
        }
        [ConsoleCommand("QUI_SaveQuest")]
        private void cmdSaveQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                bool creating = false;
                if (ActiveCreations.ContainsKey(player.userID))
                    creating = true;
                SaveStory(player, creating);
            }
        }
        [ConsoleCommand("QUI_ExitQuest")]
        private void cmdExitQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                bool creating = false;
                if (ActiveCreations.ContainsKey(player.userID))
                    creating = true;
                ExitStory(player, creating);
            }
        }
        [ConsoleCommand("QUI_AddReward")]
        private void cmdAddReward(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (player.IsAdmin)
            {
                StoryCreator Creator;
                if (ActiveCreations.ContainsKey(player.userID))
                    Creator = ActiveCreations[player.userID];
                else Creator = ActiveEditors[player.userID];
                Creator.partNum = 4;
                StoryHelp(player, 4);
            }
        }
        [ConsoleCommand("QUI_RewardFinish")]
        private void cmdFinishReward(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            StoryCreator Creator = new StoryCreator();
            if (ActiveCreations.ContainsKey(player.userID))
            {
                Creator = ActiveCreations[player.userID];
            }
            else if (ActiveEditors.ContainsKey(player.userID))
            {
                Creator = ActiveEditors[player.userID];
            }

            if (player.IsAdmin)
            {
                if (Creator.type == StoryType.Walk)
                {
                    Creator.partNum = 9;
                    StoryHelp(player, 9);// skip deduction, npc choose
                }
                else
                {
                    StoryHelp(player, 8);
                }
            }
        }
        [ConsoleCommand("QUI_OpenQuestMenu")]
        private void cmdOpenQuestMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!OpenMenuBind.Contains(player.userID))
            {
                cmdOpenMenu(player, "q", new string[0]);
                OpenMenuBind.Add(player.userID);
            }
        }
        #endregion

        #region Chat Commands
        [ChatCommand("q")]
        void cmdOpenMenu(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                CheckPlayerEntry(player);
                CreateMenu(player);
                return;
            }
            else
            {
                CheckPlayerEntry(player);
                if (!StatsMenu.Contains(player.userID))
                    StatsMenu.Add(player.userID);

                CreateEmptyMenu(player);
                PlayerStats(player);
                PopupMessage(player, LA("noVendor", player.UserIDString));
            }
        }
        //TODO:
        [ChatCommand("questnpc")]
        void cmdQuestNPC(BasePlayer player, string command, string[] args)
        {
            /*if (!player.IsAdmin) return;
            var NPC = FindEntity(player);
            if (NPC != null)
            {
                var isRegistered = isNPCRegistered(NPC.UserIDString);
                if (!string.IsNullOrEmpty(isRegistered))
                {
                    SendMSG(player, isRegistered, LA("Quest NPCs:", player.UserIDString));
                    return;
                }
                string name = "";
                if (args.Length >= 1)
                    name = string.Join(" ", args);


                bool isEditing = false;
                bool isCreating = false;
                StoryCreator Creator = new StoryCreator();
                StoryEntry Quest = new StoryEntry();

                if (ActiveEditors.ContainsKey(player.userID))
                {
                    isEditing = true;
                    Creator = ActiveEditors[player.userID];
                    Quest = Creator.entry;
                }
                else if (ActiveCreations.ContainsKey(player.userID))
                {
                    isCreating = true;
                    Creator = ActiveCreations[player.userID];
                    Quest = Creator.entry;
                }
                if (!isEditing && !isCreating)
                    return;

                var pos = new NPCInfo { x = NPC.transform.position.x, z = NPC.transform.position.z, ID = NPC.UserIDString };

                Puts("cmdQuestNPC oben drin.");

                if (AddVendor.ContainsKey((1 + player.userID)))
                {

                    Puts("cmdQuestNPC mit Story funzt.");

                    if (string.IsNullOrEmpty(name))
                        name = $"Story_{ vendors.StoryVendors.Count + 1}";

                    if (ActiveCreations.ContainsKey(player.userID))
                        ActiveCreations.Remove(player.userID);
                    pos.Name = name;

                    ActiveCreations.Add(player.userID, new QuestCreator
                    {
                        storyInfo = new StoryInfo
                        {
                            Info = pos,
                            Reward = new RewardItem()
                        },
                        partNum = 4,
                        type = QuestType.Story
                    });
                    StoryHelp(player, 2);
                    return;
                }


                if (AddVendor[player.userID])
                {
                    pos.Name = $"QuestVendor_{vendors.QuestVendors.Count + 1}";
                    vendors.QuestVendors.Add(NPC.UserIDString, pos);
                    SendMSG(player, LA("newVSucc", player.UserIDString), LA("Quest NPCs:", player.UserIDString));
                    if (NPC != null)
                    {
                        NPC.displayName = pos.Name;
                        NPC.UpdateNetworkGroup();
                    }
                    AddVendor.Remove(player.userID);
                    SaveVendorData();
                    DestroyUI(player);
                    OpenMap(player);
                    return;
                }
                else
                {
                    if (string.IsNullOrEmpty(name))
                        name = $"Delivery_{ vendors.DeliveryVendors.Count + 1}";

                    if (ActiveCreations.ContainsKey(player.userID))
                        ActiveCreations.Remove(player.userID);
                    pos.Name = name;

                    ActiveCreations.Add(player.userID, new QuestCreator
                    {
                        deliveryInfo = new DeliveryInfo
                        {
                            Info = pos,
                            Reward = new RewardItem()
                        },
                        partNum = 4,
                        type = QuestType.Delivery
                    });
                    DeliveryHelp(player, 2);
                }
            }
            else SendMSG(player, LA("noNPC", player.UserIDString));
            */
        }
        #endregion

        #region Data Management
        void SaveStoryData()
        {
            storyData.StoryDict = StoryDict;
            Story_Data.WriteObject(storyData);
        }
        void SaveVendorData()
        {
            Story_Vendors.WriteObject(vendors);
        }
        void SavePlayerData()
        {
            playerData.PlayerProgress = PlayerProgress;
            Player_Data.WriteObject(playerData);
        }
        void SaveDisplayNames()
        {
            itemNames.DisplayNames = DisplayNames;
            Item_Names.WriteObject(itemNames);
        }
        private void SaveLoop()
        {
            SavePlayerData();
            timer.Once(900, () => SaveLoop());
        }
        void LoadData()
        {
            try
            {
                storyData = Story_Data.ReadObject<StoryData>();
                StoryDict = storyData.StoryDict;
            }
            catch
            {
                Puts("Couldn't load story data, creating new datafile");
                storyData = new StoryData();
            }
            try
            {
                vendors = Story_Vendors.ReadObject<NPCData>();
            }
            catch
            {
                Puts("Couldn't load story vendor data, creating new datafile");
                vendors = new NPCData();
            }
            try
            {
                playerData = Player_Data.ReadObject<PlayerData>();
                PlayerProgress = playerData.PlayerProgress;
            }
            catch
            {
                Puts("Couldn't load player data, creating new datafile");
                playerData = new PlayerData();
                PlayerProgress = new Dictionary<ulong, PlayerStoryData>();
            }
            try
            {
                itemNames = Item_Names.ReadObject<ItemNames>();
            }
            catch
            {
                Puts("Couldn't load item display name data, creating new datafile");
                itemNames = new ItemNames();
            }
        }
        #endregion

        #region Data Storage
        class StoryData
        {
            public Dictionary<StoryType, Dictionary<string, StoryEntry>> StoryDict = new Dictionary<StoryType, Dictionary<string, StoryEntry>>
            {
                {StoryType.Craft, new Dictionary<string, StoryEntry>() },
                {StoryType.Walk, new Dictionary<string, StoryEntry>() },
                {StoryType.Gather, new Dictionary<string, StoryEntry>() },
                {StoryType.Kill, new Dictionary<string, StoryEntry>() },
                {StoryType.Loot, new Dictionary<string, StoryEntry>() }
            };
        }
        class PlayerData
        {
            public Dictionary<ulong, PlayerStoryData> PlayerProgress = new Dictionary<ulong, PlayerStoryData>();
        }
        class NPCData
        {
            public Dictionary<string, NPCInfo> StoryVendors = new Dictionary<string, NPCInfo>();
        }
        #endregion

        #region Config
        class UIColor
        {
            public string Color { get; set; }
            public float Alpha { get; set; }
        }
        class Colors
        {
            public string TextColor_Primary { get; set; }
            public string TextColor_Secondary { get; set; }
            public UIColor Background_Dark { get; set; }
            public UIColor Background_Light { get; set; }
            public UIColor Button_Standard { get; set; }
            public UIColor Button_Accept { get; set; }
            public UIColor Button_Completed { get; set; }
            public UIColor Button_Cancel { get; set; }
            public UIColor Button_Pending { get; set; }
        }
        class Keybinds
        {
            public bool Autoset_KeyBind { get; set; }
            public string KeyBind_Key { get; set; }
        }
        class ConfigData
        {
            public Colors Colors { get; set; }
            public Keybinds KeybindOptions { get; set; }
            public bool DisableUI_FadeIn { get; set; }

        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            ConfigData config = new ConfigData
            {
                DisableUI_FadeIn = false,

                Colors = new Colors
                {
                    Background_Dark = new UIColor { Color = "#2a2a2a", Alpha = 0.98f },
                    Background_Light = new UIColor { Color = "#696969", Alpha = 0.3f },
                    Button_Accept = new UIColor { Color = "#00cd00", Alpha = 0.9f },
                    Button_Cancel = new UIColor { Color = "#8c1919", Alpha = 0.9f },
                    Button_Completed = new UIColor { Color = "#829db4", Alpha = 0.9f },
                    Button_Pending = new UIColor { Color = "#a8a8a8", Alpha = 0.9f },
                    Button_Standard = new UIColor { Color = "#2a2a2a", Alpha = 0.9f },
                    TextColor_Primary = "#ce422b",
                    TextColor_Secondary = "#939393"
                },
                KeybindOptions = new Keybinds
                {
                    Autoset_KeyBind = false,
                    KeyBind_Key = "k"
                }
            };
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Messaging
        void SendMSG(BasePlayer player, string message, string keyword = "")
        {
            message = $"{textSecondary}{message}</color>";
            if (!string.IsNullOrEmpty(keyword))
                message = $"{textPrimary}{keyword}</color> {message}";
            SendReply(player, message);
        }
        Dictionary<string, string> Localization = new Dictionary<string, string>
        {
            { "Quests", "Quests:" },
            { "Story", "Story:" },
            { "StoryMenu", "Story" },
            { "storyInprog", "You already have a story mission in progress." },
            { "delInprog", "You already have a delivery mission in progress." },
            { "QC", "Quest Creator:" },
            { "noAItem", "Unable to find a active item. Place the item in your hands then type " },
            { "nameExists", "A quest with this name already exists" },
            { "numberExists", "A quest with this number already exists" },
            { "objAmount", "You need to enter a objective amount" },
            { "slNumber", "You need to enter a storyline number" },
            { "sNumber", "You need to enter a story number" },
            { "OA", "Objective Amount:" },
            { "SL", "Storyline:" },
            { "SN", "Story number:" },
            { "Desc", "Description:" },
            { "noRM", "You need to enter a reward multiplier" },
            { "RM", "Reward Multiplier:" },
            { "noRA", "You need to enter a reward amount" },
            { "RA", "Reward Amount:" },
            { "noCD", "You need to enter a cooldown amount" },
            { "CD1", "Cooldown Timer (minutes):" },
            { "qComple", "You have completed the quest" },
            { "claRew", "You can claim your reward from the quest menu." },
            { "qCancel", "You have cancelled this quest." },
            { "rewRet", "has been returned to you" },
            { "minDV", "Delivery missions require atleast 2 vendors. Add some more vendors to activate delivery missions" },
            { "SVSucc", "You have successfully added a new story vendor" },
            { "DVSucc", "You have successfully added a new delivery vendor" },
            { "saveQ", "You have successfully saved the quest:" },
            { "QCCancel", "You have cancelled quest creation" },
            { "KillOBJ", "Kill quests require you to kill 'X' amount of the target objective" },
            { "CraftOBJ", "Crafting quests require you to craft 'X' amount of the objective item" },
            { "GatherOBJ", "Gather quests require you to gather 'X' amount of the objective from resources" },
            { "LootOBJ", "Loot quests require you to collect 'X' amount of the objective item from containers" },
            { "WalkOBJ", "Walk quests require you to walk to the target NPC" },
            { "StoryOBJ", "Story quests require you to do the objective" },
            { "DelvOBJ", "Delivery quests require you to deliver a package from one vendor to another" },
            { "aQVReg", "This NPC is already a registered Quest vendor" },
            { "aSVReg", "This NPC is already a registed story vendor" },
            { "aDVReg", "This NPC is already a registed Delivery vendor" },
            { "Kill", "Kill" },
            { "Gather", "Gather" },
            { "Craft", "Craft" },
            { "Loot", "Loot" },
            { "Delivery", "Delivery" },
            { "Your Quests", "Your Quests" },
            { "Create Quest", "Create Quest" },
            { "Edit Quest", "Edit Quest" },
            { "Delete Quest", "Delete Quest" },
            { "Close", "Close" },
            { "Next", "Next" },
            { "Back", "Back" },
            { "noQ", "The are currently no" },
            { "quests", "quests" },
            { "Pending", "Pending" },
            { "Completed", "Completed" },
            { "Accept Quest", "Accept Quest" },
            { "Status:", "Status:" },
            { "Description:", "Description:" },
            { "Amount Required:", "Amount Required:" },
            { "Reward:", "Reward:" },
            { "yqDesc", "Check your current progress for each quest" },
            { "STATS", "STATS" },
            { "noQDSaved", "You don't have any quest data saved" },
            { "Cancel Quest", "Cancel Quest" },
            { "Claim Reward", "Claim Reward" },
            { "Remove", "Remove" },
            { "Cooldown", "Cooldown" },
            { "Collected:", "Collected:" },
            { "Reward Claimed:", "Reward Claimed:" },
            { "STORY", "STORY" },
            { "DELIVERY", "DELIVERY" },
            { "noASM", "You do not have a active story mission" },
            { "noADM", "You do not have a active delivery mission" },
            { "Destination:", "Destination:" },
            { "Distance:", "Distance:" },
            { "Cancel", "Cancel" },
            { "selCreat", "Select a quest type to begin creation" },
            { "CREATOR", "CREATOR" },
            { "creHelMen", "This is the quest creation help menu" },
            { "creHelFol", "Follow the instructions given by typing in chat" },
            { "creHelExi", "You can exit quest creation at any time by typing" },
            { "creHelName", "To proceed enter the name of your new quest!" },
            { "creHelObj", "Choose a quest objective from the list" },
            { "creHelRA", "Enter a required amount" },
            { "creHelQD", "Enter a quest description" },
            { "creHelRT", "Choose a reward type" },
            { "creHelNewRew", "Select a reward to remove, or add a new one" },
            { "Coins", "Coins" },
            { "RP", "RP" },
            { "HuntXP", "XP" },
            { "Item", "Item" },
            { "creHelRewA", "Enter a reward amount" },
            { "creHelIH", "Place the item you want to issue as a reward in your hands and type" },
            { "creHelAR", "Would you like to add additional rewards?" },
            { "Yes", "Yes" },
            { "No", "No" },
            { "creHelID", "Would you like to enable item deduction (take items from player when collected)?" },
            { "creHelCD", "Enter a cooldown time (in minutes)" },
            { "creHelSQ", "You have successfully created a new quest. To confirm click 'Save Quest'" },
            { "Save Quest", "Save Quest" },
            { "Name:", "Name:" },
            { "Objective:", "Objective:" },
            { "CDMin", "Cooldown (minutes):" },
            { "Quest Type:", "Quest Type:" },
            { "Required Amount:", "Required Amount:" },
            { "Item Deduction:", "Item Deduction:" },
            { "storyHelMen", "Here you can start adding story missions." },
            { "delHelMen", "Here you can add delivery missions and Quest vendors." },
            { "storyHelChoo", "Einfach test....123" },
            { "delHelChoo", "Choose either a Delivery vendor (delivery mission) or a Quest vendor (npc based quest menu)" },
            { "Story Quest", "Story Quest" },
            { "Quest Vendor", "Quest Vendor" },
            { "Delivery Vendor", "Delivery Vendor" },
            { "storyHelNewNPC", "Stand infront of the NPC you wish to add the story and type" },
            { "delHelNewNPC_vendor", "Choose the start NPC from the list" },
            { "delHelNewNPC_target", "Choose the target NPC from the list" },
            { "delHelReqQuest", "Choose the quest which is required to accept this quest" },
            { "delHelStoryLine", "Type the number of the storyline you want to give to this quest" },
            { "delHelStoryNumber", "Type the number of the story you want to give to this quest" },
            { "delHelMult", "Delivery mission rewards are based on distance X a multiplier. Keep this in mind when selecting a reward." },
            { "delHelRM", "Enter a reward multiplier (per unit)." },
            { "delHelRM1", "For example, if a delivery is" },
            { "delHelRM2", "away, and the multiplier is" },
            { "delHelRM3", "the total reward amount would be" },
            { "delHelDD", "Enter a delivery description." },
            { "delHelNewV", "You have successfully added a new delivery vendor. To confirm click 'Save Quest'" },
            { "Accept", "Accept" },
            { "Decline", "Decline" },
            { "Claim", "Claim" },
            { "delComplMSGStory", "You have successfully completed the mission" },
            { "delComplMSG", "Thanks for making the delivery" },
            { "Delete NPC", "Delete NPC" },
            { "REMOVER", "REMOVER" },
            { "Delivery Vendors", "Delivery Vendors" },
            { "Quest Vendors", "Quest Vendors" },
            { "confDel", "Are you sure you want to delete:" },
            { "confCan", "Are you sure you want to cancel:" },
            { "confCan2", "Any progress you have made will be lost!" },
            { "EDITOR", "EDITOR" },
            { "chaEdi", "Select a value to change" },
            { "Name", "Name" },
            { "Description", "Description" },
            { "Objective", "Objective" },
            { "Amount", "Amount" },
            { "Reward", "Reward" },
            { "qAccep", "You have accepted the quest" },
            { "storyAccep", "You have accepted the story mission" },
            { "dAccep", "You have accepted the delivery mission" },
            { "canConfStory", "You have cancelled the story mission" },
            { "canConf", "You have cancelled the delivery mission" },
            { "rewRec", "You have recieved" },
            { "rewError", "Unable to issue your reward. Please contact an administrator" },
            { "Quest NPCs:", "Quest NPCs:" },
            { "newVSucc", "You have successfully added a new Quest vendor" },
            { "noNPC", "Unable to find a valid NPC" },
            { "addNewRew", "Add Reward" },
            { "NoTP", "You cannot teleport while you are on a delivery mission" },
            { "noVendor", "To accept new Quests you must find a Story Vendor" },
        };
        #endregion
    }
}
