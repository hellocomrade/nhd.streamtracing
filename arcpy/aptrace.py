#-------------------------------------------------------------------------------
# Name:        aptrace
# Purpose:
#
# Author:      hellocomrade 
#
# Created:     10/08/2015
# Copyright:   (c) hellocomrade 2015
# Licence:     <your licence>
#-------------------------------------------------------------------------------
import os
import math
import time
import heapq
import cPickle as pickle
import arcpy
import vertex1

TOLERENCE=1e-8
defaultVal=None
FlowlineLyr="flowlines"
FlowlineLyr1="flowlines1"
FlowlineShp="flowline.shp"
VertexLyr="vertex"
VertexShp="junctionpoint.shp"
UserFlag = 'UserFlag'
ResultPoints='ResultPnts'
flowDict = {'MEAN_ANNUAL':4,'MONTH_MAX':5,'JANUARY':6,'FEBRUARY':7,'MARCH':8,'APRIL':9,'MAY':10,'JUNE':11,'JULY':12,'AUGUST':13,'SEPTEMBER':14,'OCTOBER':15,'NOVEMBER':16,'DECEMBER':17}
arcpy.env.workspace="./"
spatial_reference = arcpy.Describe(VertexShp).spatialReference

def arcpyPost(pnts,startPnt):
    lyr=arcpy.GetParameterAsText(2)
    ids=[str(p.eid-1) for p in pnts]
    whereclause='"FID" in ({0})'.format(",".join(ids))
    lookup={}
    for p in pnts:
        if lookup.get(p.eid-1) is not None:
            lookup[p.eid-1].append(p)
        else:
            lookup[p.eid-1]=[p]
    arcpy.MakeFeatureLayer_management(FlowlineShp,FlowlineLyr1,whereclause)
    #if fids have duplicates, shp file based query will remove duplicats and only return once for this id
    with arcpy.da.SearchCursor(FlowlineLyr1,["FID","SHAPE@"]) as cursor:
        for row in cursor:
            pntl=lookup[row[0]]
            for pnt in pntl:
                if pnt.percent==1:
                    pnt.pointGeom=arcpy.PointGeometry(row[1].lastPoint)
                else:
                    pnt.pointGeom=row[1].positionAlongLine(pnt.percent,True)

    arcpy.CreateFeatureclass_management("in_memory",os.path.basename(lyr),"POINT",None,"DISABLED","DISABLED",spatial_reference)
    arcpy.AddField_management(lyr,"cost","DOUBLE")
    with arcpy.da.InsertCursor(lyr,['SHAPE@','cost']) as cursor:
        for p in pnts:
            cursor.insertRow([p.pointGeom,p.cat])
        cursor.insertRow([startPnt,0])
    arcpy.SetParameter(2,lyr)
    arcpy.Delete_management(FlowlineLyr)

def arcpyPre(adj,costIndex):
    in_userFlag = arcpy.GetParameter(0)
    uFla = int(arcpy.GetCount_management(in_userFlag).getOutput(0))
    rt=-3
    if uFla==1:
        arcpy.MakeFeatureLayer_management(in_userFlag, UserFlag)
        arcpy.MakeFeatureLayer_management(FlowlineShp,FlowlineLyr)
        arcpy.Snap_edit(UserFlag, [[VertexShp, 'END', '1 Meters'],[FlowlineShp, 'EDGE', '150 Meters']])
        arcpy.SelectLayerByLocation_management(UserFlag, 'INTERSECT', FlowlineShp)
        sFla = int(arcpy.GetCount_management(UserFlag).getOutput(0))
        if sFla==1:
            arcpy.SelectLayerByLocation_management(FlowlineLyr, 'INTERSECT', UserFlag)
            pnt=None
            id=0
            cost=0
            with arcpy.da.SearchCursor(UserFlag,"SHAPE@") as cursor:
                for row in cursor:
                    pnt=row[0]
                    break
            with arcpy.da.SearchCursor(FlowlineLyr,("FID","FlowDir","SHAPE@","FTYPE","V0001E_MA","V0001E_MM","V0001E_01","V0001E_02","V0001E_03","V0001E_04","V0001E_05","V0001E_06","V0001E_07","V0001E_08","V0001E_09","V0001E_10","V0001E_11","V0001E_12")) as cursor:
                for row in cursor:
                    if row[3] =="Coastline": #not in ("StreamRiver","ArtificialPath","Connector"):
                        rt=-2
                    if row[1]==1:#with digizied=1
                        onStart=False
                        length,overhead=0,0
                        percent=row[2].measureOnLine(pnt,True)#row[2].queryPointAndDistance(pnt,True)
                        if math.fabs(percent-1)<TOLERENCE:#endpoint on edge
                            cost=0
                        elif math.fabs(percent)<TOLERENCE:#startpoint on edge
                            onStart=True
                        elif row[costIndex]>0:#in between
                            length=row[2].getLength('Geodesic','Feet')
                            cost=length/row[costIndex]*(1-percent)
                            overhead=length/row[costIndex]*percent
                        arcpy.MakeFeatureLayer_management(VertexShp, VertexLyr)
                        arcpy.SelectLayerByLocation_management(VertexLyr, 'INTERSECT', row[2])
                        with arcpy.da.SearchCursor(VertexLyr,("FID","SHAPE@XY")) as cursor1:
                            for row1 in cursor1:
                                if onStart and math.fabs(row1[1][0]-row[2].firstPoint.X)<TOLERENCE and math.fabs(row1[1][1]-row[2].firstPoint.Y)<TOLERENCE:
                                    id=row1[0]+1
                                    break
                                if not onStart and math.fabs(row1[1][0]-row[2].lastPoint.X)<TOLERENCE and math.fabs(row1[1][1]-row[2].lastPoint.Y)<TOLERENCE:
                                    id=row1[0]+1
                                    break
                        #junction point id,initial cost,start point, edge id (offset by 1),
                        rt=(id,cost,pnt,row[0]+1,overhead)
                    else:
                        rt=-2
                    break
        else:
            rt=-1
        arcpy.Delete_management(FlowlineLyr)
        arcpy.Delete_management(UserFlag)
    else:
        arcpy.AddMessage("No location specified!")
    return rt

#q1 as a dict was originally created for tracking the vertex that has been already pushed onto the heap
#and it's necessary. Furthermore, since python heapq doesn't offer the capacity to update the priority
#on the fly, q1 also takes the role to track down the previously inserted vertex and since we push a list
#[cost, vertex] on the heap, we can mark it as useless by set list[1]=None. So when it popups, we simply
#ignore it. At the meantime, we create a new list with the updated cost on the same vertext and push it
#on the heap, which will trigger heapify on list q.
#
#pass by value or pass by reference? :)
def bfs(adj,id,initCost,costIdx,initEid):
    q=[] #priority queue for Dijkstra
    q1={} #dict for checking footprint
    g=vertex1.Gates([3600*h for h in (1,6,12,24)])
    root=vertex1.CostVertex(id,-1,None,initCost)
    heapq.heappush(q,[root.cost,root])
    q1[root.id]=[root.cost,root]
    while len(q)>0:
        vex=heapq.heappop(q)[1]
        if vex is None:#this is the entry in the heap that has already been updated, abandon it and move on
            continue
        if not adj.get(vex.id,defaultVal): #adj table doesn't have vertices that connect the edge with no velocity
            g.appendToDestination(vex)
            continue
        if not adj[vex.id].downstreamVertices:
            g.appendToDestination(vex)
        for dv in adj[vex.id].downstreamVertices:
            vdv=q1.get(dv.id)
            if vdv is None:
                vdv=vertex1.CostVertex(dv.id,dv.eid,vex,vex.cost+dv.cost[costIdx])
                heapq.heappush(q,[vdv.cost,vdv])
                q1[vdv.id]=[vdv.cost,vdv]
            elif vdv[-1].cost>vex.cost+dv.cost[costIdx]:
                vdv_new=vertex1.CostVertex(dv.id,dv.eid,vex,vex.cost+dv.cost[costIdx])
                vdv[-1]=None
                heapq.heappush(q,[vdv_new.cost,vdv_new])
                q1[vdv.id]=[vdv_new.cost,vdv_new]

    for v in q1:
        g.filter(q1[v][1])
    #filter the segment between start point and the first junction point
    if initCost>0:#we need a virtual CostVertex here with an even virtual parent CostVertex
        g.filter(vertex1.CostVertex(id,initEid,vertex1.CostVertex(-1,-1,None,0),initCost),False)
    return g

def main():
    ssec=time.time()
    with open(os.path.join(os.path.dirname(os.path.realpath(__file__)),"graph-merged.adj"),"rb") as file:
        adj=pickle.load(file)
        arcpy.AddMessage("load file in:{0} seconds".format(time.time()-ssec))
        costIdx=flowDict.get(arcpy.GetParameterAsText(1))
        if costIdx is None:
            arcpy.AddMessage("Invalid flow velcoty choice")
        else:
            id=arcpyPre(adj,costIdx)
            arcpy.AddMessage("Arcpy prepare took {0} seconds".format(time.time()-ssec))
            if id==-3:
                arcpy.AddMessage("The flag can not be found")
                return
            elif id==-1:
                arcpy.AddMessage("The flag is too far away from the stream")
                return
            elif id==-2:
                arcpy.AddMessage("The stream you pick is not valid, either coastline or uninitialized")
                return
            ssec1=time.time()
            #cost index offset by 4 in the adjacency list
            g=bfs(adj,id[0],id[1],costIdx-4,id[3])
            arcpy.AddMessage("Seeking took {0} seconds".format(time.time()-ssec1))
            resultPnts=[]
            for t in g.results:
                for l in g.results[t]:
                    if l.aligned:
                        resultPnts.append(vertex1.GatePoint(l.eid,t,(t-l.parentime)/(l.time-l.parentime)))
                    else:#only on the first edge at which user pick the flag
                        resultPnts.append(vertex1.GatePoint(l.eid,t,(t+id[4])/(l.time+id[4])))
            for d in g.dest:
                resultPnts.append(vertex1.GatePoint(d.eid,-1,1.0))

            if len(resultPnts)==1 and resultPnts[0].eid==-1:
                arcpy.AddMessage("No possible trace from node: {0}".format(id))
            else:
                arcpyPost(resultPnts,id[2])

    arcpy.AddMessage("run:{0} seconds".format(time.time()-ssec))

if __name__ == '__main__':
    main()
