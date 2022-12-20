using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7Worker.DTOs;

public class JobStartDTO
{
    public Guid Id { get; set; }
    public string SourcePath { get; set; }
    public string ResultPath { get; set; }
    public string BackupPath { get; set; }
    public JobStartDTO(Guid id, string sourcePath, string resultPath, string backupPath)
    {
        Id = id;
        SourcePath = sourcePath;
        ResultPath = resultPath;
        BackupPath = backupPath;
    }
}
