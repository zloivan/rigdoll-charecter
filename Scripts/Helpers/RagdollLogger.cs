using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _RagdollCharacterMechanic.Scripts.Helpers
{
    internal class RagdollLogger : ILogger
    {
        public ILogHandler logHandler { get; set; } = Debug.unityLogger.logHandler;
        public bool logEnabled { get; set; } = true;
        public LogType filterLogType { get; set; } = LogType.Log;

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, format, args);
            }
        }

        public void LogException(Exception exception, Object context)
        {
            if (logEnabled && (filterLogType == LogType.Exception || filterLogType == LogType.Error))
            {
                logHandler.LogException(exception, context);
            }
        }

        public bool IsLogTypeAllowed(LogType logType)
        {
#if RAGDOLL_DEBUG
            return logEnabled && (logType <= filterLogType || logType == LogType.Warning || logType == LogType.Error);
#else
            return logEnabled && (logType == LogType.Warning || logType == LogType.Error);
#endif
        }

        public void Log(LogType logType, object message)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, "{0}", message);
            }
        }

        public void Log(LogType logType, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, "{0}", message);
            }
        }

        public void Log(LogType logType, string tag, object message)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, "{0}: {1}", tag, message);
            }
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, "{0}: {1}", tag, message);
            }
        }

        public void Log(object message)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, null, "{0}", message);
            }
        }

        public void Log(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, null, "{0}: {1}", tag, message);
            }
        }

        public void Log(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, context, "{0}: {1}", tag, message);
            }
        }

        public void LogWarning(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, null, "{0}: {1}", tag, message);
            }
        }

        public void LogWarning(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, context, "{0}: {1}", tag, message);
            }
        }

        public void LogError(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Error))
            {
                logHandler.LogFormat(LogType.Error, null, "{0}: {1}", tag, message);
            }
        }

        public void LogError(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Error))
            {
                logHandler.LogFormat(LogType.Error, context, "{0}: {1}", tag, message);
            }
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, format, args);
            }
        }

        public void LogException(Exception exception)
        {
            if (logEnabled && (filterLogType == LogType.Exception || filterLogType == LogType.Error))
            {
                logHandler.LogException(exception, null);
            }
        }
    }
}