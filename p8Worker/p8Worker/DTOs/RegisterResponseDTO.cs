using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.DTOs;

public class RegisterResponseDTO
{
    public string WorkerId { get; set; }
    public string ServerName { get; set; }
    public string LANIp { get; set; }

    public RegisterResponseDTO(string workerId, string serverName, string lanIp)
    {
        WorkerId = workerId;
        ServerName = serverName;
        LANIp = lanIp;
    }
}
