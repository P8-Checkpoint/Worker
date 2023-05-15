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
        public string LANId { get; set; }

        public WorkerReportDTO(string workerId, Guid jobId, string lANId)
        {
            WorkerId = workerId;
            JobId = jobId;
            LANId = lANId;
        }
    }
}
