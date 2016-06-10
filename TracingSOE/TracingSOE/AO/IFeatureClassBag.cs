using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace GLC.AO
{
    public interface IFeatureClassBag : IDisposable
    {
        bool CreateFeatureClass(string featureClassName, IFields fieldDefs, esriFeatureType featureType, string geomFieldName);
        bool CreateFeatureClass(IFeatureClass sourceFeatureClass, string newName);
        bool AddFeatures(string featureClassName, List<MyFeature> features, int extraFieldCount);
        bool IsInited { get; }
        bool IsFeatureClassExisted(string featureClassName);
        int GetFeatureCountInFeatureClass(string featureClassName);
        System.Collections.Generic.IEnumerable<IFeature> GetFeatures(string featureClassName, bool allowFeatureRecycle);
    }
}
