namespace SimpleNetworking.Utils
{
    internal class InternalLogger
    {
        private readonly ILogger logger;
        
        public InternalLogger(ILogger logger)
        {
            this.logger = logger;
        }
        
        public void Debug(string message)
        {
            logger?.Debug(message);
        }
        
        public void Info(string message)
        {
            logger?.Info(message);
        }
        
        public void Warn(string message)
        {
            logger?.Warn(message);
        }
        
        public void Error(string message)
        {
            logger?.Error(message);
        }
    }
}