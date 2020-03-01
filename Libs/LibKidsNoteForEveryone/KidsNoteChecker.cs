using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteChecker : Fundamentals.Looper
    {
        public KidsNoteChecker()
            : base("KidsNoteChecker")
        {

        }

        private void Check()
        {

        }

        protected override void LooperFunc()
        {
            if (QuitRequested)
            {
                Thread.Sleep(50);
                return;
            }

            DateTime now = DateTime.Now;
            if (now.DayOfWeek == DayOfWeek.Saturday ||
                now.DayOfWeek == DayOfWeek.Sunday)
            {
                SleepForAWhile(60 * 60 * 10);   // 1시간.
                return;
            }

            Check();

            SleepForAWhile(60 * 10 * 10);   // 10분.
        }

        private void SleepForAWhile(int numOf100MilliSecs)
        {
            while (--numOf100MilliSecs >= 0 && !QuitRequested)
            {
                Thread.Sleep(100);
            }
        }
    }
}
