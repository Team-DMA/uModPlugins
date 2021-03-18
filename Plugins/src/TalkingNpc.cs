using System;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using VLB;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Text.RegularExpressions;
using Color = UnityEngine.Color;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TalkingNpc", "Ts3Hosting", "1.0.12")]
    [Description("domination game play")]
    public class TalkingNpc : RustPlugin
    {	
	    [PluginReference]
        private Plugin SpawnModularCar, ServerRewards, Economics, VehicleLicence;
		
		public static TalkingNpc plugin;
		private string LayerMain = "Layer_Main";
		private string LayerMainBox = "Layer_MainBox";
        private string Layer_Words = "Layer_Words";
		private string Layer_Message = "Layer_Message";
	    private string Layer_Choices = "Layer_Choices";
		NpcPoshions npcData;
        private DynamicConfigFile NPCDATA;
		NpcCooldown cooldownData;
        private DynamicConfigFile PLAYERDATA;
		
		readonly Dictionary<ulong, Timer> pTimers = new Dictionary<ulong, Timer>();
        public Dictionary<ulong, string> playerToNpc = new Dictionary<ulong, string>();		

	[ConsoleCommand("carspawns55912")]
    void spawnCarShort(ConsoleSystem.Arg arg)
	{
		BasePlayer player = arg.Player();
		string TheConfig = arg.Args[0];
		if (configData.settings.carData.ContainsKey(TheConfig))
		{
			/*SpawnModularCar.Call("API_SpawnPresetCar", player,
				new Dictionary<string, object>
				{
					["CodeLock"] = configData.settings.carData[TheConfig].CodeLock,
					["KeyLock"] = configData.settings.carData[TheConfig].KeyLock,
					["EnginePartsTier"] = configData.settings.carData[TheConfig].EnginePartsTier,
					["FreshWaterAmount"] = configData.settings.carData[TheConfig].FreshWaterAmount,
					["FuelAmount"] = configData.settings.carData[TheConfig].FuelAmount,
					["Modules"] = configData.settings.carData[TheConfig].Modules
				},
				new Action<ModularCar>(car =>
				{
					if (configData.settings.carData[TheConfig].Position != Vector3.zero)
					{
						car.transform.position = configData.settings.carData[TheConfig].Position;
						car.transform.rotation = Quaternion.Euler(configData.settings.carData[TheConfig].Rotation);
					}
				}
			));*/



			//API from Vehicle Licenses
			switch (TheConfig)
			{
				case "small":
                {
					VehicleLicence?.Call("HandleBuyCmd", player, "small", true);
					return;
                }
				case "medium":
				{
					VehicleLicence?.Call("HandleBuyCmd", player, "medium", true);
					return;
				}
				case "long":
				{
					VehicleLicence?.Call("HandleBuyCmd", player, "large", true);
					return;
				}
				default:
					break;
			}
		}
			
	}
		
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Blocked"] = "<color=#ce422b>You can not use this command</color>",
				["cooldown"] = "Come back in",
            }, this);
        }

        #region Config 
	
	    public class spawnCar
		{
			public Vector3 Position = Vector3.zero;
			public Vector3 Rotation = Vector3.zero;
			public bool CodeLock = true;
            public bool KeyLock = false;
            public int EnginePartsTier = 3;
            public int FreshWaterAmount = 0;
            public int FuelAmount = 500;
            public string[] Modules = new string[]
		    {
                "vehicle.1mod.cockpit.with.engine",
                "vehicle.1mod.flatbed"
            };
		}
		
        private ConfigData configData;
        class ConfigData
        {

            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }
			
            public class Settings
            {
				public string PermissionUse { get; set; }
				public Dictionary<string, spawnCar> carData { get; set; }
			}					
           
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        protected override void LoadConfig()
        {
			plugin = this;
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                settings = new ConfigData.Settings
                {
	
					PermissionUse = "talkingnpc.admin",
					carData = new Dictionary<string, spawnCar>()	
				},	 			 
							
					Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
			
        }
        #endregion		

		#region Data
		
        void GetTheNpcDefaultFile()
        {
			if (HasSaveFile("default")) return;
			talkingNpc SaveFile = null;
			if (!HasSaveFile("default"))
            {
                SaveFile = new talkingNpc();
                //	SaveFilePlayer.CarData.Add(player.userID, new TheData());
                SavePlayData(SaveFile, Name + "/NpcInfo/" + "default");
            }
            else LoadData(out SaveFile, Name + "/NpcInfo/" + "default");
			
			SaveFile.theConfig.Add("Messages", new talkChoice());
			SaveFile.theConfig["Messages"].theChoices.Add(0, new talkMessage());
			SaveFile.theConfig["Messages"].theChoices[0].message = "Welcome to my shop wander. What would you like?";			
			SaveFile.theConfig["Messages"].theChoices[0].reply = new List<talkReply> { new talkReply { message = "I would like to buy a car.", command = "continue", choiceConfig = 1 },
			new talkReply { message = "I am just browsing.", command = "close", choiceConfig = 0 }
			};
			
			SaveFile.theConfig["Messages"].theChoices.Add(1, new talkMessage());
			SaveFile.theConfig["Messages"].theChoices[1].message = "A ride you say. Witch style ride would you like?";		
			SaveFile.theConfig["Messages"].theChoices[1].reply = new List<talkReply> { 
			new talkReply { message = "I would like a short car.", command = "continue", choiceConfig = 2 },
			new talkReply { message = "I would like a medium car.", command = "continue", choiceConfig = 3 },
			new talkReply { message = "I would like a long car.", command = "continue", choiceConfig = 4 },
			new talkReply { message = "I am just browsing.", command = "close", choiceConfig = 1, goodbye = ""  }
			};
			
			SaveFile.theConfig["Messages"].theChoices.Add(2, new talkMessage());
			SaveFile.theConfig["Messages"].theChoices[2].message = "That is a nice choice. Do you want to buy this ride?";		
			SaveFile.theConfig["Messages"].theChoices[2].reply = new List<talkReply>	{ 
			new talkReply { message = "Here is my 250 scrap.", command = "SendAsPlayer carspawns55912 small", choiceConfig = 2, price = 250, goodbye = "Go get your ride before someone steals it and please stop by again.", broke = "I am sorry you do not have enough  scrap" },
			new talkReply { message = "I am just browsing.", command = "close", choiceConfig = 2, goodbye = ""  }
			};
			
			SaveFile.theConfig["Messages"].theChoices.Add(3, new talkMessage());
			SaveFile.theConfig["Messages"].theChoices[3].message = "That is a nice choice. Do you want to buy this ride?";		
			SaveFile.theConfig["Messages"].theChoices[3].reply = new List<talkReply> { 
			new talkReply { message = "Here is my 350 scrap.", command = "SendAsPlayer carspawns55912 medium", choiceConfig = 3, price = 350, goodbye = "Go get your ride before someone steals it and please stop by again.", broke = "I am sorry you do not have enough  scrap" },
			new talkReply { message = "I am just browsing.", command = "close", choiceConfig = 3, goodbye = ""  }
			};
			
			SaveFile.theConfig["Messages"].theChoices.Add(4, new talkMessage());
			SaveFile.theConfig["Messages"].theChoices[4].message = "That is a nice choice. Do you want to buy this ride?";		
			SaveFile.theConfig["Messages"].theChoices[4].reply = new List<talkReply> { 
			new talkReply { message = "Here is my 500 scrap.", command = "SendAsPlayer carspawns55912 long", choiceConfig = 4, price = 500, goodbye = "Go get your ride before someone steals it and please stop by again.", broke = "I am sorry you do not have enough  scrap" },
			new talkReply { message = "I am just browsing.", command = "close", choiceConfig = 4, goodbye = ""  }
			};
			SavePlayData(SaveFile, Name + "/NpcInfo/" + "default");
		}

		public bool HasSaveFile(string id)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/NpcInfo/" + id);
        }

        private static void SavePlayData<T>(T data, string filename = null) =>
            Interface.Oxide.DataFileSystem.WriteObject(filename ?? plugin.Name, data);

        private static void LoadData<T>(out T data, string filename = null) =>
            data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? plugin.Name);
		
		void Init()
        {
            NPCDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/TalkerSpawns");
			PLAYERDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayerCooldown");
            LoadData();
			GetTheNpcDefaultFile();
			RegisterPermissions();
        }

        void LoadData()
        {
            try
            {
                npcData = Interface.Oxide.DataFileSystem.ReadObject<NpcPoshions>(Name + "/TalkerSpawns");
            }
            catch
            {
                PrintWarning("Couldn't load NpcInfo data, Creating new NpcInfo Data save file");
                npcData = new NpcPoshions();
            }
            try
            {
                cooldownData = Interface.Oxide.DataFileSystem.ReadObject<NpcCooldown>(Name + "/PlayerCooldown");
            }
            catch
            {
                PrintWarning("Couldn't load NpcInfo data, Creating new NpcInfo Data save file");
                cooldownData = new NpcCooldown();
            }			
        }
		
		public class NpcCooldown
		{
			public Dictionary<ulong, coolDowns> players = new Dictionary<ulong, coolDowns>();
		}
		
		public class coolDowns
		{
			public Dictionary<string, DateTime> time = new Dictionary<string, DateTime>();
		}
		
		void SaveCdata()
        {
            PLAYERDATA.WriteObject(cooldownData);
        }
		
		public class NpcPoshions
		{
			public Dictionary<string, SavedTalker> npc = new Dictionary<string, SavedTalker>();
		}
		
		public class SavedTalker
		{
			public string dataFile = "default";
			public Vector3 position;
			public Vector3 rotation;
			public string name;
			public uint netID;
			public float cooldown;
		}
		
		public class talkingNpc
		{
			public Dictionary<string, talkChoice> theConfig = new Dictionary<string, talkChoice>();
		}
		
		public class talkChoice
		{
			public Dictionary<int, talkMessage> theChoices = new Dictionary<int, talkMessage>();
		}

		public class talkMessage
		{
			public string message = "Welcome to my shop. What would you like to do?";
			public List<talkReply> reply = new List<talkReply>();
		}

		public class talkReply
		{
			public string command = "";
			public string message = "";
			public string goodbye = "";
			public string broke = "";
			public int price;
			public int item = -932201673;
			public int choiceConfig;
		}
		
		void SaveData()
        {
            NPCDATA.WriteObject(npcData);
        }
		
		#endregion

		private void OnServerInitialized()
		{
			if (!configData.settings.carData.ContainsKey("small"))
			{
				configData.settings.carData.Add("small", new spawnCar());
				configData.settings.carData.Add("medium", new spawnCar());
				string[] parts = new string[] { "vehicle.1mod.cockpit.with.engine", "vehicle.2mod.flatbed" };
				configData.settings.carData["medium"].Modules = parts;
				configData.settings.carData.Add("long", new spawnCar());
				parts = new string[] { "vehicle.1mod.cockpit.with.engine", "vehicle.2mod.passengers" };
				configData.settings.carData["long"].Modules = parts;
				SaveConfig();			
			}
			foreach (var key in npcData.npc.ToList())
            {
               	NPCTalking BotA = spawnTalker(key.Value.position, key.Value.rotation, key.Key, key.Value.name, true);
				if (BotA == null) continue;
				key.Value.netID = BotA.net.ID;
				SaveData();
            }	
		}
		
	    private void RegisterPermissions()
        {
            permission.RegisterPermission(configData.settings.PermissionUse, this);
        }
		
		void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer_Choices);
                CuiHelper.DestroyUi(player, Layer_Words);
				CuiHelper.DestroyUi(player, LayerMain);
            }
			foreach (var key in npcData.npc.ToList())
            {
                var networkable = BaseNetworkable.serverEntities.Find(key.Value.netID);
				if (networkable != null && networkable is NPCTalking)
					networkable?.Kill();
            }
        }

		private object CanHelicopterTarget(PatrolHelicopterAI heliAi, NPCTalking player)
		{
			return false;
		}
		
		private object CanHelicopterStrafeTarget(PatrolHelicopterAI heliAi, NPCTalking target)
		{
			return false;
		}
		
		object OnNpcConversationStart(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData)
		{
			if (npcTalking != null && player != null && npcData.npc.ContainsKey(npcTalking.displayName.ToLower()))
			{
				tempFix fix = npcTalking.GetComponent<tempFix>();
				if (fix != null && !fix.check.Contains(player)) 
					fix.check.Add(player);
				if (npcData.npc[npcTalking.displayName.ToLower()].cooldown > 0.0f && cooldownData.players.ContainsKey(player.userID) && cooldownData.players[player.userID].time.ContainsKey(npcTalking.displayName.ToLower()))
				{
					if (cooldownData.players[player.userID].time[npcTalking.displayName.ToLower()] > DateTime.Now)
					{
						var ts = cooldownData.players[player.userID].time[npcTalking.displayName.ToLower()] - DateTime.Now;
						string cooldown = string.Format("{0:hh\\:mm\\:ss}", ts);

						player?.SendConsoleCommand("gametip.hidegametip");
						player?.SendConsoleCommand("gametip.showgametip", lang.GetMessage("cooldown", this, player.UserIDString) + " " + cooldown + " ");
						timer.Once(3, () => { player?.SendConsoleCommand("gametip.hidegametip"); } );
						return false;
					}
				}
				
				if (!playerToNpc.ContainsKey(player.userID))
					playerToNpc.Add(player.userID, npcTalking.displayName.ToLower());
		    		else playerToNpc[player.userID] = npcTalking.displayName.ToLower();
			    
				DrawLayerMain(player, npcData.npc[npcTalking.displayName.ToLower()].dataFile, npcTalking.displayName);
				npcTalking?.CleanupConversingPlayers();
				return false;
			}
			return null;
		}

		class tempFix : MonoBehaviour
        {
			NPCTalking thetalker;
			public string dataFile = "default";
			public float cooldownTime = 0;
			public List<BasePlayer> check = new List<BasePlayer>();
			
			private void Awake()
            {
                thetalker = GetComponent<NPCTalking>();
            }
			void Update()
			{
				if (thetalker == null) return;
				if (check.Count > 0)
				{
					foreach (BasePlayer checkDis in check.ToList())
					{
						if (checkDis == null || Vector3.Distance(checkDis.transform.position, thetalker.transform.position) > 2.6f || checkDis.IsDead() || checkDis.IsSleeping())
						{
							check.Remove(checkDis);
							if (checkDis != null)
							{
								CuiHelper.DestroyUi(checkDis, plugin.Layer_Choices);
								CuiHelper.DestroyUi(checkDis, plugin.Layer_Words);
								CuiHelper.DestroyUi(checkDis, plugin.LayerMain);
							}
						}
					}
				}
				
				if (thetalker?.conversingPlayers == null) return;
			    //int count = thetalker.conversingPlayers.Count;
				if (thetalker.conversingPlayers.Count > 0)
				{
					foreach (BasePlayer player in thetalker.conversingPlayers.ToList())
					{
						if (player != null)
						{
							if (!check.Contains(player))
							    check.Add(player);
							thetalker?.ForceEndConversation(player);
							thetalker?.OnConversationEnded(player);					
						}
					}					
						thetalker?.CleanupConversingPlayers();
				}	
			}
		}
		
		private bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            if (player.serverInput?.current == null) return false;
            viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);
            return true;
        }
		
		private bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider.GetComponentInParent<TriggerBase>() == null && hit.distance < closestdist)
                {
                    closestdist = hit.distance;
                    closestEnt = hit.collider;
                    closestHitpoint = hit.point;
                }
            }

            if (closestEnt is bool) return false;
            return true;
        }

       [ChatCommand("npc_talker")]
        void commandspawn(BasePlayer player, string command, string[] args, bool rerun)
        {
			string colorCode = "#FFFF00";
			if (!permission.UserHasPermission(player.UserIDString, configData.settings.PermissionUse))
            {
				SendReply(player, string.Format(lang.GetMessage("Blocked", this, player.UserIDString)));
                return;
            }
			if (args.Length < 2)
			{
				SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_talker add <Name> <optional Datafile></color> - Adds npc.\n" + $"<color={colorCode}>/npc_talker remove <name> </color> - Removes npc.\n");
				return;
			}
            switch (args[0].ToLower())
            {
                case "add":
					if (args.Length < 2)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_talker add <Name> <optional Datafile></color> - Adds npc.\n" + $"<color={colorCode}>/npc_talker remove <name> </color> - Removes npc.\n");
						return;
					}
					if (npcData.npc.ContainsKey(args[1].ToLower()))	
					{
						SendReply(player, string.Format(lang.GetMessage("FailedToAddNpc", this, player.UserIDString)));
						return;
					}

					Quaternion playerloc;
					if(!TryGetPlayerView(player, out playerloc))
					{
						SendReply(player, "Couldn't get player rotation");
						return;
					}
					
					Vector3 entityloc = player.transform.position;
					
					NPCTalking BotA = spawnTalker(entityloc, playerloc.eulerAngles, args[1].ToLower(), args[1], false);
					if (BotA == null) return;

					npcData.npc.Add(args[1].ToLower(), new SavedTalker());
					npcData.npc[args[1].ToLower()].position = entityloc;
					npcData.npc[args[1].ToLower()].rotation = playerloc.eulerAngles;
					npcData.npc[args[1].ToLower()].name = args[1];
					npcData.npc[args[1].ToLower()].netID = BotA.net.ID;
					if (args.Length > 2)
					{
						npcData.npc[args[1].ToLower()].dataFile = args[2];
						tempFix fix = BotA.GetOrAddComponent<tempFix>();
						fix.dataFile = npcData.npc[args[1].ToLower()].dataFile;
						fix.cooldownTime = npcData.npc[args[1].ToLower()].cooldown;
						
					}
					SaveData();
					SendReply(player, string.Format(lang.GetMessage("Added", this, player.UserIDString)));
                    return;

                case "remove":	
					if (args.Length < 2)
					{
						SendReply(player, "Usage:\n\n" + $"<color={colorCode}>/npc_talker add <Name> <optional Datafile></color> - Adds talking npc.\n" + $"<color={colorCode}>/npc_talker remove <name> </color> - Remove the talking npc.\n");
						return;
					}
				     
					if (!npcData.npc.ContainsKey(args[1].ToLower()))	
					{
						SendReply(player, string.Format(lang.GetMessage("NoNpcFound", this, player.UserIDString)));
						return;
					}
					
					var networkable = BaseNetworkable.serverEntities.Find(npcData.npc[args[1].ToLower()].netID);
					if (networkable != null && networkable is NPCTalking)
						networkable?.Kill();
					npcData.npc.Remove(args[1].ToLower());
					SaveData();
					SendReply(player, string.Format(lang.GetMessage("Removed", this, player.UserIDString)));
                    return;
				default:
                    break;
			}
		}
		
		private NPCTalking spawnTalker(Vector3 entityloc, Vector3 playerloc, string theConfig, string name, bool load = false)
		{
			NPCTalking BotA = GameManager.server.CreateEntity("assets/prefabs/npc/bandit/shopkeepers/bandit_conversationalist.prefab", entityloc, Quaternion.Euler(playerloc)) as NPCTalking;
			if (BotA == null) return null;
			BotA.enableSaving = false;
			BotA.Spawn();
			BotA.displayName = name;
			tempFix fix = BotA.GetOrAddComponent<tempFix>();
			if (load)
			{
				fix.dataFile = npcData.npc[theConfig].dataFile;
				fix.cooldownTime = npcData.npc[theConfig].cooldown;
			}	
			return BotA;
		}
		private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        private void DrawLayerMain(BasePlayer player, string dataConfig, string name)
        {
			talkingNpc SaveFile = null;
			if (!HasSaveFile(dataConfig))
            {
				Puts("debug no save npcTalk file " + dataConfig);
                return;
            }
            else LoadData(out SaveFile, Name + "/NpcInfo/" + dataConfig);
			
            CuiHelper.DestroyUi(player, LayerMain);
			CuiHelper.DestroyUi(player, Layer_Message);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "255 255 255 0" } 
            }, "Overlay", LayerMain);

			container.Add(new CuiElement
            {
                Parent = LayerMain,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.14" }
                }
            });
			
			
			container.Add(new CuiElement
            {
                Parent = LayerMain,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.86", AnchorMax = "1 1" }
                }
            });
			
			//Hello Message Box Gray
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.60 0.33", AnchorMax = "0.85 0.65" },
                Image = { Color = "0 0 0 0.9" } 
            }, LayerMain, LayerMainBox);
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.61 0.50", AnchorMax = "0.84 0.60" },
                Image = { Color = HexToRustFormat("#696969") } 
            }, LayerMain, Layer_Words);

            container.Add(new CuiButton
            {					
                RectTransform = { AnchorMin = "0.955 0.926", AnchorMax = "1 1" },
                Button = { Color = "255 0 0", Close = LayerMain },
                Text = { Text = "X", FontSize = 10, Align = TextAnchor.MiddleCenter }
            }, LayerMainBox);
 
			// display npc name
			container.Add(new CuiButton
			{		
				RectTransform = { AnchorMin = "0.04 0.860", AnchorMax = "0.35 0.955"},
				Button = { Color = "0 0 0 0.8", Command = "" },
				Text = { Text = name, FontSize = 15, Font = "robotoCondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
			}, LayerMainBox);
 
            CuiHelper.AddUi(player, container);
			DrawLayerMessage(player, dataConfig, SaveFile.theConfig["Messages"].theChoices[0].message);
        }
		
		private void DrawLayerMessage(BasePlayer player, string theConfigs, string message, int placeHolder = 0, bool goodBye = false, int byeMark = 0, bool broke = false)
        {
			talkingNpc SaveFile = null;
            CuiHelper.DestroyUi(player, Layer_Message);
			LoadData(out SaveFile, Name + "/NpcInfo/" + theConfigs);
			message = SaveFile.theConfig["Messages"].theChoices[placeHolder].message;
			if (goodBye)
				message = SaveFile.theConfig["Messages"].theChoices[placeHolder].reply[byeMark].goodbye;
			else if (broke)
				message = SaveFile.theConfig["Messages"].theChoices[placeHolder].reply[byeMark].broke;

			var container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.02 0.06", AnchorMax = "0.98 0.92" },
                Image = { Color = "255 255 255 0" } 
            }, Layer_Words, Layer_Message);
			
			container.Add(new CuiElement
            {
                Parent = Layer_Message,
                Components =
                {
                    new CuiTextComponent { Text = message, Color = "255 255 255 0.9", Align = TextAnchor.UpperLeft, FontSize = 10, Font = "robotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
			CuiHelper.AddUi(player, container);
			DrawLayerResponce(player, SaveFile.theConfig["Messages"].theChoices[placeHolder].reply, theConfigs, goodBye, broke);			
		}

		private void DrawLayerResponce(BasePlayer player, List<talkReply> choices, string dataConfig, bool goodBye = false, bool broke = false)
        {
            CuiHelper.DestroyUi(player, Layer_Choices);
			var container = new CuiElementContainer();
			int count = 0;
			double AnchorMin = 0.0;
            double AnchorMin1 = 0.80;

            double AnchorMax = 0.995;
            double AnchorMax1 = 1.0;
			
			string AnchorMinSet = AnchorMin.ToString() + " " + AnchorMin1.ToString();
			string AnchorMaxSet = AnchorMax.ToString() + " " + AnchorMax.ToString();
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.04 0.06", AnchorMax = "0.96 0.50" },
                Image = { Color = "0 0 0 0" } 
            }, LayerMainBox, Layer_Choices);
	
			if (goodBye || broke)
			{
				if (goodBye)
				container.Add(new CuiButton
				{					
					RectTransform = { AnchorMin = AnchorMinSet, AnchorMax = AnchorMaxSet},
					Button = { Color = "2 2 2 0.60", Command = $"TalkingNpcUIHandler close none {dataConfig}" },
					Text = { Text = "   " + "1" + "    " + "See you later.", FontSize = 10, Align = TextAnchor.MiddleLeft }
				}, Layer_Choices, "buttons");
				
				if (broke)
				container.Add(new CuiButton
				{					
					RectTransform = { AnchorMin = AnchorMinSet, AnchorMax = AnchorMaxSet},
					Button = { Color = "2 2 2 0.60", Command = $"TalkingNpcUIHandler close none {dataConfig}" },
					Text = { Text = "   " + "1" + "    " + "I will come back later.", FontSize = 10, Align = TextAnchor.MiddleLeft }
				}, Layer_Choices, "buttons");
				
				container.Add(new CuiElement
				{
					Parent = "buttons",
					Components =
					{
						new CuiImageComponent { Color = "0 255 0 0.5" },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.05 1" }
					}
				});
				CuiHelper.AddUi(player, container);	
				return;
			}
			
			foreach (var key in choices)
			{
				if (count > 5) continue;
				count++;
				
				var output = Regex.Replace(key.command.Split()[0], @"[^0-9a-zA-Z\ ]+", "");
				//Button Boxs lighter Gray
				container.Add(new CuiButton
				{					
					RectTransform = { AnchorMin = AnchorMinSet, AnchorMax = AnchorMaxSet},
					Button = { Color = "2 2 2 0.60", Command = $"TalkingNpcUIHandler {output} {key.choiceConfig} {dataConfig} {count - 1}" },
					Text = { Text = "   " + count.ToString() + "    " + key.message, FontSize = 10, Align = TextAnchor.MiddleLeft }
				}, Layer_Choices, "buttons" + count.ToString());
				
				container.Add(new CuiElement
				{
					Parent = "buttons" + count.ToString(),
					Components =
					{
						new CuiImageComponent { Color = "0 255 0 0.5" },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.05 1" }
					}
				});
			
				AnchorMin1 = AnchorMin1 + -0.22;
				AnchorMax1 = AnchorMax1 + -0.22;
				AnchorMinSet = AnchorMin.ToString() + " " + AnchorMin1.ToString();
				AnchorMaxSet = AnchorMax.ToString() + " " + AnchorMax1.ToString();			
			}	
			CuiHelper.AddUi(player, container);	
		}
			
		[ConsoleCommand("TalkingNpcUIHandler")]
        private void CmdHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
			int Pos;
			if (!int.TryParse(args.Args[1], out Pos))
			{
				CuiHelper.DestroyUi(player, LayerMain);
				return;
			}
			if (Pos < 0)
				return;

			int goodBye;
			if (!int.TryParse(args.Args[3], out goodBye))
			{

			}
            switch (args.Args[0].ToLower())
            {
			    case "continue":
                {			    	
			    	DrawLayerMessage(player, args.Args[2], "Bad message Somewhere", Pos);
                    return;
			    }
                case "close":
                {
                    CuiHelper.DestroyUi(player, LayerMain);
					CuiHelper.DestroyUi(player, Layer_Message);
                    return;
                }
			}       
					talkingNpc SaveFile = null;
					LoadData(out SaveFile, Name + "/NpcInfo/" + args.Args[2]);
					if (SaveFile == null)
					{
						CuiHelper.DestroyUi(player, LayerMain);
						CuiHelper.DestroyUi(player, Layer_Message);
						SendReply(player, "Someting went wrong somewhere");
						return;
					}
					int price = SaveFile.theConfig["Messages"].theChoices[Pos].reply[goodBye].price;
					int item = SaveFile.theConfig["Messages"].theChoices[Pos].reply[goodBye].item;
					string comand = SaveFile.theConfig["Messages"].theChoices[Pos].reply[goodBye].command;
					if (price > 0)
					{
						if (item == 0000)
						{
							//check for owned cars
							string toBeSearched = "SendAsPlayer carspawns55912 ";
							int ix = comand.IndexOf(toBeSearched);
							string vehicleType = null;
							if (ix != -1)
							{
								vehicleType = comand.Substring(ix + toBeSearched.Length);
							}
							switch(vehicleType)
							{
								case "small":
									vehicleType = "SmallCar";
									break;
								case "medium":
									vehicleType = "MediumCar";
									break;
								case "long":
									vehicleType = "LargeCar";
									break;
								default:
									vehicleType = null;
									break;
							}
							bool hasVehicle = (bool)VehicleLicence?.Call("AlreadyHasVehicle", player, vehicleType);
							//
							if(!hasVehicle)
							{
								if (ServerRewards == null) return;
								int PlayerTotal = (int)ServerRewards?.Call("CheckPoints", player.userID);
								if (PlayerTotal >= price)
								{
									ServerRewards?.Call("TakePoints", player.userID, price);
								}
								else
								{
									DrawLayerMessage(player, args.Args[2], "Bad message Somewwhere", Pos, false, goodBye, true);
									return;
								}
							}
						}
						else if (item == 0001)
						{
							if (Economics == null) return;
							double total1double = (double)Economics.Call("Balance", player.userID);
							int PlayerTotal = (int)total1double;
							if (PlayerTotal >= price)
							{
								Economics?.Call("Withdraw", player.userID, price);
							}
							else
							{
								DrawLayerMessage(player, args.Args[2], "Bad message Somewwhere", Pos, false, goodBye, true);
								return;
							}
						}
						else
						{
							int PlayerTotal = player.inventory.GetAmount(item);
							if (PlayerTotal >= price)
							{
								player.inventory.Take(null, item, price);
							}
							else
							{
								DrawLayerMessage(player, args.Args[2], "Bad message Somewwhere", Pos, false, goodBye, true);
								return;
							}
						}
					}
							DrawLayerMessage(player, args.Args[2], "Bad message Somewwhere", Pos, true, goodBye);						
							string newComand = comand.Replace("playerID", player.userID.ToString());	
					if (playerToNpc.ContainsKey(player.userID))
					{
						if (!cooldownData.players.ContainsKey(player.userID))
							cooldownData.players.Add(player.userID, new coolDowns());
							
						if (npcData.npc.ContainsKey(playerToNpc[player.userID]) && npcData.npc[playerToNpc[player.userID]].cooldown > 0.0f)
						{
							if (!cooldownData.players[player.userID].time.ContainsKey(playerToNpc[player.userID]))
								cooldownData.players[player.userID].time.Add(playerToNpc[player.userID], DateTime.Now.AddSeconds((int)npcData.npc[playerToNpc[player.userID]].cooldown));
							else cooldownData.players[player.userID].time[playerToNpc[player.userID]] = DateTime.Now.AddSeconds((int)npcData.npc[playerToNpc[player.userID]].cooldown);
							SaveCdata();
						}
					}
								
					if (newComand.Contains("SendAsPlayer"))
					{
						newComand = newComand.Replace("SendAsPlayer ", "");
						player.SendConsoleCommand($"{newComand}");				
						return;
					}
							
							rust.RunServerCommand($"{newComand}");
							return;

					DrawLayerMessage(player, args.Args[2], "Bad message Somewwhere", Pos, true, goodBye);
        }


	
			
	}
}
    
