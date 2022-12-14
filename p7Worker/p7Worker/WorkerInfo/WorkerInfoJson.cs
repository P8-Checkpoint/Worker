using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7Worker.WorkerInfo;

public class WorkerInfoJson
{
    public string WorkerId { get; set; }
    public string ServerName { get; set; }

    public WorkerInfoJson(string workerId, string serverName)
    {
        WorkerId = workerId;
        ServerName = serverName;
    }
}
