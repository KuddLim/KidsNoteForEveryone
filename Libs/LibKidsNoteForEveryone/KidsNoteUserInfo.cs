using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteParentInfo
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
    }

    public class KidsNoteChildEnrollment
    {
        public int Id { get; set; }
        public int CenterId { get; set; }
        public string CenterName { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; }
    }

    public class KidsNoteChildInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }

        public List<KidsNoteChildEnrollment> Enrollments { get; set; }

        public KidsNoteChildInfo()
        {
            Enrollments = new List<KidsNoteChildEnrollment>();
        }
    }

    public class KidsNoteUserInfo
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public List<KidsNoteChildInfo> Children { get; set; }

        public KidsNoteUserInfo()
        {
            Children = new List<KidsNoteChildInfo>();
        }
    }
}
