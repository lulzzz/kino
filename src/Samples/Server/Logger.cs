﻿using NLog;
using ILogger = kino.Core.Diagnostics.ILogger;

namespace Server
{
    public class Logger : ILogger
    {
        private readonly NLog.Logger logger;

        public Logger(string name)
            => logger = LogManager.GetLogger(name);

        public void Warn(object message)
            => logger.Warn(message);

        public void Info(object message)
            => logger.Info(message);

        public void Debug(object message)
            => logger.Debug(message);

        public void Error(object message)
            => logger.Error(message);

        public void Trace(object message)
            => logger.Trace(message);
    }
}