using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupAsana.ExportDTOs
{
    public class TaskActivityDTO
    {
        public long ID { get; set; }
        public DateTime DateActivity { get; set; }
        public long CreatedBy { get; set; }
        public string Type { get; set; }
        public string Text { get; set; }
    }
}
