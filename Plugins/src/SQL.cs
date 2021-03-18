using Oxide.Core;
using Oxide.Core.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SQL", "TeamDMA", "0.0.1")]
    [Description("Manages MySQL Database")]
    class SQL : RustPlugin
    {
        Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
        Connection sqlConnection = null;

        string hostname = "45.89.126.3";
        int port = 3306;
        string dbname = "s3998_rust";
        string username = "u3998_Ko5bGz5pUY";
        string password = "gU=+.xbmYLcViFkM0kK^n1vW";

        float timer1 = 2f; // in s
        float waitUntilDelete = 600f; // in s

        int playersCount = 0;
        int pingLimit = 80;
        int intervalLastLagg = 20; // in min
        private Dictionary<ulong, List<int>> pingsList = new Dictionary<ulong, List<int>>();

        string sqlQuery = "INSERT INTO serverfps (data) VALUES(@0)";
        string sqlQueryDeleteOldEntries = "DELETE FROM serverfps WHERE cur_timestamp < NOW() - INTERVAL 7 DAY";
        string sqlQueryDeleteUnnecessaryEntries = "DELETE FROM serverfps WHERE (TIME(cur_timestamp) BETWEEN '00:59:30' AND '01:10:00') OR (TIME(cur_timestamp) BETWEEN '11:59:30' AND '12:10:00')";

        string sqlQueryPlayers = "INSERT INTO serverplayers (data) VALUES(@0)";
        string sqlQueryDeleteOldEntriesPlayers = "DELETE FROM serverplayers WHERE cur_timestamp < NOW() - INTERVAL 30 DAY";
        string sqlQueryDeleteUnnecessaryEntriesPlayers = "DELETE FROM serverplayers WHERE TIME(cur_timestamp) BETWEEN '00:59:30' AND '01:10:00' OR (TIME(cur_timestamp) BETWEEN '11:59:30' AND '12:10:00')";

        string sqlQueryCountLaggs = "SELECT cur_timestamp FROM serverfps WHERE data <= 60 ORDER BY cur_timestamp DESC LIMIT 1"; // last lagg

        private void Init()
        {
            playersCount = GetPlayersCount();

            sqlConnection = sqlLibrary.OpenDb(hostname, port, dbname, username, password, this);

            timer.Once(waitUntilDelete, () =>
            {
                DeleteEntries();
            });
            
            timer.Every(timer1, () =>
            {
                int serverfps = GetFPS();
                // fps
                Sql sqlCommand = Oxide.Core.Database.Sql.Builder.Append(sqlQuery, serverfps);
                SendQuery(sqlCommand, sqlConnection);
            });
        }
        private void Unload()
        {
            if(sqlConnection != null)
            {
                DeleteEntries();

                sqlLibrary.CloseDb(sqlConnection);
            }
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            playersCount++;
            SendPlayersCount();
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            playersCount--;
            SendPlayersCount();
        }
        private void SendPlayersCount()
        {
            // players
            Sql sqlCommand = Oxide.Core.Database.Sql.Builder.Append(sqlQueryPlayers, playersCount);
            SendQuery(sqlCommand, sqlConnection);
        }
        private void SendQuery(Sql command, Connection con)
        {
            if(sqlConnection != null)
            {
                sqlLibrary.ExecuteNonQuery(command, con);
            }
        }
        private void DeleteEntries()
        {
            Puts("Deleting old and unnecessary entries...");

            // delete old entries
            Sql command = Oxide.Core.Database.Sql.Builder.Append(sqlQueryDeleteOldEntries);
            sqlLibrary.ExecuteNonQuery(command, sqlConnection);
            // delete between 00:59:30 and 01:10:00
            command = Oxide.Core.Database.Sql.Builder.Append(sqlQueryDeleteUnnecessaryEntries);
            sqlLibrary.ExecuteNonQuery(command, sqlConnection);

            // same with pings, delete old entries
            command = Oxide.Core.Database.Sql.Builder.Append(sqlQueryDeleteOldEntriesPlayers);
            sqlLibrary.ExecuteNonQuery(command, sqlConnection);
            // delete between 00:59:30 and 01:10:00
            command = Oxide.Core.Database.Sql.Builder.Append(sqlQueryDeleteUnnecessaryEntriesPlayers);
            sqlLibrary.ExecuteNonQuery(command, sqlConnection);

            Puts("Deleted.");
        }
        private int GetFPS()
        {
            return Performance.current.frameRate;
        }
        private int GetPlayersCount()
        {
            return BasePlayer.activePlayerList.Count;
        }
        private void SendAlgorithmResult(BasePlayer player, bool isServerLagging = false, DateTime? lastLagg = null)
        {
            if (player != null)
            {
                if (isServerLagging && (lastLagg != null))
                {
                    int lastMinutes = (int)Math.Round((DateTime.Now.AddMinutes(60)).Subtract((DateTime)lastLagg).TotalMinutes);
                    SendReply(player, String.Format("[<color=#00FFFF>Algorithm</color>] Diagnosis: <color=#FF0000>The server was lagging {0}min ago</color>.", lastMinutes));
                }
                else
                {
                    if (pingsList.ContainsKey(player.userID))
                    {
                        int pPing = (int)Math.Round(pingsList[player.userID].Average());
                        if (pPing > pingLimit)
                        {
                            SendReply(player, "[<color=#00FFFF>Algorithm</color>] Diagnosis: <color=#FF0000>You are lagging</color>.");
                        }
                        else
                        {
                            SendReply(player, "[<color=#00FFFF>Algorithm</color>] Diagnosis: <color=#00FF00>Everything is fine</color>.");
                        }
                        pingsList.Remove(player.userID);
                    }       
                }              
            }
        }

        #region Commands

        [ChatCommand("lagg")]
        void LaggCheckCommand(BasePlayer player, string command, string[] args)
        {
            if(!pingsList.ContainsKey(player.userID))
            {
                IPlayer iPlayer = player.IPlayer;
                SendReply(player, "[<color=#00FFFF>Algorithm</color>] Computing laggs...");
                Sql selectCommand = Oxide.Core.Database.Sql.Builder.Append(sqlQueryCountLaggs);
                sqlLibrary.Query(selectCommand, sqlConnection, list =>
                {
                    if (list == null)
                    {
                        return; // Empty result or no records found
                    }
                    // Iterate through resulting records
                    bool isLagg = false;
                    DateTime timeLastLagg = new DateTime();
                    foreach (Dictionary<string, object> entry in list)
                    {
                        if (entry.ContainsKey("cur_timestamp"))
                        {
                            timeLastLagg = Convert.ToDateTime(entry["cur_timestamp"]);
                            DateTime nowInterval = DateTime.Now.AddMinutes(-intervalLastLagg + (60)); // + 60 because of time shifting
                            if (!(timeLastLagg > nowInterval)) // if no lagg
                            {
                                isLagg = false;
                                timeLastLagg = DateTime.MinValue;
                            }
                            else
                            {
                                isLagg = true;
                            }
                            break;
                        }
                    }
                    if (isLagg && (timeLastLagg != DateTime.MinValue))
                    {
                        SendAlgorithmResult(player, true, timeLastLagg);    
                    }
                    else
                    {
                        List<int> tmpList = new List<int>();
                        pingsList.Add(player.userID, tmpList);
                        pingsList[player.userID].Add(iPlayer.Ping); // No. 1
                        int count = 0;
                        timer.Repeat(1.33f, 2, () =>
                        {
                            pingsList[player.userID].Add(iPlayer.Ping);
                            count++;
                            if(count == 2)
                            {
                                SendAlgorithmResult(player, false);
                            }
                        });
                    }
                });
            }
        }
        #endregion
    }
}
