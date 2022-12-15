﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7Worker.DTOs;

public class RegisterResponseDTO
{
    public string WorkerId { get; set; }
    public string ServerName { get; set; }

    public RegisterResponseDTO(string workerId, string serverName, string ftpLink)
    {
        WorkerId = workerId;
        ServerName = serverName;
        ftpLink = ftpLink;

    }
}
