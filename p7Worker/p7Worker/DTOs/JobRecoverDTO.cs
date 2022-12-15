using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7Worker.DTOs;

public class JobRecoverDTO
{
    public string JobId { get; set; }
    public string FTPLink { get; set; }
    public string SourcePath { get; set; }
    public string ResultPath { get; set; }
    public string BackupPath { get; set; }
    public JobRecoverDTO(string jobId, string fTPLink, string sourcePath, string resultPath, string backupPath)
    {
        JobId = jobId;
        FTPLink = fTPLink;
        SourcePath = sourcePath;
        ResultPath = resultPath;
        BackupPath = backupPath;
    }
}
