using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupAsana.ExportDTOs
{
    public class TaskDTO
    {
        public long TaskID { get; set; }
        public string Name { get; set; }
        public DateTime DateCreated { get; set; }
        public string Description { get; set; }
        public long? Assignee { get; set; }
        public long? ParentTaskID { get; set; }
        public List<long> Followers { get; set; }
        public bool IsCompleted { get; set; }

        public List<TaskActivityDTO> Activities { get; set; }

        public TaskDTO()
        {
            Activities = new List<TaskActivityDTO>();
            Followers = new List<long>();
           
        }
    }
}
