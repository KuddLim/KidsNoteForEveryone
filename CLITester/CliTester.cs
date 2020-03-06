using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibKidsNoteForEveryone;

namespace CLITester
{
    public class CliTester
    {
        private HashSet<ContentType> MonitoringTypes;
        private KidsNoteNotifierManager Manager;

        public CliTester()
        {
            MonitoringTypes = new HashSet<ContentType>() {
                ContentType.REPORT, ContentType.NOTICE, ContentType.ALBUM
            };

            Manager = new KidsNoteNotifierManager(MonitoringTypes);

            KidsNoteScheduleParameters param = new KidsNoteScheduleParameters();
            param.Days = KidsNoteScheduleParameters.DaysType.MON_FRI;
            param.Job = KidsNoteScheduleParameters.JobType.JOB_CHECK_NEW_CONTENTS;
            Manager.AddJob(param);

            Manager.Startup();
        }
    }
}
