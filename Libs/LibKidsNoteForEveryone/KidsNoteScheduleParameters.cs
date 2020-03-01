using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteScheduleParameters
    {
        public enum DaysType
        {
            MON_FRI = 0,
            WHOLE_WEEK,
        }

        public enum JobType
        {
            JOB_CHECK_NEW_CONTENTS = 0,
        }

        public override string ToString()
        {
            if (Job == JobType.JOB_CHECK_NEW_CONTENTS)
            {
                if (Days == DaysType.MON_FRI)
                {
                    return String.Format("{0}_WEEKDAYS", Job);
                }
                else
                {
                    return String.Format("{0}_EVERYDAY", Job);
                }
            }
            else
            {
                return "UNDEFINED_JOB";
            }
        }

        public DaysType Days { get; set; }
        public JobType Job { get; set; }
    }
}
