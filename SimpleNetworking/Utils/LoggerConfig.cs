using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Appender;
using log4net.Layout;


namespace SimpleNetworking.Utils
{
    internal static class LoggerConfig
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LoggerConfig));

        public static void SetBasicConfig(bool disableLogging = false)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();

            var patternLayout = new PatternLayout
            {
                ConversionPattern = "[%date{yyyy-MM-dd HH:mm:ss}] [%level] [%thread] [%logger] - %message%newline"
            };
            patternLayout.ActivateOptions();

            var appender = new ConsoleAppender
            {
                Layout = patternLayout,
            };
            appender.ActivateOptions();

            hierarchy.Root.AddAppender(appender);
            hierarchy.Root.Level = disableLogging ? Level.Off : Level.Debug;
            hierarchy.Configured = true;
        }

        internal static void CheckLoggerConfig(bool disableLogging)
        {
            if (!log.Logger.Repository.Configured)
            {
                SetBasicConfig(disableLogging);
                return;
            }

            if (disableLogging)
                log.Warn("Logger has already been configured and it might not be disabled.");
        }
    }
}
