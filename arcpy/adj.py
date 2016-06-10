#-------------------------------------------------------------------------------
# Name:        adj
# Purpose:
#
# Author:      hellocomrade
#
# Created:     06/08/2015
# Copyright:   (c) hellocomrade 2015
# Licence:     <your licence>
#-------------------------------------------------------------------------------
import math
import cPickle as pickle
import arcpy
import vertex1
#import arcpy.da
TOLERENCE=1e-8
BREAKPNT=10000
arcpy.env.workspace=r"C:\Users\Documents"
FlowlineLyr="flowlines"
VertexLyr="vertex"
DownstreamEdgeLyr="downstreamedge"
DownstreamVertexLyr="downstreamvertex"

def main():
    adjDict={}
    brkCnt=0
    with arcpy.da.SearchCursor("junctionpoint.shp",("FID","SHAPE@XY")) as cursor:
        for row in cursor:
            if brkCnt>BREAKPNT:
                break
            else:
                brkCnt+=1
                if brkCnt%1000==0:
                    print "Up to {0}".format(brkCnt)
            #print("******************")
            #print("Work on vertex with ID:{0}".format(row[0]))
            arcpy.MakeFeatureLayer_management("junctionpoint.shp",VertexLyr,'"FID"={0}'.format(row[0]))
            arcpy.MakeFeatureLayer_management("flowline.shp",FlowlineLyr)
            arcpy.SelectLayerByLocation_management(FlowlineLyr,"INTERSECT",VertexLyr)
            dvlst=[]
            incomingCnt=0
            with arcpy.da.SearchCursor(FlowlineLyr,("FID","FlowDir","SHAPE@","FTYPE","V0001E_MA","V0001E_MM","V0001E_01","V0001E_02","V0001E_03","V0001E_04","V0001E_05","V0001E_06","V0001E_07","V0001E_08","V0001E_09","V0001E_10","V0001E_11","V0001E_12")) as innerCursor:
                for inneRow in innerCursor:
                    #print("intersected with line with OID:{0}".format(inneRow[0]))
                    if inneRow[3] =="Coastline": #not in ("StreamRiver","ArtificialPath","Connector"):
                        continue
                    if inneRow[1]==1:#with digizied=1
                        startPnt=inneRow[2].firstPoint
                        endPnt=inneRow[2].lastPoint
                        distfeet=inneRow[2].getLength('Geodesic','Feet')
                        if math.fabs(row[1][0]-startPnt.X)<TOLERENCE and math.fabs(row[1][1]-startPnt.Y)<TOLERENCE:
                            #print("Find an outgoing edge")
                            #pnt=arcpy.PointGeometry(endPnt)
                            #arcpy.MakeFeatureLayer_management("NHD_Flowline",DownstreamEdgeLyr,'"OBJECTID"={0}'.format(inneRow[0]))
                            arcpy.MakeFeatureLayer_management("junctionpoint.shp",DownstreamVertexLyr)
                            arcpy.SelectLayerByLocation_management(DownstreamVertexLyr,"INTERSECT",inneRow[2])
                            count = int(arcpy.GetCount_management(DownstreamVertexLyr).getOutput(0))
                            if count==2:
                                with arcpy.da.SearchCursor(DownstreamVertexLyr,("FID","SHAPE@XY")) as dCursor:
                                    for dRow in dCursor:
                                        if math.fabs(dRow[1][0]-row[1][0])>=TOLERENCE or math.fabs(dRow[1][1]-row[1][1])>=TOLERENCE:
                                            #print("Find a downstream vertex with id:{0}".format(dRow[0]))
                                            #velocities=(inneRow[4],inneRow[5],inneRow[6],inneRow[7],inneRow[8],inneRow[9],inneRow[10],inneRow[11],inneRow[12],inneRow[13],inneRow[14],inneRow[15],inneRow[16],inneRow[17])
                                            #dvlst.append(vertex.DownstreamVertex(dRow[0],velocities))
                                            dvlst.append(vertex1.DownstreamVertex(dRow[0],inneRow[0],[distfeet/v if v>0 else 0 for v in (inneRow[4],inneRow[5],inneRow[6],inneRow[7],inneRow[8],inneRow[9],inneRow[10],inneRow[11],inneRow[12],inneRow[13],inneRow[14],inneRow[15],inneRow[16],inneRow[17])]))
                            else:
                                pass
                                #print("Should not reach here...")
                            arcpy.Delete_management(DownstreamVertexLyr)
                            #arcpy.Delete_management(pnt)
                        elif math.fabs(row[1][0]-endPnt.X)<TOLERENCE and math.fabs(row[1][1]-endPnt.Y)<TOLERENCE:
                            #print("Find an incoming edge")
                            incomingCnt+=1
                        else:
                            pass
                            #print("Should not reach here...")
                    else:
                        pass
                        #print("Skip this line since flowdir is not defined.")
                if len(dvlst)>0 or incomingCnt>0:
                    #if(len(dvlst))>1:
                    #    print row[0]
                    adjDict[row[0]]=vertex1.Vertex(row[0],incomingCnt,dvlst)
            arcpy.Delete_management(FlowlineLyr)
            arcpy.Delete_management(VertexLyr)
            #print("******************")
        with open("graph.adj","wb") as file:
            pickle.dump(adjDict,file,1)

if __name__ == '__main__':
    main()
