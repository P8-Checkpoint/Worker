using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.DTOs;

public static class WorkerInfoDto
{
    public static string WorkerId { get; set; } = string.Empty;
    public static string ServerName { get; set; } = string.Empty;

    public static string JobId { get; set; } = string.Empty;
    public static string ClientId { get; set; } = string.Empty;
    public static string Name { get; set; } = string.Empty;
    public static string Status { get; set; } = string.Empty;
    public static string SourcePath { get; set; } = string.Empty;
    public static string BackupPath { get; set; } = string.Empty;
    public static string ResultPath { get; set; } = string.Empty;
}
