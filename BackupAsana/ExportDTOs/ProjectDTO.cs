using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupAsana.ExportDTOs
{
    public class ProjectDTO
    {
        public long ProjectID { get; set; }
        public string Name { get; set; }

        public List<TaskDTO> Tasks { get; set; }

        public ProjectDTO()
        {
            Tasks = new List<TaskDTO>();
        }
    }
}
