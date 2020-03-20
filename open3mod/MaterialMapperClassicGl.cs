///////////////////////////////////////////////////////////////////////////////////
// Open 3D Model Viewer (open3mod) (v2.0)
// [MaterialMapperClassicGl.cs]
// (c) 2012-2015, Open3Mod Contributors
//
// Licensed under the terms and conditions of the 3-clause BSD license. See
// the LICENSE file in the root folder of the repository for the details.
//
// HIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
///////////////////////////////////////////////////////////////////////////////////

using Assimp;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace open3mod
{
    public sealed class MaterialMapperClassicGl : MaterialMapper
    {

        internal MaterialMapperClassicGl(Scene scene)
            : base(scene)
        { }



        public override void Dispose()
        {
            // no dispose semantics in this implementation
        }



        public override void ApplyMaterial(Mesh mesh, Material mat, bool textured, bool shaded)
        {
            ApplyFixedFunctionMaterial(mesh, mat, textured, shaded);
        }


        public override void ApplyGhostMaterial(Mesh mesh, Material material, bool shaded)
        {
            ApplyFixedFunctionGhostMaterial(mesh, material, shaded);
        }

        public override void BeginScene(Renderer renderer)
        {
            // set fixed-function lighting parameters
            GL.ShadeModel(ShadingModel.Smooth);
            //GL.LightModel(LightModelParameter.LightModelAmbient, new[] { 0.3f, 0.3f, 0.3f, 1 });
            //var ambient = 0.2f + (GraphicsSettings.Default.OutputBrightness / 100.0f) * 0.4f;
            var ambient = 0.3f + (GraphicsSettings.Default.OutputBrightness / 100.0f) * 0.4f;
            //var ambient = (0.25f + 1.5f * GraphicsSettings.Default.OutputBrightness / 100.0f) * 1.5f;
            //var ambient = 0.2f;
            //var ambient = 1f;
            GL.LightModel(LightModelParameter.LightModelAmbient, new[] { ambient, ambient, ambient, 1 });
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);

            // light direction
            var dir = new Vector3(1, 1, 0);
            var mat = renderer.LightRotation;
            Vector3.TransformNormal(ref dir, ref mat, out dir);
            GL.Light(LightName.Light0, LightParameter.Position, new float[] { dir.X, dir.Y, dir.Z, 0 });

            // light color
            var Diffuse = 0.5f +  (GraphicsSettings.Default.OutputBrightness / 100.0f)*1f;
            var Specular =  0.8f + (GraphicsSettings.Default.OutputBrightness / 100.0f) * 1f;
            //var Diffuse = (0.25f + 1.5f * GraphicsSettings.Default.OutputBrightness / 100.0f) * 1.5f;
            //var Specular = (0.25f + 1.5f * GraphicsSettings.Default.OutputBrightness / 100.0f) * 1.5f;
            //var Diffuse = 0.5f;
            GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { Diffuse, Diffuse, Diffuse, 1 });
            GL.Light(LightName.Light0, LightParameter.Specular, new float[] { Specular, Specular, Specular, 1 });
        }

        public override void EndScene(Renderer renderer)
        {
            GL.Disable(EnableCap.Lighting);
        }


        private void ApplyFixedFunctionMaterial(Mesh mesh, Material mat, bool textured, bool shaded)
        {
            shaded = shaded && (mesh == null || mesh.HasNormals);
            if (shaded)
            {
                GL.Enable(EnableCap.Lighting);
            }
            else
            {
                GL.Disable(EnableCap.Lighting);
            }

            var hasColors = mesh != null && mesh.HasVertexColors(0);
            if (hasColors)
            {
                GL.Enable(EnableCap.ColorMaterial);
                GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
            }
            else
            {
                GL.Disable(EnableCap.ColorMaterial);
            }

            // note: keep semantics of hasAlpha consistent with IsAlphaMaterial()
            var hasAlpha = false;
            var hasTexture = false;

            // note: keep this up-to-date with the code in UploadTextures()
            if (textured && mat.GetMaterialTextureCount(TextureType.Diffuse) > 0)
            {
                hasTexture = true;

                TextureSlot tex;
                mat.GetMaterialTexture(TextureType.Diffuse, 0, out tex);
                var gtex = _scene.TextureSet.GetOriginalOrReplacement(tex.FilePath);

                //hasAlpha = hasAlpha || gtex.HasAlpha == Texture.AlphaState.HasAlpha;
                hasAlpha = hasAlpha || mat.HasTextureOpacity;

                if (gtex.State == Texture.TextureState.GlTextureCreated)
                {
                    GL.ActiveTexture(TextureUnit.Texture0);
                    gtex.BindGlTexture();
   
                    GL.Enable(EnableCap.Texture2D);
                }
                else
                {
                    GL.Disable(EnableCap.Texture2D);
                }
            }
            else
            {
                GL.Disable(EnableCap.Texture2D);
            }         

            GL.Enable(EnableCap.Normalize);

            var alpha = 1.0f;
            // Assimp always return true with obj/mtl
            // suppress zero opacity, this is likely wrong input data
            // But this may cause alpha 0.0f not working
            if (mat.HasOpacity && mat.Opacity > AlphaSuppressionThreshold && mat.Opacity != 1.0f)
            {
                alpha = mat.Opacity;
            }

            var color = new Color4(.5f, .5f, .5f, 1.0f);
            Color4 c;
            if (mat.HasColorDiffuse && mat.Name != "DefaultMaterial")
            {
                color = AssimpToOpenTk.FromColor(mat.ColorDiffuse);
                if (color.A < AlphaSuppressionThreshold) // s.a.
                {
                    color.A = 1.0f;
                }
            }
            color.A *= alpha;
            hasAlpha = hasAlpha || color.A < 1.0f;

            if (shaded)
            {
                // if the material has a texture but the diffuse color texture is all black,
                // then heuristically assume that this is an import/export flaw and substitute
                // white.
                if (hasTexture && color.R < 1e-3f && color.G < 1e-3f && color.B < 1e-3f)
                {
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, new Color4(1.0f, 1.0f, 1.0f, alpha));
                }
                else
                {
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, color);
                }


                //color = new Color4(0, 0, 0, 1.0f);
                //color = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
                color = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                if (mat.HasColorSpecular && mat.Name != "DefaultMaterial")
                {
                    c = AssimpToOpenTk.FromColor(mat.ColorSpecular);
                    if (c.R + c.G + c.B > 0.0f)
                    {
                        color = c;
                    }
                }
                color.A *= alpha;
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, color);

                // Assimp alaways returns HasColorAmbient with {0,0,0,1} when source file has no setting.
                // This makes ambient light becomes all black
                // (The same issue of diffuse color has been fixed above)
                // workaround: Shift color range from 1~0 to 1~(min ambient)
                float minAmb = 0.2f;
                color = new Color4(minAmb, minAmb, minAmb, 1.0f);
                //color = new Color4(.2f, .2f, .2f, 1.0f);
                if (mat.HasColorAmbient)
                {
                    color = AssimpToOpenTk.FromColor(mat.ColorAmbient);
                    float shiftC(float oc) => oc * (1.0f - minAmb) + minAmb;
                    color.R = shiftC(color.R);
                    color.G = shiftC(color.G);
                    color.B = shiftC(color.B);
                }
                color.A *= alpha;
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, color);

                color = new Color4(0, 0, 0, 1.0f);
                if (mat.HasColorEmissive)
                {
                    c = AssimpToOpenTk.FromColor(mat.ColorEmissive);
                    if (c.R + c.G + c.B > 0.0f)
                    {
                        color = c;
                    }
                }
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Emission, color);

                // Assimp seems convert obj/mtl Ns value to Shininess and the strength value is not set
                float shininess = 1;
                float strength = 1;
                if (mat.HasShininess && mat.Shininess > 0.0f)
                {
                    shininess = mat.Shininess;
                }
                // todo: I don't even remember how shininess strength was supposed to be handled in assimp
                if (mat.HasShininessStrength && mat.ShininessStrength > 0.0f)
                {
                    strength = mat.ShininessStrength;
                }

                var exp = shininess * strength;
                if (exp >= 128.0f) // 128 is the maximum exponent as per the Gl spec
                {
                    exp = 128.0f;
                }

                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, exp);
            }
            else if (!hasColors)
            {
                GL.Color3(color.R, color.G, color.B);
            }

            if (hasAlpha)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                GL.DepthMask(false);
            }
            else
            {
                GL.Disable(EnableCap.Blend);
                GL.DepthMask(true);
            }
        }


        private void ApplyFixedFunctionGhostMaterial(Mesh mesh, Material mat, bool shaded)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.DepthMask(false);

            var color = new Color4(.6f, .6f, .9f, 0.15f);           

            shaded = shaded && (mesh == null || mesh.HasNormals);
            if (shaded)
            {
                GL.Enable(EnableCap.Lighting);

                
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, color);

                color = new Color4(1, 1, 1, 0.4f);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, color);

                color = new Color4(.2f, .2f, .2f, 0.1f);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, color);

                color = new Color4(0, 0, 0, 0.0f);       
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Emission, color);

                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 16.0f);
            }
            else
            {
                GL.Disable(EnableCap.Lighting);
                GL.Color3(color.R, color.G, color.B);
            }

            GL.Disable(EnableCap.ColorMaterial);
            GL.Disable(EnableCap.Texture2D);
        }
    }
}

/* vi: set shiftwidth=4 tabstop=4: */ 