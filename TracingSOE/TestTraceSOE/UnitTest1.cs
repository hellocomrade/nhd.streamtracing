using System;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ESRI.ArcGIS;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.SOESupport;
using ESRI.ArcGIS.DataSourcesGDB;
using GLC.AO;
using GLC.AO.GeometricNetwork;
using Moq;

namespace TestTraceSOE
{
    internal partial class LicenseInitializer
    {
        public LicenseInitializer()
        {
            ResolveBindingEvent += new EventHandler(BindingArcGISRuntime);
        }

        void BindingArcGISRuntime(object sender, EventArgs e)
        {
            //
            // TODO: Modify ArcGIS runtime binding code as needed
            //
            if (!RuntimeManager.Bind(ProductCode.Desktop))
            {
                // Failed to bind, announce and force exit
                Console.WriteLine("Invalid ArcGIS runtime binding. Application will shut down.");
                System.Environment.Exit(0);
            }
        }
    }
    [TestClass]
    public class UnitTest1
    {
        //private static readonly string GN_Path = @"C:\Users\gwang\Documents\Visual Studio 2013\Projects\GeometricNetworkMonkey\StreamLinRef.gdb";
        private static readonly string GN_Path = @"C:\Users\gwang.GLC\Documents\Visual Studio 2013\Projects\TracingSOE\StreamLinRef.gdb";
        private static LicenseInitializer m_AOLicenseInitializer;
        [ClassInitialize]
        public static void UnitTest1Init(TestContext context)
        {
            m_AOLicenseInitializer = new TestTraceSOE.LicenseInitializer();
            //ESRI License Initializer generated code.
            m_AOLicenseInitializer.InitializeApplication(new esriLicenseProductCode[] { esriLicenseProductCode.esriLicenseProductCodeAdvanced },
            new esriLicenseExtensionCode[] { });
        }
        [ClassCleanup]
        public static void UnitTest1Cleanup()
        {
            AOUtilities.Dispose();
            //ESRI License Initializer generated code.
            //Do not make any call to ArcObjects after ShutDownApplication()
            m_AOLicenseInitializer.ShutdownApplication();
        }
        [TestMethod]
        [ExpectedException(typeof(System.Runtime.InteropServices.COMException))]
        public void CoordinatesReProjection()
        {
            //Arrange
            IPoint pnt1 =new PointClass();
            pnt1.X = -9569447.832126;
            pnt1.Y = 5524225.232441;
            //pnt1.SpatialReference = AOUtilities.GetSpatialReference(3857);
            IGeometryArray pntArr1 = new GeometryArrayClass();
            pntArr1.Add(pnt1);

            //Act
            //The SpatialReference property for all returned geometries will be null.  It is the consumers responsibility to assign the 
            //spatial reference to each geometry returned, if desired.  In this case, the spatial reference is assumed to be the output spatial reference defined for the Project operation.  
            IGeometryArray geomArr = AOUtilities.TransfromGeometriesFrom2(3857, 4269, pntArr1);
            //It appears IGeometryServer2.Project will NOT throw an error if the coordinates don't match the given epsg
            //instead, it simply returns the input coordinates as output...
            IGeometryArray geomArr1 = AOUtilities.TransfromGeometriesFrom2(4326, 4269, pntArr1);
            //Assert
            Assert.IsNotNull(geomArr);
            Assert.IsNotNull(geomArr1);
            Assert.AreEqual<int>(pntArr1.Count, geomArr.Count);
            Assert.AreEqual<int>(pntArr1.Count, geomArr1.Count);
            IPoint pnt2 = geomArr.get_Element(0) as IPoint;
            Assert.IsNotNull(pnt2);
            pnt2 = geomArr1.get_Element(0) as IPoint;
            Assert.IsNotNull(pnt2);

            //Act
            geomArr1 = AOUtilities.TransfromGeometriesFrom2(4269, 4326, geomArr);
            //Assert
            Assert.IsNotNull(geomArr1);
            Assert.AreEqual<int>(geomArr1.Count, geomArr.Count);
            pnt2 = geomArr1.get_Element(0) as IPoint;
            IPoint pnt3 = geomArr.get_Element(0) as IPoint;
            Assert.IsNotNull(pnt2);
            //Since the output geometry will not have spatial reference assigned, we can't verify if the output are in the desired projection through this shortcut.
            //Assert.IsTrue(4326 == pnt2.SpatialReference.FactoryCode);
            //Assert.IsTrue(4269 == pnt3.SpatialReference.FactoryCode);

            //Arrange
            pntArr1.RemoveAll();
            pntArr1.Add(pnt2);

            //Act
            geomArr = AOUtilities.TransfromGeometriesFrom2(4269, 3857, pntArr1);

            //Assert
            Assert.IsNotNull(geomArr);
            Assert.AreEqual<int>(pntArr1.Count, geomArr.Count);
            pnt2 = geomArr.get_Element(0) as IPoint;
            Assert.IsNotNull(pnt2);
            Assert.IsTrue((Math.Abs(pnt2.X - pnt1.X) < 1e-7) && (Math.Abs(pnt2.Y - pnt1.Y) < 1e-7));

            //Arrange
            pnt1.X = double.MinValue;
            pnt1.Y = double.PositiveInfinity;
            pntArr1.RemoveAll();
            pntArr1.Add(pnt1);

            //Act
            geomArr = AOUtilities.TransfromGeometriesFrom2(3857, 4269, pntArr1);
            pnt2 = geomArr.get_Element(0) as IPoint;

            //Assert
            Assert.IsTrue(pnt2.IsEmpty);
            double x = pnt2.X;
        }
        [TestMethod]
        [ExpectedException(typeof(System.ArgumentException))]
        public void LoadGeometricNetworkFromFileGeoDatabase()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });

            //Act
            bool ret = ctx.LoadGeometricNetwork(GN_Path, null, null);//new Mock<IServerObjectHelper>().Object, new ServerLogger());

            //Assert
            Assert.IsTrue(ret && ctx.IsNetworkLoaded);

            //Arrange
            NetworkContext ctx1 = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            //moq doesn't work on sealed class
            //Mock<ServerLogger> MockServerLogger = new Mock<ServerLogger>();

            //Act on a nonexist path
            ret = ctx1.LoadGeometricNetwork("abc", null, null);//MockServerLogger.Object);

            //Assert
            Assert.IsFalse(ret);
            
            
            //Arrange
            NetworkContext ctx2 = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge", "foobar" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            
            //Act, although the network can be loaded, we have fed some extra feature class that doesn't exist in the 
            //current network. So IsLoaded is still false.
            ret = ctx2.LoadGeometricNetwork(GN_Path, null, null);

            //Assert
            Assert.IsTrue(false == ret && false == ctx2.IsNetworkLoaded);
            
            //MockServerLogger.Verify(t => t.LogMessage(ServerLogger.msgType.error, It.IsAny<string>(), It.Is<int>(tt => tt == GLC.AO.AOUtilities.ErrorCode), It.IsAny<string>()));

            //Arrange
            NetworkContext ctx3 = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });

            //Act
            //ctx can't be populated more than once, otherwise throw exception
            ctx3.LoadGeometricNetwork(GN_Path, null, null);
            ctx3.LoadGeometricNetwork(GN_Path, null, null);
        
        }
        [TestMethod]
        public void GetApproximateDegreeAsDistance()
        {
            //Arrange
            IPoint pnt = new PointClass();
            pnt.X = -86.118;
            pnt.Y = 43.971;
            
            //Act, make sure error is less than 5% from range [1, 1500]
            Tuple<double, double> distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1500.0, 1.0);
            System.Diagnostics.Debug.WriteLine("1500 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 1500.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1000.0, 1.0);
            System.Diagnostics.Debug.WriteLine("1000 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 1000.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 500.0, 1.0);
            System.Diagnostics.Debug.WriteLine("500 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 500.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 200.0, 1.0);
            System.Diagnostics.Debug.WriteLine("200 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 200.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 100.0, 1.0);
            System.Diagnostics.Debug.WriteLine("100 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 100.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 50.0, 1.0);
            System.Diagnostics.Debug.WriteLine("50 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 50.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 10.0, 1.0);
            System.Diagnostics.Debug.WriteLine("10 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 10.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 5.0, 1.0);
            System.Diagnostics.Debug.WriteLine("5 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 5.0);
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1.0, 1.0);
            System.Diagnostics.Debug.WriteLine("1 Distance: Result = {0}, Error = {1}", distD.Item1, distD.Item2 / 1.0);

            //Arrange
            pnt.X = -186.118;
            pnt.Y = 43.971;

            //Act
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 5.0, 1.0);

            //Assert
            Assert.IsNull(distD);
        }
        [TestMethod]
        public void FindNearestEdgeAtLocation()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            IPoint pnt = new PointClass();
            pnt.X = -86.118;
            pnt.Y = 43.971;
            Tuple<double, double> distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1500, 0.1);
            
            //Act
            Tuple<int, IFeature, double> edgeFlag = GLC.AO.AOUtilities.FindNearestFeature(pnt, ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge"), distD.Item1, null);

            //Assert
            Assert.IsTrue(edgeFlag.Item1 > 0 && edgeFlag.Item2 != null);

            //Arrange
            pnt.X = 86.118;
            pnt.Y = -43.971;
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1500, 0.1);

            //Act
            edgeFlag = GLC.AO.AOUtilities.FindNearestFeature(pnt, ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge"), distD.Item1, null);

            //Assert
            Assert.IsNull(edgeFlag);
        }
        [TestMethod]
        public void FindJunctionsOnEdge()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            IPoint pnt = new PointClass();
            pnt.X = -86.118;
            pnt.Y = 43.971;
            IFeatureClass edgeFeatureClass = ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge");
            IFeatureClass junctionFeatureClass = ctx.GetJunctionFeatureClassIdByAliasName("Hydro_Net_Junctions");
            Tuple<double, double> distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1500, 0.1);
            Tuple<int, IFeature, double> edgeFlag = GLC.AO.AOUtilities.FindNearestFeature(pnt, edgeFeatureClass, distD.Item1, null);
            
            //Act
            Tuple<int, int> juncFrom2 = NetworkHelper.FindJunctionsOnEdge(edgeFlag.Item2, junctionFeatureClass, null);

            //Assert
            Assert.IsNotNull(juncFrom2);
            Assert.IsTrue(juncFrom2.Item1 > 0 && juncFrom2.Item2 > 0);

            //Arrange
            pnt.X = -86.218;
            pnt.Y = 43.937;
            distD = GLC.AO.AOUtilities.GetEstimatedDistInDegree(pnt.X, pnt.Y, 1500, 0.1);
            edgeFlag = GLC.AO.AOUtilities.FindNearestFeature(pnt, edgeFeatureClass, distD.Item1, null);

            //Act
            juncFrom2 = NetworkHelper.FindJunctionsOnEdge(edgeFlag.Item2, junctionFeatureClass, null);

            //Arrange
            Assert.IsNotNull(juncFrom2);
            Assert.IsTrue(juncFrom2.Item1 > 0 && juncFrom2.Item2 == -1);
        }
        [TestMethod]
        public void TestGetFieldIndexAndValues()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            IFeatureClass fc = ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge");

            //Act
            int idx = AOUtilities.GetFieldIndexByName(fc, "FlowDir");
            Dictionary<string, Tuple<int, esriFieldType>> fldDict = AOUtilities.GetFieldIndexes(fc);

            //Assert
            Assert.IsTrue(idx >= 0);
            Assert.IsNotNull(fldDict);
            Assert.IsTrue(idx == fldDict["FlowDir"].Item1);
        }
        [TestMethod]
        public void ConstrcutStartEdge()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            //Flag
            IPoint pnt = new PointClass();
            pnt.X = -86.291;
            pnt.Y = 43.941;
            IFeatureClass fc = ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge");
            int idx = AOUtilities.GetFieldIndexByName(fc, "FlowDir");


            //Act
            StartFlagEdge flag = NetworkHelper.GetStartFlagEdge(ctx, -86.291, 43.941, 100, 5, "FlowlineMerge", idx, "0", null);

            //Assert
            Assert.IsNotNull(flag);
            Assert.IsTrue(flag.FeatureID == 1927894);
        }
        //Buf Fix Issue#12 at https://github.com/GreatLakesCommission/brcs-models/issues/12
        /*
         * Barriers:
Flag:{"displayFieldName":"","hasZ":false,"geometryType":"esriGeometryPoint","spatialReference":{"wkid":4269,"latestWkid":4269},"fields":[{"name":"OBJECTID","type":"esriFieldTypeOID","alias":"OBJECTID"},{"name":"Enabled","type":"esriFieldTypeSmallInteger","alias":"Enabled"}],"features":[{"geometry":{"x":-85.04465103149414,"y":43.27143070627271,"spatialReference":{"wkid":4326}}}],"exceededTransferLimit":false}
Trace_Task_type:TRACE_UPSTREAM
f:json
env:outSR:4326
         */
        [TestMethod]
        public void TestIdentifyUninitializedStreamSegment()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);

            //Get FlowDir index
            IFeatureClass fc = ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge");
            int idx = AOUtilities.GetFieldIndexByName(fc, "FlowDir");
            IPoint pnt = new PointClass()
                        {
                            X = -85.04465103149414,
                            Y = 43.27143070627271
                        };
            IGeometryArray pntArr = new GeometryArrayClass();
            pntArr.Add(pnt);
            IGeometryArray geomArr = AOUtilities.TransfromGeometriesFrom2(4326, 4269, pntArr);
            IPoint pnt1 = geomArr.get_Element(0) as IPoint; 

            //Act
            StartFlagEdge flag = NetworkHelper.GetStartFlagEdge(ctx, pnt1.X, pnt1.Y, 200, 5, "FlowlineMerge", idx, "0", null);

            //Assert
            Assert.IsNull(flag);
        }
        [TestMethod]
        public void ConstructStoppersOnAllExistingBarriers()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);

            //Act
            StopperJunctions stoppers = NetworkHelper.GetStoppers(ctx, "Barriers");

            //Assert
            Assert.IsNotNull(stoppers);
            Assert.IsTrue(stoppers.Stoppers != null && stoppers.Stoppers.Length > 0);
        }
        [TestMethod]
        public void ConstructStoppersOnAllExistingBarriersEIDs()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);

            //Act
            StopperJunctions stoppers = NetworkHelper.GetStoppersEID(ctx, "Barriers");

            //Assert
            Assert.IsNotNull(stoppers);
            Assert.IsTrue(stoppers.Stoppers != null && stoppers.Stoppers.Length > 0);
        }
        [TestMethod]
        public void ConstructStoppersOnVirtualBarriers()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            IPoint pnt = new PointClass();
            pnt.X = -86.313;
            pnt.Y = 43.922;
            IPoint pnt1 = new PointClass();
            pnt1.X = -86.31;
            pnt1.Y = 43.92;
            List<IPoint> pnts =new List<IPoint>(){pnt, pnt1};
            
            //Act
            StopperJunctions stoppers = NetworkHelper.GetStoppers(ctx, pnts, true, 200, 5, "FlowlineMerge", "Hydro_Net_Junctions", null);

            //Assert
            Assert.IsNotNull(stoppers);
            Assert.IsTrue(stoppers.Stoppers != null && stoppers.Stoppers.Length == 2);
            Assert.IsTrue(stoppers.Stoppers[0] == 624591 && stoppers.Stoppers[1] == 624974);

            //Act
            stoppers = NetworkHelper.GetStoppers(ctx, pnts, false, 200, 5, "FlowlineMerge", "Hydro_Net_Junctions", null);
            Assert.IsNotNull(stoppers);
            Assert.IsTrue(stoppers.Stoppers != null && stoppers.Stoppers.Length == 2);
            Assert.IsTrue(stoppers.Stoppers[0] == 624381 && stoppers.Stoppers[1] == 624391);
        }
        [TestMethod]
        public void GetFeatureClassEPSG()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);

            //Act
            ISpatialReference srs = AOUtilities.GetFeatureClassSpatialReference(ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge"), false);
            
            //Assert
            Assert.IsTrue(4269 == srs.FactoryCode);
        }
        [TestMethod]
        public void TraceStream()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            //Flag
            IFeatureClass fc = ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge");
            int idx = AOUtilities.GetFieldIndexByName(fc, "FlowDir");
            StartFlagEdge flag = NetworkHelper.GetStartFlagEdge(ctx, -85.062153, 44.003813, 1000, 5, "FlowlineMerge", idx, "0", null);
            //All Existing Barriers
            List<int> fcid = new List<int>(){ctx.GetJunctionFeatureClassIdByAliasName("Barriers").FeatureClassID};
            //Virtual Barriers
            IPoint pnt = new PointClass();
            pnt.X = -85.021764;
            pnt.Y = 44.122331;
            IPoint pnt1 = new PointClass();
            pnt1.X = -84.974754;
            pnt1.Y = 44.162720;
            IPoint pnt2 = new PointClass();
            pnt2.X = -84.896625;
            pnt2.Y = 44.128;
            List<IPoint> pnts = new List<IPoint>() { pnt, pnt1, pnt2 };
            StopperJunctions stoppers1 = NetworkHelper.GetStoppers(ctx, pnts, true, 1000, 5, "FlowlineMerge", "Hydro_Net_Junctions", null);
            
            //Act
            //Do upstream first
            List<int> ftrList = AOUtilities.StreamTrace(ctx.GeometricNetwork, flag, fcid, stoppers1, true, 1000, null);

            //Assert
            Assert.IsNotNull(ftrList);
            List<string> ids = new List<string>();
            foreach (var ftr in ftrList)
                ids.Add(ftr.ToString());
            //System.Diagnostics.Debug.WriteLine(string.Join(",", ids.ToArray()));

            //Act
            //Do upstream again without stoppers to test if the previous setting is gone
            ftrList = AOUtilities.StreamTrace(ctx.GeometricNetwork, flag, fcid, null, true, 1000, null);

            //Assert
            Assert.IsNotNull(ftrList);
            ids = new List<string>();
            foreach (var ftr in ftrList)
                ids.Add(ftr.ToString());
            //System.Diagnostics.Debug.WriteLine(string.Join(",", ids.ToArray()));

            //Act
            //downstream
            ftrList = AOUtilities.StreamTrace(ctx.GeometricNetwork, flag, fcid, null, false, 1000, null);

            //Assert
            Assert.IsNotNull(ftrList);
            ids = new List<string>();
            foreach (var ftr in ftrList)
                ids.Add(ftr.ToString());
            //System.Diagnostics.Debug.WriteLine(string.Join(",", ids.ToArray()));
            IRecordSet records = AOUtilities.GetRecordSetFromFeatureClass(ctx.GetEdgeFeatureClassByAliasName("FlowlineMerge"), ftrList, new List<string>() { "Shape_Length" }, 3857);
            Assert.IsNotNull(records);
            //How to update a feature
            //http://help.arcgis.com/en/sdk/10.0/arcobjects_net/conceptualhelp/index.html#/d/0001000002rs000000.htm
            /*ICursor ftrCursor = records.get_Cursor(true);
            try
            {
                IRow row = ftrCursor.NextRow();
                IFeature ftr = row as IFeature;
                esriFeatureType type = ftr.FeatureType;
                esriGeometryType t1 = ftr.Shape.GeometryType;
                
                IRow row1 = ftrCursor.NextRow();
                IFeature ftr1 = row as IFeature;

                ftr.Shape = ftr1.ShapeCopy;
            }
            catch(Exception e)
            {
                string err = e.Message;
            }
            finally
            {
                AOUtilities.ReleaseCOMObj(ftrCursor);
            }*/
            JsonObject json = new JsonObject(System.Text.Encoding.UTF8.GetString(Conversion.ToJson(records)));
            Assert.IsNotNull(json);
            //System.Diagnostics.Debug.WriteLine(json.ToJson());
        }
        [TestMethod]
        public void CopyFeatureClassTemplate()
        {
            //Arrange
            NetworkContext ctx = new NetworkContext("Hydro", new List<string>() { "FlowlineMerge" }, new List<string>() { "Hydro_Net_Junctions", "Barriers" });
            ctx.LoadGeometricNetwork(GN_Path, null, null);
            IFeatureClass fc = ctx.GetJunctionFeatureClassIdByAliasName("Barriers");

            //Act
            AbstractFeatureClassBag fcb = new InMemoryFeatureClassBag("in memory", AOUtilities.GetInMemoryWorkspaceFactory());
            bool ret = fcb.CreateFeatureClass(fc, "clone barrier");
            fcb.Dispose();

            //Assert
            Assert.IsTrue(ret);

        }
        private double randomDouble(double low, double high)
        {
            Random r = new Random();
            return low + (high - low) * r.NextDouble();
        }
        [TestMethod]
        public void FeatureClassBagRAMProfile()
        {
            //more detailed approach is necessary
            //Arrange
            //1,000,000 points will take roughly 500 MB RAM on my desktop
            /*int length = (int)1e6;
            List<MyFeature> pnts = new List<MyFeature>();
            for (int i = 0; i < length; ++i)
                pnts.Add(new MyFeature(esriFeatureType.esriFTSimple, esriGeometryType.esriGeometryPoint, new PointClass() { X = randomDouble(-180, 180), Y = randomDouble(-90, 90) }));
            */
            //Act
            for (int i = 0; i < 5; ++i)
            {
                int length = (int)1e6;
                List<MyFeature> pnts = new List<MyFeature>();
                for (int j = 0; j < length; ++j)
                    pnts.Add(new MyFeature(esriFeatureType.esriFTSimple, esriGeometryType.esriGeometryPoint, new PointClass() { X = randomDouble(-180, 180), Y = randomDouble(-90, 90) }, j, null));
                using (InMemoryFeatureClassBag bag1 = new InMemoryFeatureClassBag("wname1", AOUtilities.GetInMemoryWorkspaceFactory()))
                {
                    IFieldsEdit flds = AOUtilities.GetBareMetalFields(esriGeometryType.esriGeometryPoint, 3857, null);
                    Assert.IsTrue(bag1.CreateFeatureClass("foo", flds, esriFeatureType.esriFTSimple, "Shape"));
                    Assert.IsTrue(bag1.AddFeatures("foo", pnts, 0));
                }
            }
        }
        [TestMethod]
        public void TestFeatureClassBag()
        {
            //Arrange
            double d;
            int length = (int)1e3;
            List<MyFieldDef> fdefs = new List<MyFieldDef>();
            fdefs.Add(new MyFieldDef(esriFieldType.esriFieldTypeString, "strFld"));
            fdefs.Add(new MyFieldDef(esriFieldType.esriFieldTypeDouble, "doubleFld"));
            IFieldsEdit flds = AOUtilities.GetBareMetalFields(esriGeometryType.esriGeometryPoint, 3857, fdefs);
            List<MyFeature> pnts = new List<MyFeature>();
            
            for (int j = 0; j < length; ++j)
                pnts.Add(new MyFeature(esriFeatureType.esriFTSimple, esriGeometryType.esriGeometryPoint, new PointClass() { X = randomDouble(-180, 180), Y = randomDouble(-90, 90) }, j, new List<object>() { j.ToString(), randomDouble(0, 100) }));
            
            //Act
            using (IFeatureClassBag bag1 = new InMemoryFeatureClassBag("wname1", AOUtilities.GetInMemoryWorkspaceFactory()))
            {
                Assert.IsTrue(bag1.CreateFeatureClass("foo", flds, esriFeatureType.esriFTSimple, "Shape"));
                Assert.IsTrue(bag1.AddFeatures("foo", pnts, fdefs.Count));
                List<IFeature> ftrs = new List<IFeature>();
                foreach (var f in bag1.GetFeatures("foo", false))
                    ftrs.Add(f);
                object o1 = ftrs[0].get_Value(2);
                object o2 = ftrs[0].get_Value(3);

                object o3 = ftrs[length - 1].get_Value(2);
                object o4 = ftrs[length - 1].get_Value(3);

                //Assert
                Assert.IsTrue(ftrs.Count == pnts.Count);
                Assert.IsTrue(null != o1 && o1.ToString() == "0");
                Assert.IsTrue(null != o3 && o3.ToString() == "999");
                Assert.IsTrue(null != o2 && double.TryParse(o2.ToString(), out d));
                Assert.IsTrue(null != o4 && double.TryParse(o4.ToString(), out d));
                //Assert
                //The following should fall through, GetFeatures actually returns nothing instead of an IEnumerable<IFeature>
                foreach (var f in bag1.GetFeatures("bar", true))
                    continue;
            }


            IFeatureClassBag bag2 = new InMemoryFeatureClassBag("wname2", AOUtilities.GetInMemoryWorkspaceFactory());
            IFeatureClassBag bag3 = new InMemoryFeatureClassBag("wname3", AOUtilities.GetInMemoryWorkspaceFactory());
            bag2.Dispose();
            bag3.Dispose();
        }
        [TestMethod]
        public void TestTracingOutputSize()
        {
            //http://www.asp.net/web-api/overview/advanced/calling-a-web-api-from-a-net-client
            //http://stackoverflow.com/questions/1076622/browser-limitation-with-maximum-page-length
            /*
             * IE can only handle json up to 35MB, so the max feature limit should be less than 10K? It actually depends on the size of the geometry.
             * It's hard to say if 10,100 features will guarantee exceed 30MB...
             * 
             * For the following case, the result will contain about 14K features. IE will not be able to process it, so does Chrome.
             */ 
            string url = "http://localhost:6080/arcgis/rest/services/GeometricNetwork_Great_Lakes/MapServer/exts/TracingSOE/StreamNetworkTrace";
            string flag = @"{'displayFieldName':'',
                            'hasZ':false,
                            'geometryType':'esriGeometryPoint',
                            'spatialReference':{'wkid':4269,'latestWkid':4269},
                            'fields':[{'name':'OBJECTID','type':'esriFieldTypeOID','alias':'OBJECTID'},
                                      {'name':'Enabled','type':'esriFieldTypeSmallInteger','alias':'Enabled'}
                                     ],
                            'features':[
                                        {'geometry':{'x':-9399021.0662199,'y':5986920.5644451,'spatialReference':{'wkid':3857}}}
                                       ],
                            'exceededTransferLimit':false
                           }";
            /*
             * Really large dataset, 12MB output, 9300 features
             * flag = {"displayFieldName":"","hasZ":false,"geometryType":"esriGeometryPoint","spatialReference":{"wkid":4269
,"latestWkid":4269},"fields":[{"name":"OBJECTID","type":"esriFieldTypeOID","alias":"OBJECTID"},{"name"
:"Enabled","type":"esriFieldTypeSmallInteger","alias":"Enabled"}],"features":[{"geometry":{"x":-84.28347015171313
,"y":46.71392616173629,"spatialReference":{"wkid":4326}}}],"exceededTransferLimit":false}
             * 
             * 
             * 
             */
            string barrier = "";
            string type = "TRACE_UPSTREAM";
            string srs = "3857";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                var post = new List<KeyValuePair<string, string>>();
                post.Add(new KeyValuePair<string, string>("Flag", flag));
                post.Add(new KeyValuePair<string, string>("Barriers", barrier));
                post.Add(new KeyValuePair<string, string>("Trace_Task_type", type));
                post.Add(new KeyValuePair<string, string>("env:outSR", srs));
                post.Add(new KeyValuePair<string, string>("f", "pjson"));
                var output = client.PostAsync(url, new FormUrlEncodedContent(post));
                if(System.Net.HttpStatusCode.OK == output.Result.StatusCode)
                {
                    var bt = output.Result.Content.ReadAsByteArrayAsync();
                    System.Diagnostics.Debug.WriteLine("Output (Byte): " + bt.Result.Length);
                }
            }
        }
        [TestMethod]
        public void TestOutputContentEncoding()
        {
            string url = "http://localhost/arcgis/rest/services/GeometricNetwork_Great_Lakes/MapServer/exts/TracingSOE/StreamNetworkTrace";
            string flag = @"{'displayFieldName':'',
                            'hasZ':false,
                            'geometryType':'esriGeometryPoint',
                            'spatialReference':{'wkid':4269,'latestWkid':4269},
                            'fields':[{'name':'OBJECTID','type':'esriFieldTypeOID','alias':'OBJECTID'},
                                      {'name':'Enabled','type':'esriFieldTypeSmallInteger','alias':'Enabled'}
                                     ],
                            'features':[
                                        {'geometry':{'x':-9399021.0662199,'y':5986920.5644451,'spatialReference':{'wkid':3857}}}
                                       ],
                            'exceededTransferLimit':false
                           }";
            string barrier = "";
            string type = "TRACE_UPSTREAM";
            string srs = "3857";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                /*
                 * gzip will reduce the content transferring through network by 80%. However, it will take server extra time to conduct compression
                 */ 
                client.DefaultRequestHeaders.AcceptEncoding.Clear();
                client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                var post = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("Flag", flag),
                    new KeyValuePair<string, string>("Barriers", barrier),
                    new KeyValuePair<string, string>("Trace_Task_type", type),
                    new KeyValuePair<string, string>("env:outSR", srs),
                    new KeyValuePair<string, string>("f", "pjson")
                };
                var output = client.PostAsync(url, new FormUrlEncodedContent(post));
                if (System.Net.HttpStatusCode.OK == output.Result.StatusCode)
                {
                    var bt = output.Result.Content.ReadAsByteArrayAsync();
                    System.Diagnostics.Debug.WriteLine("Output (Byte): " + bt.Result.Length);

                    foreach (var en in output.Result.Content.Headers.ContentEncoding)
                        System.Diagnostics.Debug.WriteLine(en);
                }
            }
        }
    }
}
