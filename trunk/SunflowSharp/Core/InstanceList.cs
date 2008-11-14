using System;
using SunflowSharp;
using SunflowSharp.Maths;

namespace SunflowSharp.Core
{

    public class InstanceList : PrimitiveList
    {
        private Instance[] instances;

        public InstanceList()
        {
            instances = new Instance[0];
        }

        public InstanceList(Instance[] instances)
        {
            this.instances = instances;
        }

        public float getPrimitiveBound(int primID, int i)
        {
            return instances[primID].getBounds().getBound(i);
        }

        public BoundingBox getWorldBounds(Matrix4 o2w)
        {
            BoundingBox bounds = new BoundingBox();
            foreach (Instance i in instances)
                bounds.include(i.getBounds());
            return bounds;
        }

        public void intersectPrimitive(Ray r, int primID, IntersectionState state)
        {
            instances[primID].intersect(r, state);
        }

        public int getNumPrimitives()
        {
            return instances.Length;
        }

        public int getNumPrimitives(int primID)
        {
            return instances[primID].getNumPrimitives();
        }

        public void prepareShadingState(ShadingState state)
        {
            state.getInstance().prepareShadingState(state);
        }

        public bool update(ParameterList pl, SunflowAPI api)
        {
            // TODO: build accelstructure into this (?)
            return true;
        }

        public PrimitiveList getBakingPrimitives()
        {
            return null;
        }
    }
}