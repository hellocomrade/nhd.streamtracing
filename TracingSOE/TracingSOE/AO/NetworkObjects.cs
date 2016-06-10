using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SOESupport;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.NetworkAnalysis;

namespace GLC.AO.GeometricNetwork
{
    public class NetworkContext
    {
        private Dictionary<string, IFeatureClass> edgesMap = null;
        private Dictionary<string, IFeatureClass> junctionsMap = null;
        
        private bool isLoaded = false;
        public bool IsNetworkLoaded
        {
            get
            {
                return this.isLoaded;
            }
        }

        private IGeometricNetwork geometricNetwork = null;
        public IGeometricNetwork GeometricNetwork
        {
            get
            {
                return this.geometricNetwork;
            }
        }
        private string networkName;
        public string NetworkName
        {
            get
            {
                return this.networkName;
            }
        }

        private int edgeCount = 0;
        public int EdgeCount
        {
            get
            {
                return this.edgeCount;
            }
        }

        private int junctionCount = 0;
        public int JunctionCount
        {
            get
            {
                return this.junctionCount;
            }
        }
        public NetworkContext(string networkName, List<string> edgeFeatureClassAliasNames, List<string> junctionFeatureClassAliasName)
        {
            this.networkName = networkName;
            if(null != edgeFeatureClassAliasNames && edgeFeatureClassAliasNames.Count > 0)
            {
                edgesMap = new Dictionary<string,IFeatureClass>();
                foreach(var name in edgeFeatureClassAliasNames)
                    edgesMap[name] = null;
            }
            if(null != junctionFeatureClassAliasName && junctionFeatureClassAliasName.Count > 0)
            {
                junctionsMap = new Dictionary<string,IFeatureClass>();
                foreach(var name in junctionFeatureClassAliasName)
                    junctionsMap[name] = null;
            }
        }
        public IFeatureClass GetEdgeFeatureClassByAliasName(string name)
        {
            IFeatureClass ret = null;
            if (false == string.IsNullOrEmpty(name) && this.edgeCount > 0 && this.edgesMap.ContainsKey(name))
                ret = this.edgesMap[name];
            return ret;
        }
        public IFeatureClass GetJunctionFeatureClassIdByAliasName(string name)
        {
            IFeatureClass ret = null;
            if (false == string.IsNullOrEmpty(name) && this.junctionCount > 0 && this.junctionsMap.ContainsKey(name))
                ret = this.junctionsMap[name];
            return ret;
        }
        private bool addEdgeFeatureClass(IFeatureClass featureClass)
        {
            if (null != featureClass && esriFeatureType.esriFTSimpleEdge == featureClass.FeatureType && true == this.edgesMap.ContainsKey(featureClass.AliasName))
            {
                this.edgesMap[featureClass.AliasName] = featureClass;
                ++this.edgeCount;
                return true;
            }
            return false;
        }
        private bool addJunctionFeatureClass(IFeatureClass featureClass)
        {
            if (null != featureClass && esriFeatureType.esriFTSimpleJunction == featureClass.FeatureType && true == this.junctionsMap.ContainsKey(featureClass.AliasName))
            {
                this.junctionsMap[featureClass.AliasName] = featureClass;
                ++this.junctionCount;
                return true;
            }
            return false;
        }
        private bool checkEdgesAndJunctions()
        {
            foreach (var k in this.edgesMap.Keys)
            {
                if (null == this.edgesMap[k])
                    return false;
            }
            foreach (var k in this.junctionsMap.Keys)
            {
                if (null == this.junctionsMap[k])
                    return false;
            }
            return true;
        }
        public bool LoadGeometricNetwork(string path, IServerObjectHelper serverObjectHelper, ServerLogger logger)
        {
            if (false == this.isLoaded)
            {
                this.isLoaded = path != null ? this.loadGeometricNetworkFromPath(path, logger) : this.loadGeometricNetworkFromServer(serverObjectHelper, logger);
                this.isLoaded = this.isLoaded && this.checkEdgesAndJunctions();
                return this.isLoaded;
            }
            else
                throw new ArgumentException("Can not load network on one context more than once.");
        }
        private bool loadGeometricNetworkFromServer(IServerObjectHelper serverObjectHelper, ServerLogger logger)
        {
            bool result = false;
            if(null != serverObjectHelper)
            {
                try
                {
                    IMapServer3 mapServer = (IMapServer3)serverObjectHelper.ServerObject;
                    IMapServerDataAccess da = (IMapServerDataAccess)mapServer;
                    IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapServer.DefaultMapName).MapLayerInfos;
                    IFeatureDataset ftrDataset = null;
                    for (int i = 0; i < layerInfos.Count; i++)
                    {
                        IMapLayerInfo lyrInfo = layerInfos.get_Element(i);
                        if (lyrInfo.IsFeatureLayer)
                        {
                            IFeatureClass ftrClass = (IFeatureClass)da.GetDataSource(mapServer.DefaultMapName, lyrInfo.ID);
                            if (null == ftrDataset && ftrClass.FeatureDataset.Name == this.networkName) ftrDataset = ftrClass.FeatureDataset;
                            if (esriFeatureType.esriFTSimpleEdge == ftrClass.FeatureType)
                                this.addEdgeFeatureClass(ftrClass);
                            else if (esriFeatureType.esriFTSimpleJunction == ftrClass.FeatureType)
                                this.addJunctionFeatureClass(ftrClass);
                        }
                    }
                    if (this.edgeCount > 0 && this.junctionCount > 0 && null != ftrDataset)
                    {
                        INetworkCollection networkCollection = ftrDataset as INetworkCollection;
                        if (networkCollection != null && networkCollection.GeometricNetworkCount > 0)
                        {
                            this.geometricNetwork = networkCollection.GeometricNetwork[0];
                            result = true;
                        }
                    }
                }
                catch(Exception e)
                {
                    if (null != logger)
                        logger.LogMessage(ServerLogger.msgType.error, typeof(NetworkHelper).Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, GLC.AO.AOUtilities.ErrorCode, e.Message);
                }
            }
            return result;
        }
        private bool loadGeometricNetworkFromPath(string path, ServerLogger logger)
        {
            bool result = false;
            if (true == System.IO.Directory.Exists(path))
            {
                IWorkspaceFactory workspaceFactory = null;
                IWorkspace workspace = null;
                IFeatureDataset ftrDs = null;
                try
                {
                    Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.FileGDBWorkspaceFactory");
                    workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);
                    workspace = workspaceFactory.OpenFromFile(path, 0);
                    IFeatureWorkspace ftrWorkspace = workspace as IFeatureWorkspace;
                    ftrDs = ftrWorkspace.OpenFeatureDataset(this.networkName);
                    IFeatureClassContainer fcContainer = ftrDs as IFeatureClassContainer;
                    IFeatureClass fc = null;
                    for (int i = 0; i < fcContainer.ClassCount; ++i)
                    {
                        fc = fcContainer.get_Class(i);
                        if (esriFeatureType.esriFTSimpleEdge == fc.FeatureType)
                            this.addEdgeFeatureClass(fc);
                        else if (esriFeatureType.esriFTSimpleJunction == fc.FeatureType)
                            this.addJunctionFeatureClass(fc);
                    }
                    if (this.edgeCount > 0 && this.junctionCount > 0)
                    {
                        INetworkCollection networkCollection = ftrDs as INetworkCollection;
                        if (null != networkCollection && 0 < networkCollection.GeometricNetworkCount)
                        {
                            this.geometricNetwork = networkCollection.GeometricNetwork[0];
                            result = true;
                        }
                    }
                }
                catch(Exception e)
                {
                    if (null != logger)
                        logger.LogMessage(ServerLogger.msgType.error, typeof(NetworkHelper).Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, GLC.AO.AOUtilities.ErrorCode, e.Message);
                }
                finally
                {
                    GLC.AO.AOUtilities.ReleaseCOMObj(ftrDs);
                    GLC.AO.AOUtilities.ReleaseCOMObj(workspace);
                    GLC.AO.AOUtilities.ReleaseCOMObj(workspaceFactory);
                }
            }
            else
            {
                if (null != logger)
                    logger.LogMessage(ServerLogger.msgType.error, typeof(NetworkHelper).Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, GLC.AO.AOUtilities.ErrorCode, "File geodatabase does not exist at " + path);
            }
            return result;
        }
    }
    public class StartFlagEdge
    {
        public int FeatureClassID{ get; set;}
        public int FeatureID { get; set; }
        public StartFlagEdge(IFeatureClass featureClass, int featureID)
        {
            if(null != featureClass && esriFeatureType.esriFTSimpleEdge == featureClass.FeatureType && featureID > 0)
            {
                this.FeatureClassID = featureClass.FeatureClassID;
                this.FeatureID = featureID;
            }
            else
            {
                this.FeatureClassID = -1;
                this.FeatureID = -1;
            }
        }
    }
    public class StopperJunctions
    {
        public int FeatureClassID { get; set; }

        private int[] stoppers = null;
        public int[] Stoppers
        {
            get
            {
                return stoppers;
            }
        }
        public StopperJunctions(IFeatureClass featureClass, List<int> featureIDs)
        {
            if (null != featureClass && esriFeatureType.esriFTSimpleJunction == featureClass.FeatureType && null != featureIDs && featureIDs.Count > 0)
            {
                this.FeatureClassID = featureClass.FeatureClassID;
                this.stoppers = featureIDs.ToArray();
            }
            else
                this.FeatureClassID = -1;
        }
        public StopperJunctions(IFeatureClass featureClass, int[] featureIDs)
        {
            if (null != featureClass && esriFeatureType.esriFTSimpleJunction == featureClass.FeatureType && null != featureIDs && featureIDs.Length > 0)
            {
                this.FeatureClassID = featureClass.FeatureClassID;
                this.stoppers = featureIDs;
            }
            else
                this.FeatureClassID = -1;
        }
    }
    public class NetworkHelper
    {
        public static Tuple<int, int> FindJunctionsOnEdge(IFeature ftr, IFeatureClass junctionFC, ServerLogger logger)
        {
            if (null != ftr && ftr.Shape is ICurve && null != junctionFC && esriFeatureType.esriFTSimpleJunction == junctionFC.FeatureType)
            {
                int fromid = -1, toid = -1;
                IFeatureCursor ftrCursor1 = null, ftrCursor2 = null;
                try
                {
                    if (false == ftr.Shape.IsEmpty)//I am not sure if a feature could have an empty geometry?
                    {
                        ICurve curv = ftr.Shape as ICurve;
                        ISpatialFilter filter = new SpatialFilterClass();
                        filter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                        filter.Geometry = curv.FromPoint;
                        ftrCursor1 = junctionFC.Search(filter, true);
                        IFeature pntFtr = ftrCursor1.NextFeature();
                        if (null != pntFtr)
                            fromid = pntFtr.OID;
                        filter.Geometry = curv.ToPoint;
                        ftrCursor2 = junctionFC.Search(filter, true);
                        pntFtr = ftrCursor2.NextFeature();
                        if (null != pntFtr)
                            toid = pntFtr.OID;
                    }
                }
                catch(Exception e)
                {
                    if(null != logger)
                        logger.LogMessage(ServerLogger.msgType.error, typeof(NetworkHelper).Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, GLC.AO.AOUtilities.ErrorCode, e.Message);
                }
                finally
                {
                    GLC.AO.AOUtilities.ReleaseCOMObj(ftrCursor2);
                    GLC.AO.AOUtilities.ReleaseCOMObj(ftrCursor1);
                }
                return new Tuple<int, int>(fromid, toid);
            }
            return null;
        }
        public static StartFlagEdge GetStartFlagEdge(NetworkContext ctx, double x, double y, double distance, double toleranceOnDist, string edgeFeatureClassAliasName, int flowDirFieldIndex, string invalidFlowDirFieldValue, ServerLogger logger)
        {
            StartFlagEdge sEdge = null;
            if (x > -180 && x < 180 && y > -90 && y < 90 && distance > 0 && toleranceOnDist > 0 && null != ctx && false == string.IsNullOrEmpty(edgeFeatureClassAliasName))
            {
                IFeatureClass edgeFeatureClass = ctx.GetEdgeFeatureClassByAliasName(edgeFeatureClassAliasName);
                if(null != edgeFeatureClass)
                {
                    Tuple<double, double> distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(x, y, distance, 1.0);
                    if(null != distD && distD.Item2 < toleranceOnDist)
                    {
                        IPoint pnt = new PointClass();
                        pnt.X = x;
                        pnt.Y = y;
                        Tuple<int, IFeature, double> edgeFlag = GLC.AO.AOUtilities.FindNearestFeature(pnt, edgeFeatureClass, distD.Item1, logger);
                        if (null != edgeFlag)
                        {
                            if(edgeFlag.Item2.get_Value(flowDirFieldIndex).ToString() != invalidFlowDirFieldValue)
                                sEdge = new StartFlagEdge(edgeFeatureClass, edgeFlag.Item1);
                        }
                    }
                }
            }
            return sEdge;
        }
        public static StopperJunctions GetStoppers(NetworkContext ctx, string junctionFeatureClassAliasName)
        {
            StopperJunctions stoppers = null;
            if (null != ctx && false == string.IsNullOrEmpty(junctionFeatureClassAliasName))
            {
                IFeatureClass junctionFeatureClass = ctx.GetJunctionFeatureClassIdByAliasName(junctionFeatureClassAliasName);
                if(null != junctionFeatureClass)
                {
                    int ftrCnt = junctionFeatureClass.FeatureCount(null);
                    if(ftrCnt > 0)
                    {
                        int[] arr = new int[ftrCnt];
                        for (int i = 0; i < ftrCnt; ++i)
                            arr[i] = i + 1;
                        stoppers = new StopperJunctions(junctionFeatureClass, arr);
                    }
                }
            }
            return stoppers;
        }
        public static StopperJunctions GetStoppersEID(NetworkContext ctx, string junctionFeatureClassAliasName)
        {
            StopperJunctions stoppers = null;
            if (null != ctx && null != ctx.GeometricNetwork && false == string.IsNullOrEmpty(junctionFeatureClassAliasName))
            {
                IFeatureClass junctionFeatureClass = ctx.GetJunctionFeatureClassIdByAliasName(junctionFeatureClassAliasName);
                INetElements netElements = ctx.GeometricNetwork.Network as INetElements;
                if (null != junctionFeatureClass && null != netElements)
                {
                    int ftrCnt = junctionFeatureClass.FeatureCount(null);
                    if (ftrCnt > 0)
                    {
                        int[] arr = new int[ftrCnt];
                        for (int i = 0; i < ftrCnt; ++i)
                            arr[i] = netElements.GetEID(junctionFeatureClass.FeatureClassID, i + 1, -1, esriElementType.esriETJunction);
                        stoppers = new StopperJunctions(junctionFeatureClass, arr);
                    }
                }
            }
            return stoppers;
        }
        public static StopperJunctions GetStoppers(NetworkContext ctx, List<IPoint> pnts, bool isUpStream, double distance, double toleranceOnDist, string edgeFeatureClassAliasName, string junctionFeatureClassAliasName, ServerLogger logger)
        {
            StopperJunctions stoppers = null;
            if(null != pnts && pnts.Count > 0 && distance > 0 && toleranceOnDist > 0 && null != ctx && false == string.IsNullOrEmpty(edgeFeatureClassAliasName) && false == string.IsNullOrEmpty(junctionFeatureClassAliasName))
            {
                IFeatureClass edgeFeatureClass = ctx.GetEdgeFeatureClassByAliasName(edgeFeatureClassAliasName);
                IFeatureClass junctionFeatureClass = ctx.GetJunctionFeatureClassIdByAliasName(junctionFeatureClassAliasName);
                if (null != edgeFeatureClass && null != junctionFeatureClass)
                {
                    List<int> stopperIds = new List<int>();
                    foreach (var pnt in pnts)
                    {
                        if (false == pnt.IsEmpty && pnt.X > -180 && pnt.X < 180 && pnt.Y > -90 && pnt.Y < 90)
                        {
                            Tuple<double, double> distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, distance, 1.0);
                            if (null != distD && distD.Item2 < toleranceOnDist)
                            {
                                Tuple<int, IFeature, double> stopperEdge = GLC.AO.AOUtilities.FindNearestFeature(pnt, edgeFeatureClass, distD.Item1, logger);
                                if (null != stopperEdge)
                                {
                                    Tuple<int, int> juncFrom2 = NetworkHelper.FindJunctionsOnEdge(stopperEdge.Item2, junctionFeatureClass, null);
                                    if (isUpStream && juncFrom2.Item1 > 0)
                                        stopperIds.Add(juncFrom2.Item1);
                                    else if (!isUpStream && juncFrom2.Item2 > 0)
                                        stopperIds.Add(juncFrom2.Item2);
                                }
                            }
                        }
                    }
                    if(stopperIds.Count > 0)
                        stoppers = new StopperJunctions(junctionFeatureClass, stopperIds);
                }
            }
            return stoppers;
        }
    }
}
