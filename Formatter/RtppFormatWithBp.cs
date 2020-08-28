using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RealTimePPDisplayer.Displayer;
using RealTimePPDisplayer.Formatter;
using Sync;
using Sync.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static OsuRTDataProvider.Listen.OsuListenerManager;
using static OsuRTDataProvider.Mods.ModsInfo;

namespace RealTimePPDisplayer.Formatter
{
    class RtppFormatWithBp : RtppFormatter,IFormatterClearable
    {
        private static List<BeatPerformance> bps = null;
        private static int bps_locked = 0;
        private static UserInfo userInfo = null;
        private static int userinfo_locked = 0;

        public RtppFormatWithBp(string format) : base(format)
        {

        }

        private void InitPublicOsuBotTransferPlugin()
        {
        }

        private static int FindBpIndex(List<BeatPerformance> bpsOG, double pp, int? mapID)
        {
            List<BeatPerformance> bps = new List<BeatPerformance>(bpsOG);
            if (mapID.HasValue)
            {
                foreach (BeatPerformance bP in bpsOG)
                {
                    if (bP.BeatmapID == mapID.Value)
                    {
                        bps.Remove(bP);
                    }
                }
            }

            for (int i = bps.Count - 1; i >= 0; i--)
            {
                if (bps[i].PP > pp)
                {
                    return i+1;
                }
            }

            return 0;
        }

        private static List<BeatPerformance> GetBpWithCurrentPP(List<BeatPerformance> bps, double pp, double weight, int? mapID)
        {
            List<BeatPerformance> tempBps = new List<BeatPerformance>(bps);
            if (mapID.HasValue)
            {
                foreach (BeatPerformance bP in bps)
                {
                    if (bP.BeatmapID == mapID.Value)
                    {
                        if (bP.PP < pp)
                        {
                            tempBps.Remove(bP);
                            BeatPerformance currMapBP = new BeatPerformance();
                            currMapBP.PP = pp * weight;
                            currMapBP.BeatmapID = mapID.HasValue ? mapID.Value : 0;
                            tempBps.Add(currMapBP);
                            tempBps.Sort(CompareBP);
                            tempBps.Reverse();
                        }
                    }
                }
            }

            return tempBps;
        }

        private static double GetTotalPPFromBP(List<BeatPerformance> bps)
        {
            double tempPP = 0;
            int i = 0;
            foreach (BeatPerformance bp in bps)
            {
                tempPP += bp.PP * GetWeight(i++);
            }
            return tempPP;
        }

        private static int CompareBP(BeatPerformance x, BeatPerformance y)
        {
            if (x.PP < y.PP) return -1;
            if (x.PP > y.PP) return 1;
            return 0;
        }

        private void GetBpFromOsu()
        {
            if (bps_locked == 0)
            {
                if (bps == null)
                {
                    var mode = Displayer.Mode;

                    Interlocked.Increment(ref bps_locked);
                    bps = OsuApi.GetBp(Displayer.Playername, mode);
                    Interlocked.Decrement(ref bps_locked);
                }
            }
        }

        private void GetUserInfoFromOsu()
        {
            if (userinfo_locked == 0)
            {
                if (bps == null)
                {
                    var mode = Displayer.Mode;

                    Interlocked.Increment(ref userinfo_locked);
                    userInfo = OsuApi.GetPlayerInfo(Displayer.Playername, mode);
                    Interlocked.Decrement(ref userinfo_locked);
                }
            }
        }

        public void UpdateBpList()
        {
            GetBpFromOsu();
            GetUserInfoFromOsu();

            if (bps == null || (userInfo == null && !Setting.ByCuteSyncProxy))
            {
                return;
            }

            bool playedBefore = false;
            foreach (BeatPerformance bp in bps)
            {
                if (bp.BeatmapID == Displayer.BeatmapTuple.BeatmapID) playedBefore = true;
            }

            double totalBeatmapCurrentPP = GetTotalPPFromBP(bps);
            double totalCurrentPP = userInfo.TotalPP;
            double bonusPP = totalCurrentPP - totalBeatmapCurrentPP;
            double bonusPPAfterPlay = bonusPP;
            if (!playedBefore && bonusPP < 416.6667)
            {
                int scoreCount = (int)Math.Round(Math.Log10(-(bonusPP / 416.6667D) + 1.0D) / Math.Log10(0.9994D));
                bonusPPAfterPlay = 416.6667 * (1 - Math.Pow(0.9994, ++scoreCount));
            }
            int rtbp = FindBpIndex(bps, Displayer.Pp.RealTimePP, Displayer.BeatmapTuple.BeatmapID);
            int fcbp = FindBpIndex(bps, Displayer.Pp.FullComboPP, Displayer.BeatmapTuple.BeatmapID);
            double rtpp_weight = GetWeight(rtbp);
            double fcpp_weight = GetWeight(fcbp);
            List<BeatPerformance> bpWithRTPP = GetBpWithCurrentPP(bps, Displayer.Pp.RealTimePP, rtpp_weight, Displayer.BeatmapTuple.BeatmapID);
            List<BeatPerformance> bpWithFCPP = GetBpWithCurrentPP(bps, Displayer.Pp.FullComboPP, fcpp_weight, Displayer.BeatmapTuple.BeatmapID);
            double totalRTPP = GetTotalPPFromBP(bpWithRTPP);
            double totalFCPP = GetTotalPPFromBP(bpWithFCPP);

            Context.Variables["totalpp"] = totalCurrentPP;
            Context.Variables["ppaddrt"] = totalRTPP - totalCurrentPP + bonusPPAfterPlay;
            Context.Variables["ppaddfc"] = totalFCPP - totalCurrentPP + bonusPPAfterPlay;

            if (rtbp != -1)
            {
                Context.Variables["rtbp"] = rtbp + 1;
                Context.Variables["fcbp"] = fcbp + 1;
                Context.Variables["rtpp_with_weight"] = Displayer.Pp.RealTimePP * rtpp_weight;
                Context.Variables["fcpp_with_weight"] = Displayer.Pp.FullComboPP * fcpp_weight;
            }
            else
            {
                Context.Variables["rtbp"] = 0;
                Context.Variables["fcbp"] = 0;
                Context.Variables["rtpp_with_weight"] = 0;
                Context.Variables["fcpp_with_weight"] = 0;
            }
            Context.Variables["rtpp_weight"] = rtpp_weight;
            Context.Variables["fcpp_weight"] = rtpp_weight;
            Context.Variables["bonuspp"] = bonusPP;
        }

        public override string GetFormattedString()
        {
            if (Displayer.Mods.HasMod(Mods.Autoplay) || Displayer.Mods.HasMod(Mods.Cinema))
            {
                return base.GetFormattedString();
            }

            if (Constants.NO_FETCH_BP_USERNAMES.Any(u => u == Displayer.Playername))
            {
                return base.GetFormattedString();
            }

            UpdateBpList();

            return base.GetFormattedString();
        }

        public new void Clear()
        {
            bps = null;

            base.Clear();
        }

        private static double GetWeight(int index)
        {
            return Math.Pow(0.95, index);
        }
    }
}
