using System;
using SunflowSharp;
using SunflowSharp.Maths;
using SunflowSharp.Systems;
using SunflowSharp.Systems.Ui;

namespace SunflowSharp.Core
{

    /**
     * This represents an instance of a {@link Geometry} into the scene. This class
     * maps object space to world space and maintains a list of shaders and
     * modifiers attached to the surface.
     */
    public class Instance : RenderObject
    {
        private Matrix4 o2w;
        private Matrix4 w2o;
        private BoundingBox bounds;
        private Geometry geometry;
        private IShader[] shaders;
        private Modifier[] modifiers;

        public bool update(ParameterList pl, SunflowAPI api)
        {
            string geometryName = pl.getstring("geometry", null);
            if (geometry == null || geometryName != null)
            {
                if (geometryName == null)
                {
                    UI.printError(UI.Module.GEOM, "geometry parameter missing - unable to create instance");
                    return false;
                }
                geometry = api.lookupGeometry(geometryName);
                if (geometry == null)
                {
                    UI.printError(UI.Module.GEOM, "Geometry \"{0}\" was not declared yet - instance is invalid", geometryName);
                    return false;
                }
            }
            string[] shaderNames = pl.getstringArray("shaders", null);
            if (shaderNames != null)
            {
                // new shader names have been provided
                shaders = new IShader[shaderNames.Length];
                for (int i = 0; i < shaders.Length; i++)
                {
                    shaders[i] = api.lookupShader(shaderNames[i]);
                    if (shaders[i] == null)
                        UI.printWarning(UI.Module.GEOM, "Shader \"{0}\" was not declared yet - ignoring", shaderNames[i]);
                }
            }
            else
            {
                // re-use existing shader array
            }
            string[] modifierNames = pl.getstringArray("modifiers", null);
            if (modifierNames != null)
            {
                // new modifier names have been provided
                modifiers = new Modifier[modifierNames.Length];
                for (int i = 0; i < modifiers.Length; i++)
                {
                    modifiers[i] = api.lookupModifier(modifierNames[i]);
                    if (modifiers[i] == null)
                        UI.printWarning(UI.Module.GEOM, "Modifier \"{0}\" was not declared yet - ignoring", modifierNames[i]);
                }
            }
            Matrix4 transform = pl.getMatrix("transform", o2w);
            if (transform != o2w)
            {
                o2w = transform;
                if (o2w != null)
                {
                    w2o = o2w.inverse();
                    if (w2o == null)
                    {
                        UI.printError(UI.Module.GEOM, "Unable to compute transform inverse - determinant is: {0}", o2w.determinant());
                        return false;
                    }
                }
                else
                    o2w = w2o = null;
            }
            return true;
        }

        /**
         * Recompute world space bounding box of this instance.
         */
        public void updateBounds()
        {
            bounds = geometry.getWorldBounds(o2w);
        }

        /**
         * Checks to see if this instance is relative to the specified geometry.
         * 
         * @param g geometry to check against
         * @return <code>true</code> if the instanced geometry is equals to g,
         *         <code>false</code> otherwise
         */
        public bool hasGeometry(Geometry g)
        {
            return geometry == g;
        }

        /**
         * Remove the specified shader from the instance's list if it is being used.
         * 
         * @param s shader to remove
         */
        public void removeShader(IShader s)
        {
            if (shaders != null)
            {
                for (int i = 0; i < shaders.Length; i++)
                    if (shaders[i] == s)
                        shaders[i] = null;
            }
        }

        /**
         * Remove the specified modifier from the instance's list if it is being
         * used.
         * 
         * @param m modifier to remove
         */
        public void removeModifier(Modifier m)
        {
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Length; i++)
                    if (modifiers[i] == m)
                        modifiers[i] = null;
            }
        }

        /**
         * Get the world space bounding box for this instance.
         * 
         * @return bounding box in world space
         */
        public BoundingBox getBounds()
        {
            return bounds;
        }

        public int getNumPrimitives()
        {
            return geometry.getNumPrimitives();
        }

        public void intersect(Ray r, IntersectionState state)
        {
            Ray localRay = r.transform(w2o);
            state.current = this;
            geometry.intersect(localRay, state);
            // FIXME: transfer max distance to current ray
            r.setMax(localRay.getMax());
        }

        /**
         * Prepare the shading state for shader invocation. This also runs the
         * currently attached surface modifier.
         * 
         * @param state shading state to be prepared
         */
        public void prepareShadingState(ShadingState state)
        {
            geometry.prepareShadingState(state);
            if (state.getNormal() != null && state.getGeoNormal() != null)
                state.correctShadingNormal();
            // run modifier if it was provided
            if (state.getModifier() != null)
                state.getModifier().modify(state);
        }

        /**
         * Get a shader for the instance's list.
         * 
         * @param i index into the shader list
         * @return requested shader, or <code>null</code> if the input is invalid
         */
        public IShader getShader(int i)
        {
            if (shaders == null || i < 0 || i >= shaders.Length)
                return null;
            return shaders[i];
        }

        /**
         * Get a modifier for the instance's list.
         * 
         * @param i index into the modifier list
         * @return requested modifier, or <code>null</code> if the input is
         *         invalid
         */
        public Modifier getModifier(int i)
        {
            if (modifiers == null || i < 0 || i >= modifiers.Length)
                return null;
            return modifiers[i];
        }

        /**
         * Transform the given point from object space to world space. A new
         * {@link Point3} object is returned.
         * 
         * @param p object space position to transform
         * @return transformed position
         */
        public Point3 transformObjectToWorld(Point3 p)
        {
            return o2w == null ? new Point3(p) : o2w.transformP(p);
        }

        /**
         * Transform the given point from world space to object space. A new
         * {@link Point3} object is returned.
         * 
         * @param p world space position to transform
         * @return transformed position
         */
        public Point3 transformWorldToObject(Point3 p)
        {
            return o2w == null ? new Point3(p) : w2o.transformP(p);
        }

        /**
         * Transform the given normal from object space to world space. A new
         * {@link Vector3} object is returned.
         * 
         * @param n object space normal to transform
         * @return transformed normal
         */
        public Vector3 transformNormalObjectToWorld(Vector3 n)
        {
            return o2w == null ? new Vector3(n) : w2o.transformTransposeV(n);
        }

        /**
         * Transform the given normal from world space to object space. A new
         * {@link Vector3} object is returned.
         * 
         * @param n world space normal to transform
         * @return transformed normal
         */
        public Vector3 transformNormalWorldToObject(Vector3 n)
        {
            return o2w == null ? new Vector3(n) : o2w.transformTransposeV(n);
        }

        /**
         * Transform the given vector from object space to world space. A new
         * {@link Vector3} object is returned.
         * 
         * @param v object space vector to transform
         * @return transformed vector
         */
        public Vector3 transformVectorObjectToWorld(Vector3 v)
        {
            return o2w == null ? new Vector3(v) : o2w.transformV(v);
        }

        /**
         * Transform the given vector from world space to object space. A new
         * {@link Vector3} object is returned.
         * 
         * @param v world space vector to transform
         * @return transformed vector
         */
        public Vector3 transformVectorWorldToObject(Vector3 v)
        {
            return o2w == null ? new Vector3(v) : w2o.transformV(v);
        }

        public PrimitiveList getBakingPrimitives()
        {
            return geometry.getBakingPrimitives();
        }

        public Geometry getGeometry()
        {
            return geometry;
        }
    }
}