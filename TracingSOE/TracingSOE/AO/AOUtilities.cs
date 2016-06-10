using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.NetworkAnalysis;
using ESRI.ArcGIS.SOESupport;
using GLC.AO.GeometricNetwork;

namespace GLC.AO
{
    public class AOUtilities
    {
        public static readonly int ErrorCode = 973;
        const double RadianPerDegree = 0.01745329251994329576923690768489;
        const double QuadraticMeanRadiusOfEarth = 6367453.6344937783740582933718864;
        private static Dictionary<uint, ISpatialReference> SpatialRefMap;
        private static IGeometryServer2 GeomSrv;
        private static IWorkspaceFactory inMemoryWorkspaceFactory;
        static AOUtilities()
        {
            SpatialRefMap = new Dictionary<uint, ISpatialReference>();
            GeomSrv = new GeometryServerClass();
            inMemoryWorkspaceFactory = new InMemoryWorkspaceFactoryClass();
        }
        public static void Dispose()
        {
            if (null != inMemoryWorkspaceFactory)
                ReleaseCOMObj(inMemoryWorkspaceFactory);
            if (null != GeomSrv)
                ReleaseCOMObj(GeomSrv);

            if (null != SpatialRefMap)
            {
                foreach (var i in SpatialRefMap.Keys)
                    ReleaseCOMObj(SpatialRefMap[i]);
            }
            
        }
        public static void ReleaseCOMObj(object comobj)
        {
            if (null == comobj) return;
            int refCount = 0;
            do
            {
                refCount = System.Runtime.InteropServices.Marshal.ReleaseComObject(comobj);
            } while (refCount > 0);
        }
        public static bool ReleaseInMemoryWorkspaceMemory(IWorkspace workspace)
        {
            if(null != workspace)
            {
                IDataset dataset = workspace as IDataset;
                if (null != dataset && dataset.CanDelete())
                    dataset.Delete();
            }
            return false;
        }
        public static double LonLatDistHaversine(double lon1, double lat1, double lon2, double lat2)
        {
            double lonArc = Math.Abs(lon2 - lon1) * RadianPerDegree;
            double latArc = Math.Abs(lat2 - lat1) * RadianPerDegree;
            double lonh = Math.Sin(lonArc * 0.5);
            double lath = Math.Sin(latArc * 0.5);
            return 2.0 * QuadraticMeanRadiusOfEarth * Math.Asin(Math.Sqrt(lath * lath + Math.Cos(lat1 * RadianPerDegree) * Math.Cos(lat2 * RadianPerDegree) * lonh * lonh));
        }
        public static Tuple<double, double> GetEstimatedDistInDegree(double lon, double lat, double distance, double offset)
        {
            //https://github.com/antirez/redis/blob/unstable/deps/geohash-int/geohash.h
            double GEO_LAT_MIN = -85.05112878;
            double GEO_LAT_MAX = 85.05112878;
            double GEO_LONG_MIN = -180;
            double GEO_LONG_MAX = 180;
            double OFFSET = 1e-8;
            double ZTOLERANCE = 1e-5;
            if (lat > GEO_LAT_MAX || lat < GEO_LAT_MIN || lon > GEO_LONG_MAX || lon < GEO_LONG_MIN)
                return null;
            double llat, hlat, llon, hlon, midlon, midlat, dist, retDist = 1.1;
            if (lat + 1 < GEO_LAT_MAX)
            {
                hlat = lat + offset;
                llat = lat;
            }
            else
            {
                llat = lat - offset;
                hlat = lat;
            }
            if (lon + 1 < GEO_LONG_MAX)
            {
                hlon = lon + offset;
                llon = lon;
            }
            else
            {
                llon = lon - offset;
                hlon = lon;
            }
            double blat = llat, blon = llon, lastDiff = double.PositiveInfinity, diff = 0;
            while (hlon - llon > ZTOLERANCE && hlat - llat > ZTOLERANCE)
            {
                midlon = llon + (hlon - llon) / 2;
                midlat = llat + (hlat - llat) / 2;
                dist = LonLatDistHaversine(blon, blat, midlon, midlat);
                diff = Math.Abs(dist - distance);
                if (diff < lastDiff)
                {
                    retDist = Math.Max(midlon - blon, midlat - blat);
                    lastDiff = diff;
                }
                if (dist <= distance)
                {

                    llon = midlon + OFFSET;
                    llat = midlat + OFFSET;
                }
                else
                {
                    hlon = midlon - OFFSET;
                    hlat = midlat - OFFSET;
                }
            }
            return new Tuple<double, double>(retDist, lastDiff);
        }
        public static ISpatialReference GetSpatialReference(uint epsg)
        {
            if (epsg > 0)
            {
                if (false == SpatialRefMap.ContainsKey(epsg))
                    SpatialRefMap[epsg] = GeomSrv.FindSRByWKID("EPSG", (int)epsg, -1, true, true);
                return SpatialRefMap[epsg];
            }
            return null;
        }
        public static ISpatialReference GetFeatureClassSpatialReference(IFeatureClass featureClass, bool cloned)
        {
            if(null != featureClass)
            {
                int shapeFieldIndex = featureClass.FindField(featureClass.ShapeFieldName);
                IField shapeField = featureClass.Fields.get_Field(shapeFieldIndex);

                // Get the geometry definition from the shape field and clone it.
                IGeometryDef geometryDef = shapeField.GeometryDef;
                if (true == cloned)
                {
                    IClone geometryDefClone = (IClone)geometryDef;
                    return geometryDef.SpatialReference;
                }
                else
                    return geometryDef.SpatialReference;
            }
            return null;
        }
        public static IGeometryDefEdit CreateGeometryDef(esriGeometryType type, int epsg)
        {
            if (epsg > 0)
            {
                IGeometryDefEdit geomDef = new GeometryDefClass();
                geomDef.GeometryType_2 = type;
                geomDef.SpatialReference_2 = GetSpatialReference((uint)epsg);
                return geomDef;
            }
            return null;
        }
        public static IFieldsEdit GetBareMetalFields(esriGeometryType type, int epsg, List<MyFieldDef> extraFields, string oidFieldName = "OID", string shapeFieldName = "Shape")
        {
            int len = null == extraFields ? 0 : extraFields.Count;
            int count = 0;
            IFieldsEdit flds = new FieldsClass();
            flds.FieldCount_2 = 2 + len;

            IFieldEdit fld = new FieldClass();
            fld.Name_2 = oidFieldName;
            fld.Type_2 = esriFieldType.esriFieldTypeOID;
            flds.set_Field(count++, fld);

            fld = new FieldClass();
            fld.Name_2 = shapeFieldName;
            fld.Type_2 = esriFieldType.esriFieldTypeGeometry;
            fld.GeometryDef_2 = CreateGeometryDef(type, epsg);
            flds.set_Field(count++, fld);
            
            if(null != extraFields)
            {
                foreach(var fd in extraFields)
                {
                    //let the exception throw if the following conditions are not met
                    //Otherwise, user may take granted and cause confusion if the missing fields fail the following operations
                    //if (null != fd && null != fd.Name && null != fd.Type)
                    {
                        fld = new FieldClass();
                        fld.Name_2 = fd.Name;
                        fld.Type_2 = fd.Type;
                        flds.set_Field(count++, fld);
                    }
                }
            }
            return flds;
        }
        public static IWorkspaceName GetInMemoryWorkspaceName(string name)
        {
            if (null != inMemoryWorkspaceFactory && false == string.IsNullOrWhiteSpace(name))
                return inMemoryWorkspaceFactory.Create(null, name, null, 0);
            else
                return null;
        }
        public static InMemoryWorkspaceFactory GetInMemoryWorkspaceFactory()
        {
            return AOUtilities.inMemoryWorkspaceFactory as InMemoryWorkspaceFactory;
        }
        public static int GetFieldIndexByName(IFeatureClass featureClass, string fieldName)
        {
            int ret = -1;
            if (null != featureClass && false == string.IsNullOrWhiteSpace(fieldName))
                ret = featureClass.Fields.FindField(fieldName);
            return ret;
        }
        public static Dictionary<string, Tuple<int, esriFieldType>> GetFieldIndexes(IFeatureClass featureClass)
        {
            Dictionary<string, Tuple<int, esriFieldType>> dict = null;
            if(null != featureClass)
            {
                int cnt = featureClass.Fields.FieldCount;
                if (cnt > 0)
                {
                    dict = new Dictionary<string, Tuple<int, esriFieldType>>();
                    IFields flds = featureClass.Fields;
                    IField fld = null;
                    for (int i = 0; i < cnt; ++i)
                    {
                        fld = flds.get_Field(i);
                        dict.Add(fld.Name, new Tuple<int, esriFieldType>(i, fld.Type));
                    }
                }
            }
            return dict;
        }
        public static IGeometryArray TransfromGeometriesFrom2(uint epsgFrom, uint epsgTo, IGeometryArray geomArr)
        {
            IGeometryArray ret = null;
            if(null != geomArr && geomArr.Count > 0)
            {
                try
                {
                    if (false == SpatialRefMap.ContainsKey(epsgFrom))
                        SpatialRefMap[epsgFrom] = GeomSrv.FindSRByWKID("EPSG", (int)epsgFrom, -1, true, true);
                    if (false == SpatialRefMap.ContainsKey(epsgTo))
                        SpatialRefMap[epsgTo] = GeomSrv.FindSRByWKID("EPSG", (int)epsgTo, -1, true, true);
                    /*
                     * The SpatialReference property for all returned geometries will be null.  It is the consumers responsibility to assign the 
                     * spatial reference to each geometry returned, if desired.  In this case, the spatial reference is assumed to be the output 
                     * spatial reference defined for the Project operation.  
                     */
                    ret = GeomSrv.Project(SpatialRefMap[epsgFrom], SpatialRefMap[epsgTo], esriTransformDirection.esriTransformForward, null, null, geomArr);
                }
                catch { throw; }
            }
            return ret;
        }
        /*
         * Tuple<int, IFeature, double>
         * int -- FID of the nearest feature on the target feature class
         * IFeature -- feature itself
         * double -- distance from the point
         */
        public static Tuple<int, IFeature, double> FindNearestFeature(IPoint pnt, IFeatureClass targetFeatureClass, double tolerance, ServerLogger logger)
        {
            if (null != targetFeatureClass && null != pnt && false == pnt.IsEmpty && tolerance > 0.0)
            {
                IFeatureCursor ftrCursor = null;
                double minDist = double.PositiveInfinity;
                try
                {
                    ISpatialFilter sptFilter = new SpatialFilterClass();
                    sptFilter.GeometryField = targetFeatureClass.ShapeFieldName;
                    sptFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                    ITopologicalOperator topoOperator = pnt as ITopologicalOperator;
                    sptFilter.Geometry = topoOperator.Buffer(tolerance);
                    //recycle should be false if you expect a loop against the cursor
                    //otherwise, the feature field inside the cursor will be reused so the reference
                    //you retrieve from the cursor will be changed slicently from your perspective
                    //http://help.arcgis.com/en/sdk/10.0/arcobjects_net/conceptualhelp/index.html#/d/000100000047000000.htm
                    ftrCursor = targetFeatureClass.Search(sptFilter, false);
                    IFeature ftr = null;
                    IFeature minDistftr = null;
                    IProximityOperator proxOperator = null;
                    while ((ftr = ftrCursor.NextFeature()) != null)
                    {
                        proxOperator = ftr.Shape as IProximityOperator;
                        double curDist = proxOperator.ReturnDistance(pnt);
                        if (curDist < minDist)
                        {
                            minDistftr = ftr;
                            minDist = curDist;
                        }
                    }
                    if (null != minDistftr)
                    {
                        proxOperator = minDistftr.Shape as IProximityOperator;
                        IPoint geom = proxOperator.ReturnNearestPoint(pnt, esriSegmentExtension.esriNoExtension);//ftr.ShapeCopy;
                        return new Tuple<int, IFeature, double>(minDistftr.OID, minDistftr, minDist);
                    }
                    return null;
                }
                catch(Exception e)
                {
                    if (null != logger)
                        logger.LogMessage(ServerLogger.msgType.error, typeof(AOUtilities).Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, ErrorCode, e.Message);
                }
                finally
                {
                    ReleaseCOMObj(ftrCursor);
                }
            }
            return null;
        }
        public static IRecordSet2 ConvertToRecordset(IFeatureClass featureClass, IQueryFilter2 queryFilter)
        {
            if (null != featureClass)
            {
                IRecordSet recordSet = new RecordSetClass();
                IRecordSetInit recordSetInit = recordSet as IRecordSetInit;
                recordSetInit.SetSourceTable(featureClass as ITable, queryFilter);
                return (IRecordSet2)recordSetInit;
            }
            else
                return null;
        }
        public static IRecordSet GetRecordSetFromFeatureClass(IFeatureClass featureClass, List<int> fids, List<string> fields, uint epsg)
        {
            if(null != featureClass)
            {
                IQueryFilter2 queryFilter = new QueryFilterClass();
                queryFilter.AddField(featureClass.OIDFieldName);
                queryFilter.AddField(featureClass.ShapeFieldName);
                if (null != fields)
                    foreach (var fld in fields)
                        queryFilter.AddField(fld);
                ISpatialReference srs = GetSpatialReference(epsg);
                if (null != srs)
                    queryFilter.set_OutputSpatialReference(featureClass.ShapeFieldName, srs);
                queryFilter.WhereClause = featureClass.OIDFieldName + " IN (" + string.Join(",", System.Array.ConvertAll<int, string>(fids.ToArray(), s => s.ToString(System.Globalization.CultureInfo.InvariantCulture))) + ")";
                return ConvertToRecordset(featureClass, queryFilter);
            }
            return null;
        }
        public static List<int> StreamTrace(IGeometricNetwork geometricNetwork, StartFlagEdge edge, List<int> disabledFeatureClassIds, StopperJunctions stoppers, bool isUpStream, int maxFeatureCount, ServerLogger logger)
        {
            esriFlowMethod direction = isUpStream ? esriFlowMethod.esriFMUpstream : esriFlowMethod.esriFMDownstream;
            if (null == geometricNetwork || null == edge || maxFeatureCount<= 0)
                return null;
            ITraceFlowSolverGEN traceFlowSolver = new TraceFlowSolverClass() as ITraceFlowSolverGEN;
            INetSolver netSolver = traceFlowSolver as INetSolver;
            netSolver.SourceNetwork = geometricNetwork.Network;
            INetFlag netFlag = new EdgeFlagClass();
            netFlag.UserClassID = edge.FeatureClassID;
            netFlag.UserID = edge.FeatureID;
            //no idea when to assign -1, when to do 0
            netFlag.UserSubID = -1;
            traceFlowSolver.PutEdgeOrigins(new IEdgeFlag[1] { netFlag as IEdgeFlag });
            if (null != disabledFeatureClassIds)
            {
                foreach(int il in disabledFeatureClassIds)
                    if(il > 0)
                        netSolver.DisableElementClass(il);
            }
            if (null != stoppers && null != stoppers.Stoppers && stoppers.Stoppers.Length > 0)
            {
                INetElementBarriersGEN netBarriersGEN = null;
                netBarriersGEN = new NetElementBarriersClass() as INetElementBarriersGEN;
                netBarriersGEN.ElementType = esriElementType.esriETJunction;
                netBarriersGEN.Network = geometricNetwork.Network;
                netBarriersGEN.SetBarriers(stoppers.FeatureClassID, stoppers.Stoppers);
                netSolver.set_ElementBarriers(esriElementType.esriETJunction, netBarriersGEN as INetElementBarriers);
            }
            
            IEnumNetEID junctionEIDs = null;
            IEnumNetEID edgeEIDs = null;
            traceFlowSolver.TraceIndeterminateFlow = false;
            try
            {
                traceFlowSolver.FindFlowElements(direction, esriFlowElements.esriFEEdges, out junctionEIDs, out edgeEIDs);
                if (null != edgeEIDs)
                {
                    if (edgeEIDs.Count <= maxFeatureCount)
                    {
                        IEIDHelper eidHelper = new EIDHelperClass();
                        eidHelper.GeometricNetwork = geometricNetwork;
                        eidHelper.ReturnGeometries = false;
                        eidHelper.ReturnFeatures = true;
                        //eidHelper.AddField("FType");
                        IEnumEIDInfo eidInfos = eidHelper.CreateEnumEIDInfo(edgeEIDs);
                        eidInfos.Reset();
                        IEIDInfo eidInfo = null;
                        List<int> ftrs = new List<int>();
                        //IFeature cadFtr = null;
                        //int ftype;
                        while ((eidInfo = eidInfos.Next()) != null)
                        {
                            ftrs.Add(eidInfo.Feature.OID);
                            /*cadFtr = eidInfo.Feature;
                            if (null != cadFtr.get_Value(edgeTypeId) && int.TryParse(cadFtr.get_Value(edgeTypeId).ToString(), out ftype))
                            {
                                if(460 == ftype || 558 == ftype)
                                    ftrs.Add(cadFtr);
                            }*/
                        }
                        return ftrs;
                    }
                }
            }
            catch (Exception e)
            {
                if(null != logger)
                    logger.LogMessage(ServerLogger.msgType.error, typeof(AOUtilities).Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, ErrorCode, e.Message);
            }
            finally
            {
                ReleaseCOMObj(traceFlowSolver);
            }
            return null;
        }
    }
}
