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
            using StreamReader r = new("WorkerInfo.json");
            string json = r.ReadToEnd();
            WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);
            return info.WorkerId;
        }

        public static string GetServerName()
        {
            using StreamReader r = new("WorkerInfo.json");
            string json = r.ReadToEnd();
            WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);
            return info.ServerName;
        }

        public static void SetWorkerInfo(WorkerInfoJson workerInfo)
        {
            string fileName = "WorkerInfo.json";
            string jsonString = JsonSerializer.Serialize(workerInfo);
            File.WriteAllText(fileName, jsonString);
        }
    }
}
