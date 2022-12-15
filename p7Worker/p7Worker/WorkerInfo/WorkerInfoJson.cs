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
    public string FTPLink { get; set; }

    public WorkerInfoJson(string workerId, string serverName, string ftplink)
    {
        WorkerId = workerId;
        ServerName = serverName;
        FTPLink = ftplink;
    }
}
