// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;

namespace ProjectNavigator
{
    /// <summary>
    /// Exponentially decayed visit counter ("frecency"). Each visit adds 1; the total halves every
    /// <c>halfLifeDays</c>. A folder visited 20 times last month ranks below one visited 5 times today.
    /// </summary>
    internal static class Frecency
    {
        public static double Decayed(double score, long lastVisitTicks, long nowTicks, double halfLifeDays)
        {
            if (score <= 0) return 0;
            if (halfLifeDays <= 0) return score;
            double days = (nowTicks - lastVisitTicks) / (double)TimeSpan.TicksPerDay;
            if (days <= 0) return score;
            return score * Math.Pow(0.5, days / halfLifeDays);
        }

        public static double Bumped(double score, long lastVisitTicks, long nowTicks, double halfLifeDays)
        {
            return Decayed(score, lastVisitTicks, nowTicks, halfLifeDays) + 1.0;
        }
    }
}
