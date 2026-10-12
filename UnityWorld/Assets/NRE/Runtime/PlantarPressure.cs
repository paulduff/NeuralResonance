using System;
using System.Collections.Generic;

namespace Nre.UnityWorld
{
    public readonly struct PlantarContact
    {
        public readonly float X,Z;
        public PlantarContact(float x,float z){X=x;Z=z;}
    }
    [Serializable] public sealed class FootPressureFrame
    {
        public float heelMedialLoadNewtons,heelLateralLoadNewtons,forefootMedialLoadNewtons,forefootLateralLoadNewtons;
    }
    public static class PlantarPressure
    {
        // The solver exposes aggregate link impulse. Distribute that measured
        // load uniformly over floor contacts, retaining anatomical side and sum.
        public static FootPressureFrame Capture(bool left,float load,IReadOnlyList<PlantarContact> contacts)
        {
            if(float.IsNaN(load)||float.IsInfinity(load)||load<0)throw new ArgumentException("Invalid plantar load.");
            var result=new FootPressureFrame();
            if(contacts.Count==0||load==0)return result;
            float each=load/contacts.Count;
            foreach(var point in contacts){
                if(float.IsNaN(point.X)||float.IsInfinity(point.X)||float.IsNaN(point.Z)||float.IsInfinity(point.Z))
                    throw new ArgumentException("Invalid plantar contact.");
                bool medial=left?point.X>=0:point.X<=0,forefoot=point.Z>=0;
                if(forefoot){if(medial)result.forefootMedialLoadNewtons+=each;else result.forefootLateralLoadNewtons+=each;}
                else{if(medial)result.heelMedialLoadNewtons+=each;else result.heelLateralLoadNewtons+=each;}
            }
            return result;
        }
    }
}
