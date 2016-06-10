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
    public abstract class AbstractFeatureClassBag : IFeatureClassBag
    {
        protected bool disposed = false;
        protected Dictionary<string, IFeatureClass> featureClassMap = new Dictionary<string, IFeatureClass>();
        protected IWorkspaceName workspaceName = null;
        protected IFeatureWorkspace workspace = null;
        protected bool isInited = false;
        public virtual bool IsInited
        {
            get
            {
                return this.isInited;
            }
        }
        protected AbstractFeatureClassBag(string workspaceName, InMemoryWorkspaceFactory factory)
        {
            if (null != factory && false == string.IsNullOrWhiteSpace(workspaceName))
            {
                this.workspaceName = factory.Create(null, workspaceName, null, 0);
                IName name = (IName)this.workspaceName;
                IWorkspace wspace = (IWorkspace)name.Open();
                this.workspace = wspace as IFeatureWorkspace;
                if (null != workspace)
                    this.isInited = true;
            }
        }
        protected void releaseCOMObj(object comobj)
        {
            if (null == comobj) return;
            int refCount = 0;
            do
            {
                refCount = System.Runtime.InteropServices.Marshal.ReleaseComObject(comobj);
            } while (refCount > 0);
        }
        public void Dispose()
        {
            if (this.isInited)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (true == disposed) return;
            if (disposing)
            {
                /*
                 * http://help.arcgis.com/en/sdk/10.0/arcobjects_net/componenthelp/index.html#/InMemoryWorkspaceFactoryClass_Class/001m0000002q000000/
                 *When an in-memory workspace is no longer needed, it is the developer's responsibility to call IDataset.Delete on the workspace to release its memory.
                 * 
                 *I am not sure the memory held by idataset is managed or unmanaged, currently treat it as "unmanaged"
                */
                if (null != workspace && workspace is IDataset)
                {
                    IDataset dataset = workspace as IDataset;
                    if (null != dataset && dataset.CanDelete())
                        dataset.Delete();
                    this.releaseCOMObj(this.workspace);
                    this.releaseCOMObj(this.workspaceName);
                }
            }
            disposed = true;
        }
        //http://help.arcgis.com/en/sdk/10.0/arcobjects_net/conceptualhelp/index.html#/d/00010000028w000000.htm
        protected IGeometryDef CloneGeometryDef(IFeatureClass sourceFeatureClass)
        {
            // Find the shape field.
            int shapeFieldIndex = sourceFeatureClass.FindField(sourceFeatureClass.ShapeFieldName);
            IField shapeField = sourceFeatureClass.Fields.get_Field(shapeFieldIndex);

            // Get the geometry definition from the shape field and clone it.
            IGeometryDef geometryDef = shapeField.GeometryDef;
            IClone geometryDefClone = (IClone)geometryDef;
            return geometryDefClone.Clone() as IGeometryDef;
        }
        protected IFields CloneFields(IWorkspace sourceWorkspace, IFeatureClass sourceFeatureClass, IWorkspace targetWorkspace)
        {
            // Create the objects and references necessary for field validation.
            IFieldChecker fieldChecker = new FieldCheckerClass();
            IFields sourceFields = sourceFeatureClass.Fields;
            IFields targetFields = null;
            IEnumFieldError enumFieldError = null;

            // Set the required properties for the IFieldChecker interface.
            fieldChecker.InputWorkspace = sourceWorkspace;
            fieldChecker.ValidateWorkspace = targetWorkspace;

            // Validate the fields and check for errors.
            fieldChecker.Validate(sourceFields, out enumFieldError, out targetFields);
            if (enumFieldError != null)
            {
                // Handle the errors in a way appropriate to your application.
                throw new ArgumentException("Errors were encountered during field validation.");
            }
            return targetFields;
        }
        public virtual bool IsFeatureClassExisted(string featureClassName)
        {
            if (false == string.IsNullOrWhiteSpace(featureClassName))
                return true == this.featureClassMap.ContainsKey(featureClassName) && null != this.featureClassMap[featureClassName];
            return false;
        }
        public virtual int GetFeatureCountInFeatureClass(string featureClassName)
        {
            if (false == string.IsNullOrWhiteSpace(featureClassName) && true == this.featureClassMap.ContainsKey(featureClassName) && null != this.featureClassMap[featureClassName])
                return this.featureClassMap[featureClassName].FeatureCount(null);
            else
                return -1;
        }
        public virtual IEnumerable<IFeature> GetFeatures(string featureClassName, bool allowFeatureRecycle)
        {
            if (false == string.IsNullOrWhiteSpace(featureClassName) && true == this.featureClassMap.ContainsKey(featureClassName) && null != this.featureClassMap[featureClassName])
            {
                IFeatureClass featureClass = this.featureClassMap[featureClassName];
                IFeatureCursor featureCursor = featureClass.Search(null, allowFeatureRecycle);
                if (null != featureCursor)
                {
                    //https://msdn.microsoft.com/en-us/library/9k7k7cf0.aspx
                    /*
                     * A yield return statement can't be located in a try-catch block. A yield return statement can be located in the try block of a try-finally statement.
                     * A yield break statement can be located in a try block or a catch block but not a finally block.
                     */
                    try
                    {
                        IFeature feature = null;
                        while ((feature = featureCursor.NextFeature()) != null)
                            yield return feature;
                    }
                    finally
                    {
                        this.releaseCOMObj(featureCursor);
                    }
                }
            }

        }
        public abstract bool CreateFeatureClass(string featureClassName, IFields fieldDefs, esriFeatureType featureType, string geomFieldName);
        public abstract bool CreateFeatureClass(IFeatureClass sourceFeatureClass, string newName);
        public abstract bool AddFeatures(string featureClassName, List<MyFeature> features, int extraFieldCount);
    }
}
