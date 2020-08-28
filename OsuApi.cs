using Newtonsoft.Json.Linq;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider.Mods;
using RealTimePPDisplayer.Warpper;
using Sync;
using Sync.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RealTimePPDisplayer
{

    public class BeatPerformance
    {
        public int BeatmapID;
        public string ScoreID;
        public int Score;
        public int MaxCombo;
        public int Count50;
        public int Count100;
        public int Count300;
        public int CountMiss;
        public int CountKatu;
        public int CountGeki;

        public bool Perfect;
        public ModsInfo EnabledMods;
        public string UserID;
        public DateTime Date;
        public string Rank;
        public double PP;
    }

    public class UserInfo
    {
        public string UserID;
        public string Username;
        public string JoinDate;
        public uint Count50;
        public uint Count100;
        public uint Count300;
        public uint PlayCount;
        public string RankedScore;
        public string TotalScore;
        public uint PPRank;
        public double TotalPP;
        public double Level;
        public double Accuracy;
        public uint CountSS;
        public uint CountSSH;
        public uint CountS;
        public uint CountSH;
        public uint CountA;
        public string Country;
        public string TotalSecondsPlayed;
        public uint PPRankCountry;
    }

    static class OsuApi
    {
        public static PublicOsuBotTransferWarpper publicOsuBotTransferWarpper;

        public static List<BeatPerformance> GetBp(string player,OsuPlayMode mode)
        {
            HttpWebRequest req;
            if(Setting.ByCuteSyncProxy)
            {
                if (publicOsuBotTransferWarpper == null)
                {
                    publicOsuBotTransferWarpper = new PublicOsuBotTransferWarpper();
                    if (!publicOsuBotTransferWarpper.Init())
                        return null;
                }
                if(publicOsuBotTransferWarpper.Username != player)
                {
                    Sync.Tools.IO.DefaultIO.WriteColor(DefaultLanguage.HINT_CANNOT_WATCH_OTHER_PLAYER, ConsoleColor.Yellow);
                    return null;
                }

                if (string.IsNullOrEmpty(publicOsuBotTransferWarpper.Token))
                    return null;

                req = (HttpWebRequest)WebRequest.Create($"https://osubot.kedamaovo.moe/osuapi/bp?k={publicOsuBotTransferWarpper.Token}&u={player}&type=string&limit=100&m={(uint)mode}");
            }
            else
            {
                req = (HttpWebRequest)WebRequest.Create($"https://osu.ppy.sh/api/get_user_best?k={Setting.ApiKey}&u={player}&type=string&limit=100&m={(uint)mode}");
            }

            req.Timeout = 5000;
            List<BeatPerformance> result = new List<BeatPerformance>();
            Stream stream = null;
            try
            {
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                stream = resp.GetResponseStream();
                using (StreamReader sr = new StreamReader(stream))
                {
                    var json = sr.ReadToEnd();
                    var objs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Dictionary<string,string>>>(json);
                    foreach(var obj in objs)
                    {
                        BeatPerformance bp = new BeatPerformance();
                        bp.BeatmapID = int.Parse(obj["beatmap_id"]);
                        bp.Score = int.Parse(obj["score"]);
                        bp.ScoreID = obj["score_id"];
                        bp.MaxCombo = int.Parse(obj["maxcombo"]);
                        bp.Count50 = int.Parse(obj["count50"]);
                        bp.Count100 = int.Parse(obj["count100"]);
                        bp.Count300 = int.Parse(obj["count300"]);
                        bp.CountMiss = int.Parse(obj["countmiss"]);
                        bp.CountKatu = int.Parse(obj["countkatu"]);
                        bp.CountGeki = int.Parse(obj["countgeki"]);
                        bp.Perfect = obj["perfect"] == "1";
                        Enum.TryParse<ModsInfo.Mods>(obj["enabled_mods"],out var mods);
                        bp.EnabledMods = new ModsInfo() { Mod = mods };

                        bp.UserID = obj["user_id"];
                        bp.Date = DateTime.Parse(obj["date"]);
                        bp.Rank = obj["rank"];
                        bp.PP = double.Parse(obj["pp"]);
    
                        result.Add(bp);
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                stream?.Close();
            }
            
            result.Sort((a, b) => b.PP.CompareTo(a.PP));
            return result;
        }

        public static UserInfo GetPlayerInfo(string player, OsuPlayMode mode)
        {
            HttpWebRequest req;
            if (Setting.ByCuteSyncProxy)
            {
                return null;
            }
            else
            {
                req = (HttpWebRequest)WebRequest.Create($"https://osu.ppy.sh/api/get_user?k={Setting.ApiKey}&u={player}&type=string&m={(uint)mode}");
            }

            req.Timeout = 5000;
            UserInfo result = new UserInfo();
            Stream stream = null;
            try
            {
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                stream = resp.GetResponseStream();
                using (StreamReader sr = new StreamReader(stream))
                {
                    var json = sr.ReadToEnd();
                    JArray jObject = JArray.Parse(json);

                    JToken obj = jObject[0];                    
                    result.Accuracy = double.Parse(obj["accuracy"].Value<string>());
                    result.Count50 = uint.Parse(obj["count50"].Value<string>());
                    result.Count100 = uint.Parse(obj["count100"].Value<string>());
                    result.Count300 = uint.Parse(obj["count300"].Value<string>());
                    result.CountA = uint.Parse(obj["count_rank_a"].Value<string>());
                    result.CountS = uint.Parse(obj["count_rank_s"].Value<string>());
                    result.CountSH = uint.Parse(obj["count_rank_sh"].Value<string>());
                    result.CountSS = uint.Parse(obj["count_rank_ss"].Value<string>());
                    result.CountSSH = uint.Parse(obj["count_rank_ssh"].Value<string>());
                    result.Country = obj["country"].Value<string>();
                    result.JoinDate = obj["join_date"].Value<string>();
                    result.Level = double.Parse(obj["level"].Value<string>());
                    result.PlayCount = uint.Parse(obj["playcount"].Value<string>());
                    result.PPRank = uint.Parse(obj["pp_rank"].Value<string>());
                    result.PPRankCountry = uint.Parse(obj["pp_country_rank"].Value<string>());
                    result.RankedScore = obj["ranked_score"].Value<string>();
                    result.TotalScore = obj["total_score"].Value<string>();
                    result.TotalSecondsPlayed = obj["total_seconds_played"].Value<string>();
                    result.UserID = obj["user_id"].Value<string>();
                    result.Username = obj["username"].Value<string>();
                    result.TotalPP = double.Parse(obj["pp_raw"].Value<string>());
                }
            }
            catch (Exception e)
            {
                IO.CurrentIO.WriteColor(e.ToString(), ConsoleColor.Red);
                return null;
            }
            finally
            {
                stream?.Close();
            }

            return result;
        }
    }
}
