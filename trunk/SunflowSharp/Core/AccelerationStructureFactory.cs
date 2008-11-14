using System;
using SunflowSharp.Core.Accel;
using SunflowSharp.Systems;

namespace SunflowSharp.Core
{
    public class AccelerationStructureFactory
    {
        public static AccelerationStructure create(string name, int n, bool primitives)
        {
            if (name == null || name == "auto")
            {
                if (primitives)
                {
                    if (n > 20000000)
                        return new UniformGrid();
                    else if (n > 2000000)
                        return new BoundingIntervalHierarchy();
                    else if (n > 2)
                        return new KDTree();
                    else
                        return new NullAccelerator();
                }
                else
                {
                    if (n > 2)
                        return new BoundingIntervalHierarchy();
                    else
                        return new NullAccelerator();
                }
            }
            else if (name == "uniformgrid")
                return new UniformGrid();
            else if (name == "null")
                return new NullAccelerator();
            else if (name == "kdtree")
                return new KDTree();
            else if (name == "bih")
                return new BoundingIntervalHierarchy();
            else
            {
                UI.printWarning(UI.Module.ACCEL, "Unrecognized intersection accelerator \"{0}\" - using auto", name);
                return create(null, n, primitives);
            }
        }
    }
}