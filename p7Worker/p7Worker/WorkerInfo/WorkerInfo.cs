using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace p7Worker.WorkerInfo
{
    public static class WorkerInfo
    {
        public static string GetWorkerId()
        {
            using StreamReader r = new("config.json");
            string json = r.ReadToEnd();
            WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);
            return info.WorkerId;
        }

        public static string GetServerName()
        {
            using StreamReader r = new("config.json");
            string json = r.ReadToEnd();
            WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);
            return info.ServerName;
        }
    }
}
