using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.ResourceWarp
{
    public class KnownFile
    {
        public DateTimeOffset lastModifiedTime;
        public StorageFolder folder;
        public StorageFile file;
        public string fullPath;
        public string relativePath;

        public async Task<bool> IsModified()
        {
            try
            {
                var file = await folder.GetFileAsync(relativePath);
                var attr = await file.GetBasicPropertiesAsync();
                bool modified = false;
                if (lastModifiedTime != attr.DateModified)
                {
                    modified = true;
                    this.file = file;
                    lastModifiedTime = attr.DateModified;
                }
                return modified;
            }
            catch(Exception e)
            {
                lastModifiedTime = new DateTimeOffset();
                throw e;
            }
        }
    }
}
