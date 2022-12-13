using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using p7Worker.DTOs;
using RabbitMQ.Client;

namespace p7Worker
{
    public class Worker
    {
        RabbitMQHandler handler;
        string _serverName;
        string _replyQueueName;

        public Worker()
        {
            Init();
            while (true)
            {
                Thread.Sleep(100);
            }
        }

        void Init()
        {

        }
    }
}
