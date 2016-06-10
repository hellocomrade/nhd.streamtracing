using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.Specialized;
using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.SOESupport;

using GLC.AO;
using GLC.AO.GeometricNetwork;


//TODO: sign the project (project properties > signing tab > sign the assembly)
//      this is strongly suggested if the dll will be registered using regasm.exe <your>.dll /codebase


namespace GLC.TracingSOE
{
    [ComVisible(true)]
    [Guid("bffba63a-8a8a-4b4d-aad7-66390cbd89af")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectExtension("MapServer",//use "MapServer" if SOE extends a Map service and "ImageServer" if it extends an Image service.
        AllCapabilities = "Trace Stream Network with barriers. Barriers can be real or virtual.",
        DefaultCapabilities = "Trace Stream Network and stop at barriers",
        Description = "",
        DisplayName = "TracingSOE",
        Properties = "NetworkName=Hydro;MaxFeatureCount=20000;",
        SupportsREST = true,
        SupportsSOAP = false)]
    public class TracingSOE : IServerObjectExtension, IObjectConstruct, IRESTRequestHandler
    {
        private static readonly string FlowLineName = "FlowlineMerge";
        private static readonly string JunctionName = "Hydro_Net_Junctions";
        private static readonly string BarrierJunctionName = "Barriers";
        private static readonly string FlagParameterName = "Flag";
        private static readonly string BarrierParameterName = "Barriers";
        private static readonly string TracingDirParameterName = "Trace_Task_type";
        private static readonly string OutputEPSGParameterName = "env:outSR";
        private static readonly string InvalidFlowDirValue = "0";//"Uninitialized", "1 - WithDigitized", " 2- AgainstDigitized";
        
        private string soe_name;
        
        private IPropertySet configProps;
        private IServerObjectHelper serverObjectHelper;
        private ServerLogger logger;
        private IRESTRequestHandler reqHandler;

        private string m_networkName;
        private int m_maxFeatureCount;
        private NetworkContext m_networkContext;
        private const double m_searchDistance = 200.0;
        private const double m_searchTolerance = 5.0;
        private List<int> m_disabledFeatureClassIDs;
        private List<string> m_outputFields;
        private int m_networkEPSG = 4269;
        private int m_flowDirFieldIndex;
        private bool m_isReady = false;

        public TracingSOE()
        {
            soe_name = this.GetType().Name;
            logger = new ServerLogger();
            reqHandler = new SoeRestImpl(soe_name, CreateRestSchema()) as IRESTRequestHandler;
        }

        #region IServerObjectExtension Members

        public void Init(IServerObjectHelper pSOH)
        {
            serverObjectHelper = pSOH;
        }

        public void Shutdown()
        {
            AOUtilities.Dispose();
        }

        #endregion

        #region IObjectConstruct Members

        public void Construct(IPropertySet props)
        {
            configProps = props;
            LoadNetwork(props);
        }

        #endregion

        #region IRESTRequestHandler Members

        public string GetSchema()
        {
            return reqHandler.GetSchema();
        }

        public byte[] HandleRESTRequest(string Capabilities, string resourceName, string operationName, string operationInput, string outputFormat, string requestProperties, out string responseProperties)
        {
            if(this.m_isReady)
                return reqHandler.HandleRESTRequest(Capabilities, resourceName, operationName, operationInput, outputFormat, requestProperties, out responseProperties);
            else
            {
                responseProperties = null;
                JsonObject result = new JsonObject();
                result.AddString("error", "Network is not ready.");
                return Encoding.UTF8.GetBytes(result.ToJson());
            }
        }

        #endregion
        private void LoadNetwork(IPropertySet props)
        {
            if (null != props.GetProperty("NetworkName"))
                m_networkName = props.GetProperty("NetworkName") as string;
            else
                throw new ArgumentNullException("NetworkName is unspecified.");
            if (null == props.GetProperty("MaxFeatureCount") || false == int.TryParse(props.GetProperty("MaxFeatureCount") as string, out this.m_maxFeatureCount))
                this.m_maxFeatureCount = 5000;
            this.m_outputFields = new List<string>() { "Shape_Length" };
            this.m_networkContext = new NetworkContext(m_networkName, new List<string>() { FlowLineName }, new List<string>() { JunctionName, BarrierJunctionName });
            this.m_networkContext.LoadGeometricNetwork(null, this.serverObjectHelper, this.logger);
            this.m_isReady = this.m_networkContext.IsNetworkLoaded;
            if (this.m_isReady)
            {
                this.m_disabledFeatureClassIDs = new List<int>() { this.m_networkContext.GetJunctionFeatureClassIdByAliasName(BarrierJunctionName).FeatureClassID };
                IFeatureClass flowlineFeatureClass = this.m_networkContext.GetEdgeFeatureClassByAliasName(FlowLineName);
                ISpatialReference srs = AOUtilities.GetFeatureClassSpatialReference(flowlineFeatureClass, false);
                if (null != srs)
                    this.m_networkEPSG = srs.FactoryCode;
                else
                    this.m_isReady = false;
                this.m_flowDirFieldIndex = AOUtilities.GetFieldIndexByName(flowlineFeatureClass, "FlowDir");
                if (this.m_flowDirFieldIndex < 0)
                    this.m_isReady = false;
            }
        }
        private List<IPoint> ParseVirtualBarriers(JsonObject[] barrierJsonArray, uint epsg)//lazy here, just copy epsg from flag and assume barriers having the same one
        {
            List<IPoint> ret = null;
            if(null != barrierJsonArray && barrierJsonArray.Length > 0)
            {
                JsonObject barrierFeature = null;
                IPoint pnt = null;
                IGeometryArray pntArr1 = new GeometryArrayClass();
                for(int i = 0; i < barrierJsonArray.Length; ++i)
                {
                    if(barrierJsonArray[i].TryGetJsonObject("geometry", out barrierFeature))
                    {
                        if(null != barrierFeature)
                        {
                            double ? x, y;
                            //int? epsg;
                            if (barrierFeature.TryGetAsDouble("x", out x) && barrierFeature.TryGetAsDouble("y", out y))
                            {
                                pnt = new PointClass();
                                pnt.X = x.Value;
                                pnt.Y = y.Value;
                                pntArr1.Add(pnt);
                            }
                        }
                    }
                }
                if (pntArr1.Count > 0)
                {
                    IGeometryArray geomArr = AOUtilities.TransfromGeometriesFrom2(epsg, (uint)this.m_networkEPSG, pntArr1);
                    if (null != geomArr && geomArr.Count > 0)
                    {
                        ret = new List<IPoint>();
                        for (int i = 0; i < geomArr.Count; ++i)
                            ret.Add(geomArr.get_Element(i) as IPoint);
                    }
                }
            }
            return ret;
        }
        private RestResource CreateRestSchema()
        {
            RestResource rootRes = new RestResource(soe_name, false, RootResHandler);

            RestOperation sampleOper = new RestOperation("StreamNetworkTrace",
                                                      new string[] { BarrierParameterName, FlagParameterName, TracingDirParameterName, OutputEPSGParameterName },
                                                      new string[] { "json" },
                                                      NetworkTraceHandler);

            rootRes.operations.Add(sampleOper);

            return rootRes;
        }

        private byte[] RootResHandler(NameValueCollection boundVariables, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;

            JsonObject result = new JsonObject();
            result.AddString("Description", "Tracing run, run tracing...");

            return Encoding.UTF8.GetBytes(result.ToJson());
        }

        private byte[] NetworkTraceHandler(NameValueCollection boundVariables,
                                                  JsonObject operationInput,
                                                      string outputFormat,
                                                      string requestProperties,
                                                  out string responseProperties)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            responseProperties = null;

            string traceTypeStr;
            bool found = operationInput.TryGetString(TracingDirParameterName, out traceTypeStr);
            if (!found || string.IsNullOrEmpty(traceTypeStr))
                throw new ArgumentNullException(TracingDirParameterName + " is required");
            traceTypeStr = traceTypeStr.ToUpper();
            bool ? isUpstream = null;
            if ("TRACE_UPSTREAM" == traceTypeStr)
                isUpstream = true;
            else if ("TRACE_DOWNSTREAM" == traceTypeStr)
                isUpstream = false;
            if (false == isUpstream.HasValue)
                throw new ArgumentException("Not valid Trace_Task_type");

            long? outEPSG;
            found = operationInput.TryGetAsLong(OutputEPSGParameterName, out outEPSG);
            if (!found || !outEPSG.HasValue || outEPSG <= 0)
                throw new ArgumentNullException(OutputEPSGParameterName + " is required");
            if (outEPSG < 1)
                throw new ArgumentException(OutputEPSGParameterName + " is not valid");

            JsonObject flagJSON = null;
            object[] flagArray = null;
            JsonObject[] flagJsonArray = null;
            if (false == operationInput.TryGetJsonObject(FlagParameterName, out flagJSON) || null == flagJSON)
                throw new ArgumentNullException(FlagParameterName+ " is required");
            if(flagJSON.TryGetArray("features", out flagArray))
            {
                try
                {
                    flagJsonArray = flagArray.Cast<JsonObject>().ToArray();
                }
                catch
                {
                    throw new ArgumentException("invalid Flags json format");
                }
            }
            //Found the flag
            List<int> ftrList = null;
            if (null != flagJsonArray && 1 == flagJsonArray.Length)
            {
                JsonObject flagFeature = null;
                if (flagJsonArray[0].TryGetJsonObject("geometry", out flagFeature))
                {
                    if(null == flagFeature)
                        throw new ArgumentException("invalid Flags json format with geometry");
                    double ? x, y;
                    long ? epsg;
                    JsonObject srsObj;
                    if (true == flagFeature.TryGetJsonObject("spatialReference", out srsObj))
                    {
                        if (false == srsObj.TryGetAsLong("wkid", out epsg) || epsg <= 0)
                            throw new ArgumentException("No valid wikd found for flag feature.");
                    }
                    else
                        throw new ArgumentException("No spatial reference found for flag feature.");
                    if (flagFeature.TryGetAsDouble("x", out x) && flagFeature.TryGetAsDouble("y", out y))
                    {
                        if (!x.HasValue || !y.HasValue)
                            throw new ArgumentException("invalid Flag coordinate");
                        IPoint pnt1 = new PointClass();
                        pnt1.X = x.Value;
                        pnt1.Y = y.Value;
                        IGeometryArray pntArr1 = new GeometryArrayClass();
                        pntArr1.Add(pnt1);
                        IGeometryArray geomArr = AOUtilities.TransfromGeometriesFrom2((uint)epsg, (uint)this.m_networkEPSG, pntArr1);
                        if (null == geomArr || 1 != geomArr.Count)
                            throw new ArgumentException("invalid Flag coordinate for reprojection");
                        pnt1 = geomArr.get_Element(0) as IPoint;
                        StartFlagEdge flag = NetworkHelper.GetStartFlagEdge(this.m_networkContext, pnt1.X, pnt1.Y, TracingSOE.m_searchDistance, TracingSOE.m_searchTolerance, FlowLineName, this.m_flowDirFieldIndex, InvalidFlowDirValue, logger);
                        StopperJunctions stoppers = null;
                        if (null != flag)
                        {
                            List<IPoint> barrierPnts = null;
                            JsonObject barriersJSON = null;
                            object[] barrierArray = null;
                            JsonObject[] barrierJsonArray = null;
                            if (true == operationInput.TryGetJsonObject(BarrierParameterName, out barriersJSON) || null != barriersJSON)
                            {
                                if (barriersJSON.TryGetArray("features", out barrierArray))
                                {
                                    try
                                    {
                                        barrierJsonArray = barrierArray.Cast<JsonObject>().ToArray();
                                        barrierPnts = ParseVirtualBarriers(barrierJsonArray, (uint)epsg);
                                    }
                                    catch
                                    {
                                        throw new ArgumentException("invalid Barriers json format");
                                    }
                                }
                                if (null != barrierPnts && barrierPnts.Count > 0)
                                    stoppers = NetworkHelper.GetStoppers(this.m_networkContext, barrierPnts, isUpstream.Value, TracingSOE.m_searchDistance, TracingSOE.m_searchTolerance, FlowLineName, JunctionName, logger);
                            }
                        }
                        ftrList = AOUtilities.StreamTrace(this.m_networkContext.GeometricNetwork, flag, this.m_disabledFeatureClassIDs, stoppers, isUpstream.Value, this.m_maxFeatureCount, logger);
                    }
                }
            }
            IRecordSet records = null;
            if(null != ftrList && ftrList.Count > 0)
                records = AOUtilities.GetRecordSetFromFeatureClass(this.m_networkContext.GetEdgeFeatureClassByAliasName(FlowLineName), ftrList, this.m_outputFields, (uint)outEPSG.Value);
            JsonObject result = new JsonObject();
            if(null != records)
                result.AddJsonObject("value", new JsonObject(System.Text.Encoding.UTF8.GetString(Conversion.ToJson(records))));
            else
                result.AddString("output", "{}");
            watch.Stop();
            this.logger.LogMessage(ServerLogger.msgType.debug, "NetworkTraceHandler", 973, "Tracing taked: " + watch.ElapsedMilliseconds.ToString() + " ms");
            result.AddLong("time(ms)", watch.ElapsedMilliseconds);
            return Encoding.UTF8.GetBytes(result.ToJson());
        }


    }
}
