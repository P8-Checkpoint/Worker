using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.DTOs
{
    public class WorkerReportDTO
    {
        public string WorkerId { get; set; }
        public Guid JobId { get; set; }
        public string LANIp { get; set; }

        public WorkerReportDTO(string workerId, Guid jobId, string lANIp)
        {
            WorkerId = workerId;
            JobId = jobId;
            LANIp = lANIp;
        }
    }
}
