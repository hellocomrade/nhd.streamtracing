using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.Geodatabase;

namespace GLC.AO
{
    public class MyFieldDef
    {
        private string name;
        public string Name
        { 
            get
            {
                return this.name;
            }
        }

        private esriFieldType type;
        public esriFieldType Type
        { 
            get
            {
                return this.type;
            }
        }

        public MyFieldDef(esriFieldType type, string name)
        {
            this.type = type;
            this.name = name;
        }
    }
}
