using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesGDB;

namespace GLC.AO
{
    public class InMemoryFeatureClassBag : AbstractFeatureClassBag
    {
        public InMemoryFeatureClassBag(string workspaceName, InMemoryWorkspaceFactory factory)
            : base(workspaceName, factory)
        {}
        public override bool CreateFeatureClass(string featureClassName, IFields fieldDefs, esriFeatureType featureType, string geomFieldName)
        {
            if (this.isInited && false == string.IsNullOrWhiteSpace(featureClassName))
            {
                if (false == this.featureClassMap.ContainsKey(featureClassName))
                {
                    IFeatureClass ftrc = this.workspace.CreateFeatureClass(featureClassName, fieldDefs, null, null, esriFeatureType.esriFTSimple, geomFieldName, null);
                    if (null != ftrc)
                    {
                        this.featureClassMap.Add(featureClassName, ftrc);
                        return true;
                    }
                }
            }
            return false;
        }
        public override bool CreateFeatureClass(IFeatureClass sourceFeatureClass, string newName)
        {
            if (null != sourceFeatureClass && (false == this.featureClassMap.ContainsKey(newName) || null == this.featureClassMap[newName]))
            {
                if (null != sourceFeatureClass.FeatureDataset)
                {
                    IWorkspace sourceWorkspace = sourceFeatureClass.FeatureDataset.Workspace;
                    if (null != sourceWorkspace)
                    {
                        /*
                         * The WorkspaceName for a workspace can be persisted, for example, in a map document.
                         * An application can call the Open method on the workspace name after loading it 
                         * from persistent storage in order to connect to and get an object reference to the 
                         * workspace.  A WorkspaceName name object can be returned from a workspace through 
                         * the use of IDataset.FullName.
                        */
                        IFields fields = this.CloneFields(sourceWorkspace, sourceFeatureClass, this.workspace as IWorkspace);
                        return this.CreateFeatureClass(newName, fields, sourceFeatureClass.FeatureType, sourceFeatureClass.ShapeFieldName);
                    }
                }
                
            }
            return false;
        }
        public override bool AddFeatures(string featureClassName, List<MyFeature> features, int extraFieldCount)
        {
            if(this.featureClassMap.ContainsKey(featureClassName) && null != this.featureClassMap[featureClassName] && null != features && features.Count > 0)
            {
                IFeatureBuffer featureBuffer = this.featureClassMap[featureClassName].CreateFeatureBuffer();
                IFeatureCursor featureCursor = this.featureClassMap[featureClassName].Insert(true);
                try
                {
                    int count = 0;
                    foreach(var feature in features)
                    {
                        if (null != feature && feature.GeometryType == this.featureClassMap[featureClassName].ShapeType)
                        {
                            featureBuffer.Shape = feature.Geometry;
                            /*
                             * Index for extra fields start from 2
                             * Index 0 is OID
                             * Index 1 is geometry
                            */
                            if (extraFieldCount > 0)
                            {
                                if (null == feature.Attributes && extraFieldCount != feature.Attributes.Count)
                                    throw new ArgumentException("Extra field doesn't exist or it's count doesn't match with the specified value in the method's argument.");
                                for (int i = 0, fldIdx = 2; i < extraFieldCount; ++i, ++fldIdx)
                                {
                                    featureBuffer.set_Value(fldIdx, feature.Attributes[i]);
                                }
                            }
                            if (null != featureCursor.InsertFeature(featureBuffer))
                                ++count;
                        }
                        else
                            throw new ArgumentException("Feature is null or its geometry type doesn't match feature class specified.");
                    }
                    featureCursor.Flush();
                    return count == features.Count;
                }
                finally
                {
                    this.releaseCOMObj(featureCursor);
                }
            }
            return false;
        }
    
    }
}
