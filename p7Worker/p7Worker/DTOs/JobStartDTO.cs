using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7Worker.DTOs
{
    public class JobStartDTO
    {
        public string JobId { get; set; }
        public string FTPLink { get; set; }

        public JobStartDTO(string jobId, string fTPLink)
        {
            JobId = jobId;
            FTPLink = fTPLink;
        }
    }
}
