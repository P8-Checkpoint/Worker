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
    public string LANId { get; set; }

    public RegisterResponseDTO(string workerId, string serverName, string lanId)
    {
        WorkerId = workerId;
        ServerName = serverName;
        LANId = lanId;
    }
}
