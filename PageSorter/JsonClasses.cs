using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PageSorter
{
    class JsonClasses
    {
        // --
        public class ProjectInfo
        {
            public string project_id { get; set; }
            public string project_name { get; set; }
            public string[] version_groups { get; set; }
            public string[] versions { get; set; }
        }

        // --
        public class VersionInfo
        {
            public string project_id { get; set; }
            public string project_name { get; set; }
            public string version { get; set; }
            public int[] builds { get; set; }
        }

        // --
        public class VersionBuilds
        {
            public string project_id { get; set; }
            public string project_name { get; set; }
            public string version { get; set; }
            public int build { get; set; }
            public DateTime time { get; set; }
            public Change[] changes { get; set; }
            public Downloads downloads { get; set; }
        }

        public class Downloads
        {
            public Application application { get; set; }
        }

        public class Application
        {
            public string name { get; set; }
            public string sha256 { get; set; }
        }

        public class Change
        {
            public string commit { get; set; }
            public string summary { get; set; }
            public string message { get; set; }
        }

    }
}
