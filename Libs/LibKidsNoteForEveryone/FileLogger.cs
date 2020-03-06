using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;

namespace LibKidsNoteForEveryone
{
    public class FileLogger
    {
        private static FileLogger TheSingleton = null;
        private static ILog LoggerInterface = null;

        public delegate bool UseLoggerDelegate();
        public UseLoggerDelegate UseLogger;

        private FileLogger()
        {
            LoggerInterface = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            log4net.Repository.Hierarchy.Logger logger = (log4net.Repository.Hierarchy.Logger)LoggerInterface.Logger;
            logger.Level = Level.All;

            string logDirectory = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "Logs";

            if (!System.IO.Directory.Exists(logDirectory))
            {
                System.IO.Directory.CreateDirectory(logDirectory);
            }

            string logFileName = "KidsNoteForEveryone.log";
            string logFilePathName = logDirectory + System.IO.Path.DirectorySeparatorChar + logFileName;

            logger.AddAppender(CreateRollingFileAppender(logFileName, logFilePathName));
        }

        private IAppender CreateRollingFileAppender(string name, string fileName)
        {
            log4net.Appender.RollingFileAppender appender = new RollingFileAppender();

            appender.Name = name;
            appender.File = fileName;
            appender.AppendToFile = true;
            appender.LockingModel = new FileAppender.MinimalLock();
            //appender.ImmediateFlush = true;
            appender.RollingStyle = RollingFileAppender.RollingMode.Date;
            appender.StaticLogFileName = true;

            log4net.Layout.PatternLayout layout = new log4net.Layout.PatternLayout();
            layout.ConversionPattern = "%d [%t] %-5p %c [%x] - %m%n";
            layout.ActivateOptions();

            appender.Encoding = Encoding.UTF8;
            appender.Layout = layout;
            appender.ActivateOptions();

            return appender;
        }

        public void WriteLog(string text)
        {
            if (UseLogger != null && !UseLogger())
            {
                return;
            }

            try
            {
                LoggerInterface.Info(text);
            }
            catch (Exception)
            {
            }
        }

        public static FileLogger Singleton
        {
            get
            {
                if (TheSingleton == null)
                {
                    TheSingleton = new FileLogger();
                }

                return TheSingleton;
            }
        }
    }
}
