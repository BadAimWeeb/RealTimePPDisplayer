using RealTimePPDisplayer.Displayer;
using RealTimePPDisplayer.Formatter;
using Sync;
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
        public RtppFormatWithBp(string format) : base(format)
        {

        }

        private void InitPublicOsuBotTransferPlugin()
        {
        }

        private static int FindBpIndex(List<BeatPerformance> bps,double pp)
        {
            for (int i = bps.Count - 1; i >= 0; i--)
            {
                if (bps[i].PP > pp)
                {
                    return i+1;
                }
            }
            return 0;
        }

        private static List<BeatPerformance> GetBpWithCurrentPP(List<BeatPerformance> bps, double weightedPP, int? mapID)
        {
            List<BeatPerformance> tempBps = new List<BeatPerformance>(bps);
            if (mapID.HasValue)
            {
                foreach (BeatPerformance bP in tempBps)
                {
                    if (bP.BeatmapID == mapID.Value)
                    {
                        tempBps.Remove(bP);
                    }
                }
            }
            BeatPerformance currMapBP = new BeatPerformance();
            currMapBP.PP = weightedPP;
            currMapBP.BeatmapID = mapID.HasValue ? mapID.Value : 0;
            tempBps.Add(currMapBP);
            tempBps.Sort(CompareBP);

            return tempBps;
        }

        private static double GetTotalPPFromBP(List<BeatPerformance> bps)
        {
            double tempPP = 0;
            int i = 0;
            foreach (BeatPerformance bp in bps)
            {
                tempPP += bp.PP * GetWeight(i);
                i++;
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

        public void UpdateBpList()
        {
            GetBpFromOsu();

            if (bps == null)
            {
                return;
            }

            double totalCurrentPP = GetTotalPPFromBP(bps);
            int rtbp = FindBpIndex(bps, Displayer.Pp.RealTimePP);
            int fcbp = FindBpIndex(bps, Displayer.Pp.FullComboPP);
            double rtpp_weight = GetWeight(rtbp);
            double fcpp_weight = GetWeight(fcbp);
            List<BeatPerformance> bpWithRTPP = GetBpWithCurrentPP(bps, Displayer.Pp.RealTimePP * rtpp_weight, Displayer.Id);
            List<BeatPerformance> bpWithFCPP = GetBpWithCurrentPP(bps, Displayer.Pp.FullComboPP * fcpp_weight, Displayer.Id);
            double totalRTPP = GetTotalPPFromBP(bpWithRTPP);
            double totalFCPP = GetTotalPPFromBP(bpWithFCPP);

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

            Context.Variables["totalpp"] = totalCurrentPP;
            Context.Variables["ppaddrt"] = totalRTPP - totalCurrentPP;
            Context.Variables["ppaddfc"] = totalFCPP - totalCurrentPP;
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
