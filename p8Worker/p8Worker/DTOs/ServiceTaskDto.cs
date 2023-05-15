using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.DTOs;

public class ServiceTaskDto
{
    public Guid Id { get; set; }
    public string SourcePath { get; set; }
    public string ResultPath { get; set; }
    public string BackupPath { get; set; }
    public ServiceTaskDto(Guid id, string sourcePath, string resultPath, string backupPath)
    {
        Id = id;
        SourcePath = sourcePath;
        ResultPath = resultPath;
        BackupPath = backupPath;
    }
}
