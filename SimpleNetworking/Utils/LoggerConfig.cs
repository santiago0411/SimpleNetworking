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

        private static void SetBasicConfig(Level level)
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
            hierarchy.Root.Level = level;
            hierarchy.Configured = true;
        }

        internal static void CheckLoggerConfig(Level level)
        {
            if (!log.Logger.Repository.Configured)
            {
                SetBasicConfig(level);
                return;
            }

            if (level.Equals(Level.Off))
                log.Warn("Logger has already been configured and it might not be disabled.");
        }
    }
}
