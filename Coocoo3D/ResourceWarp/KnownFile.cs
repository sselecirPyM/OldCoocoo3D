using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Coocoo3D.ResourceWarp
{
    public class KnownFile
    {
        public DateTimeOffset lastModifiedTime;
        public FileInfo file;
        public string fullPath;
        public string relativePath;
        public bool requireReload;

        public bool IsModified(FileInfo[] fileInfos)
        {
            try
            {
                var file = fileInfos.Where(u=>u.Name.Equals(relativePath,StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                var attr = file.LastWriteTime;
                bool modified = false;
                if (lastModifiedTime != attr)
                {
                    modified = true;
                    this.file = file;
                    lastModifiedTime = attr;
                }
                return modified;
            }
            catch(Exception e)
            {
                lastModifiedTime = new DateTimeOffset();
                throw;
            }
        }
    }
}
