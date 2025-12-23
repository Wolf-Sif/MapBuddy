using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace MapBuddy
{
    internal class Logger
    {
        public string log;

        private StringBuilder debugLogBuilder;
        private StringBuilder oldLogBuilder;
        private StringBuilder idLogBuilder;
        private StringBuilder csvLogBuilder;

        // When true, debugLog will be written to disk in WriteLog().
        public static bool PrintDebug { get; set; } = false;

        public Logger()
        {
            log = "";
            debugLogBuilder = new StringBuilder();
            oldLogBuilder = new StringBuilder();
            idLogBuilder = new StringBuilder();
            csvLogBuilder = new StringBuilder();
        }

        public void WriteLog()
        {
            string logDir = GetLogDir();

            bool exists = Directory.Exists(logDir);

            if (!exists)
            {
                Directory.CreateDirectory(logDir);
            }

            if (debugLogBuilder.Length > 0 && PrintDebug)
                File.AppendAllText(Path.Combine(logDir, "debugLog.txt"), debugLogBuilder.ToString()); ;

            if (oldLogBuilder.Length > 0)
                File.AppendAllText(Path.Combine(logDir, "oldLog.txt"), oldLogBuilder.ToString());

            if (idLogBuilder.Length > 0)
                File.AppendAllText(Path.Combine(logDir, "idLog.txt"), idLogBuilder.ToString());

            if (csvLogBuilder.Length > 0)
                File.AppendAllText(Path.Combine(logDir, "csvLog.csv"), csvLogBuilder.ToString());

            debugLogBuilder.Clear();
            oldLogBuilder.Clear();
            idLogBuilder.Clear();
            csvLogBuilder.Clear();
            log = "";
        }

        public void AddToLog(string text)
        {
            log = log + text + "\n";
            oldLogBuilder.AppendLine(text);
        }
        public void AddToDebugLog(string text)
        {
            log = log + text + "\n";
            debugLogBuilder.AppendLine(text);
        }

        public void AddToIdLog(string text)
        {
            log = log + text + "\n";
            idLogBuilder.AppendLine(text);
        }

        public void AddToCsvLog(string entityName, ulong id, string map)
        {
            if (entityName == null) entityName = "";
            var m = Regex.Match(entityName, @"c\d+");
            string key = m.Success ? m.Value : entityName;
            csvLogBuilder.AppendLine($"{key},{id},{map}");
        }

        public string GetLogDir()
        {
            return Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Log");
        }
    }
}
