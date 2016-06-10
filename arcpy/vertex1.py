#-------------------------------------------------------------------------------
# Name:       vertex1 
# Purpose:
#
# Author:      hellocomrade
#
# Created:     07/08/2015
# Copyright:   (c) hellocomrade 2015
# Licence:     <your licence>
#-------------------------------------------------------------------------------
class DownstreamVertex:
    def __init__(self,id,eid,costs):
        self.id=id
        self.eid=eid
        if len(costs)==14:
            self.cost=tuple(costs)
class Vertex:
    def __init__(self,id,incomingCnt,dvs):
        self.id=id
        self.incomingCnt=incomingCnt
        self.downstreamVertices=tuple(dvs)
class CostVertex(Vertex):
    def __init__(self,id,eid,parent,cost):
        self.id=id
        self.parent=parent
        self.cost=cost
        self.eid=eid
class Snapshot:
    def __init__(self,vid,time,eid,parentime,isAligned=True):
        self.vid=vid
        self.time=time
        self.eid=eid
        self.parentime=parentime
        self.aligned=isAligned

class GatePoint:
    def __init__(self,eid,cat,percent):
        self.eid=eid
        self.cat=cat
        self.percent=percent
        self.pointGeom=None

class Gates:
    def __init__(self,stoppers):
        self.stoppers=tuple(stoppers)
        self.threshold=None
        self.results=dict([(l,[]) for l in self.stoppers])
        self.dest=[]

    def filter(self,cv,isAligned=True):
        if cv.parent is None or cv.parent.cost is None:
            return
        for s in self.stoppers:
            if cv.parent.cost>s:
                continue
            elif cv.cost<s:
                break;
            if cv.parent.cost<=s and cv.cost>=s:
                self.results[s].append(Snapshot(cv.id,cv.cost,cv.eid,cv.parent.cost,isAligned))

    def appendToDestination(self,cv):
        self.dest.append(Snapshot(cv.id,cv.cost,cv.eid,self.threshold))
