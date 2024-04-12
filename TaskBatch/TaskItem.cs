using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskBatch
{
    public class TaskItem
    {

        public string UserEmail { get; set; }

        public string Description { get; set; }
 
        public string Details { get; set; }

        public DateTime DueDate { get; set; }
 
        public string FrequencyType { get; set; }
  
        public int FrequencyNumber { get; set; }

        public int Sensative { get; set; }
 
        public DateTime? LastCompleted { get; set; }
    }
}
