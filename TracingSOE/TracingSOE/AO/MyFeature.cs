using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace GLC.AO
{
    public class MyFeature
    {
        private esriFeatureType featureType;
        public esriFeatureType FeatureType
        {
            get { return this.featureType; }
        }

        private esriGeometryType geomType;
        public esriGeometryType GeometryType
        {
            get { return this.geomType; }
        }

        private IGeometry geometry;
        public IGeometry Geometry
        {
            get { return this.geometry; }
            set
            {
                if (geomType == value.GeometryType)
                    this.geometry = value;
            }
        }

        private int refId;
        public int ReferenceID
        {
            get { return this.refId; }
        }

        private List<object> attrs;
        public List<object> Attributes
        {
            get { return this.attrs; }
        }

        public MyFeature(esriFeatureType featureType, esriGeometryType geomType, IGeometry geom, int refid, List<object> extraAttr)
        {
            this.featureType = featureType;
            this.geomType = geomType;
            this.geometry = geom;
            this.refId = refid;
            this.attrs = extraAttr;
        }
    }
}
