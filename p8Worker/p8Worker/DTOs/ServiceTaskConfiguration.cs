using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.DTOs
{
    internal class ServiceTaskConfiguration
    {
       public TimeSpan ShutOffDelay {get;set;}
        public  TimeSpan CheckPointInterval { get; set;}
    }
}
