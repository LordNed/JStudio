﻿using GameFormatReader.Common;
using JStudio.J3D.Animation;
using JStudio.J3D.ShaderGen;
using JStudio.JStudio.J3D.ExternalTypes;
using JStudio.OpenGL;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WArchiveTools;
using WindEditor;

namespace JStudio.J3D
{
    public partial class J3D : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Magic { get; protected set; }
        public string StudioType { get; protected set; }
        public string TotalFileSize { get { return string.Format("{0} bytes", m_totalFileSize); } }
        public string Name { get; protected set; }
        public FAABox BoundingBox { get; protected set; }
        public FSphere BoundingSphere { get; protected set; }

        public TevColorOverride TevColorOverrides { get { return m_tevColorOverrides; } }

        public INF1 INF1Tag { get; protected set; }
        public VTX1 VTX1Tag { get; protected set; }
        public MAT3 MAT3Tag { get; protected set; }
        public SHP1 SHP1Tag { get; protected set; }
        public JNT1 JNT1Tag { get; protected set; }
        public TEX1 TEX1Tag { get; private set; }
        public EVP1 EVP1Tag { get; private set; }
        public DRW1 DRW1Tag { get; private set; }

        public ObservableCollection<BCK> BoneAnimations { get { return m_boneAnimations; } }
        public ObservableCollection<BTK> MaterialAnimations { get { return m_materialAnimations; } }
        public ObservableCollection<BRK> RegisterAnimations { get { return m_registerAnimations; } }

        public ObservableCollection<BMT> ExternalMaterials { get { return m_externalMaterials; } }

        public BCK CurrentBoneAnimation
        {
            get { return m_currentBoneAnimation; }
            set { SetBoneAnimation(value != null ? value.Name : null); }
        }

        public BTK CurrentMaterialAnimation
        {
            get { return m_currentMaterialAnimation; }
            set { SetMaterialAnimation(value != null ? value.Name : null); }
        }

        public BMT CurrentExternalMaterial
        {
            get { return m_currentExternalMaterial; }
            set { SetExternalMaterial(value != null ? value.Name : null); }
        }

        public BRK CurrentRegisterAnimation
        {
            get { return m_currentRegisterAnimation; }
            set { SetRegisterAnimation(value != null ? value.Name : null); }
        }

        // Hack
        private Matrix4 m_viewMatrix;
        private Matrix4 m_projMatrix;
        private Matrix4 m_modelMatrix;
        private Material m_currentBoundMat;

        private GXLight[] m_hardwareLights = new GXLight[8];
        private TevColorOverride m_tevColorOverrides;
        private int m_hardwareLightBuffer;

        private Dictionary<string, Texture> m_textureOverrides;
        private Dictionary<string, bool> m_colorWriteOverrides;
        private ObservableCollection<BCK> m_boneAnimations;
        private ObservableCollection<BTK> m_materialAnimations;
        private ObservableCollection<BMT> m_externalMaterials;
        private ObservableCollection<BRK> m_registerAnimations;

        private BCK m_currentBoneAnimation;
        private BTK m_currentMaterialAnimation;
        private BMT m_currentExternalMaterial;
        private BRK m_currentRegisterAnimation;

        private bool m_skinningInvalid;
        private int m_totalFileSize;

        // To detect redundant calls
        private bool m_hasBeenDisposed = false;

        public J3D(string name)
        {
            Name = name;
        }

        public void LoadFromStream(EndianBinaryReader reader, bool dumpTextures = false, bool dumpShaders = false)
        {
            // Read the J3D Header
            Magic = new string(reader.ReadChars(4));
            StudioType = new string(reader.ReadChars(4));
            m_totalFileSize = reader.ReadInt32();
            int tagCount = reader.ReadInt32();

            // Skip over an unused tag ("SVR3") which is consistent in all models.
            reader.Skip(16);

            LoadTagDataFromFile(reader, tagCount, dumpTextures, dumpShaders);

            // Rendering Stuff
            m_hardwareLightBuffer = GL.GenBuffer();
            m_textureOverrides = new Dictionary<string, Texture>();
            m_colorWriteOverrides = new Dictionary<string, bool>();
            m_tevColorOverrides = new TevColorOverride();
            m_boneAnimations = new ObservableCollection<BCK>();
            m_materialAnimations = new ObservableCollection<BTK>();
            m_externalMaterials = new ObservableCollection<BMT>();
            m_registerAnimations = new ObservableCollection<BRK>();

            // Mark this as true when we first load so it moves non-animated pieces into the right area.
            m_skinningInvalid = true;
        }

        public void SetHardwareLight(int index, GXLight light)
        {
            if (index < 0 || index >= 8)
                throw new ArgumentOutOfRangeException("index", "index must be >= 0 or < 8. Maximum of 8 hardware lights supported!");

            m_hardwareLights[index] = light;

            // Fill the buffer with data at the chosen binding point
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, (int)ShaderUniformBlockIds.LightBlock, m_hardwareLightBuffer);
            GL.BufferData(BufferTarget.UniformBuffer, (IntPtr)(GXLight.SizeInBytes * 8), m_hardwareLights, BufferUsageHint.DynamicDraw);
        }

        public void SetTevColorOverride(int index, WLinearColor overrideColor)
        {
            m_tevColorOverrides.SetTevColorOverride(index, overrideColor);
        }

        public void SetTevkColorOverride(int index, WLinearColor overrideColor)
        {
            m_tevColorOverrides.SetTevkColorOverride(index, overrideColor);
        }

        public void LoadBoneAnimation(string bckFile)
        {
            string animName = Path.GetFileNameWithoutExtension(bckFile);
            BCK bck = new BCK(animName);

            using (var reader = FileUtilities.LoadFile(bckFile))
                bck.LoadFromStream(reader);

            m_boneAnimations.Add(bck);
        }

        public void LoadMaterialAnim(string btkFile)
        {
            string animName = Path.GetFileNameWithoutExtension(btkFile);
            BTK btk = new BTK(animName);

            using (var reader = FileUtilities.LoadFile(btkFile))
                btk.LoadFromStream(reader);

            m_materialAnimations.Add(btk);
        }

        public void LoadRegisterAnim(string brkFile)
        {
            string animName = Path.GetFileNameWithoutExtension(brkFile);
            BRK brk = new BRK(animName);

            using (var reader = FileUtilities.LoadFile(brkFile))
                brk.LoadFromStream(reader);

            m_registerAnimations.Add(brk);
        }

        public void LoadExternalMaterial(string bmtFile)
        {
            string fileName = Path.GetFileNameWithoutExtension(bmtFile);
            BMT bmt = new BMT(fileName);

            using (var reader = FileUtilities.LoadFile(bmtFile))
                bmt.LoadFromStream(reader);

            if (bmt.MaterialsCount != 0)
            {
                // Generate shaders for the loaded materials.
                Material dummyMat = null;
                AssignVertexAttributesToMaterialsRecursive(INF1Tag.HierarchyRoot, ref dummyMat, bmt.MAT3);

                // Now that the vertex attributes are assigned to the materials, generate a shader from the data.
                GenerateShadersForMaterials(bmt.MAT3, false);
            }

            m_externalMaterials.Add(bmt);
        }

        public void UnloadMaterialAnimations()
        {
            m_materialAnimations.Clear();
        }
        public void UnloadBoneAnimations()
        {
            m_boneAnimations.Clear();
        }

        public void UnloadExternalMaterials()
        {
            m_externalMaterials.Clear();
        }

        public void UnloadRegisterAnimations()
        {
            m_registerAnimations.Clear();
        }

        public void SetBoneAnimation(string animName)
        {
            BCK anim = null;
            if (!string.IsNullOrEmpty(animName))
            {
                anim = m_boneAnimations.FirstOrDefault(x => x.Name == animName);
                if (anim == null)
                {
                    Console.WriteLine("Failed to play animation {0}, animation not loaded!", animName);
                }
            }

            if (m_currentBoneAnimation != null)
                m_currentBoneAnimation.Stop();

            m_currentBoneAnimation = anim;

            if (m_currentBoneAnimation != null)
                m_currentBoneAnimation.Start();

            // We set this to true so that when we unload an animation it marks us as invalid for one frame so it updates our GPU mesh back to T-pose.
            m_skinningInvalid = true;

            // The setter for CurrentBoneAnimation calls this function, so broadcast the event here, instead of inside the setter.
            OnPropertyChanged("CurrentBoneAnimation");

            ForceAnimationRestart();
        }

        public void SetMaterialAnimation(string animName)
        {
            BTK anim = null;
            if (!string.IsNullOrEmpty(animName))
            {
                anim = m_materialAnimations.FirstOrDefault(x => x.Name == animName);
                if (anim == null)
                {
                    Console.WriteLine("Failed to play animation {0}, animation not loaded!", animName);
                }
            }

            if (m_currentMaterialAnimation != null)
                m_currentMaterialAnimation.Stop();

            m_currentMaterialAnimation = anim;

            if (m_currentMaterialAnimation != null)
                m_currentMaterialAnimation.Start();

            // The setter for CurrentMaterialAnimation calls this function, so broadcast the event here, instead of inside the setter.
            OnPropertyChanged("CurrentMaterialAnimation");

            ForceAnimationRestart();
        }

        public void SetRegisterAnimation(string animName)
        {
            BRK anim = null;
            if (!string.IsNullOrEmpty(animName))
            {
                anim = m_registerAnimations.FirstOrDefault(x => x.Name == animName);
                if (anim == null)
                {
                    Console.WriteLine("Failed to play register animation {0}, animation not loaded!", animName);
                }
            }

            if (m_currentRegisterAnimation != null)
                m_currentRegisterAnimation.Stop();

            m_currentRegisterAnimation = anim;

            if (m_currentRegisterAnimation != null)
                m_currentRegisterAnimation.Start();

            OnPropertyChanged("CurrentRegisterAnimation");

            ForceAnimationRestart();
        }

        public void SetExternalMaterial(string bmtName)
        {
            BMT extMat = null;
            if (!string.IsNullOrEmpty(bmtName))
            {
                extMat = m_externalMaterials.FirstOrDefault(x => x.Name == bmtName);
                if (extMat == null)
                {
                    Console.WriteLine("Failed to play external material {0}, external material not loaded!", bmtName);
                }
            }

            m_currentExternalMaterial = extMat;

            // The setter for CurrentMaterialAnimation calls this function, so broadcast the event here, instead of inside the setter.
            OnPropertyChanged("CurrentExternalMaterial");
        }

        private void ForceAnimationRestart()
        {
            foreach (BCK bck in m_boneAnimations)
            {
                bck.Stop();
                bck.Start();
            }

            foreach (BTK btk in m_materialAnimations)
            {
                btk.Stop();
                btk.Start();
            }

            foreach (BRK brk in m_registerAnimations)
            {
                brk.Stop();
                brk.Start();
            }
        }

        /// <summary>
        /// This is used to emulate a feature used in The Legend of Zelda: The Wind Waker. In WW all characters appear to share
        /// their Toon texture lookup image. The J3D files include a texture in their files which is a black/white checkerboard
        /// that is a placeholder for the one the game overrides. Unfortunately, if we use the black/white checkerboard then lighting
        /// gets broken, so this function is provided to optionally override a texture name with a specific texture, such as the
        /// texture name "ZBtoonEX" used by WW.
        /// </summary>
        /// <param name="textureName">Name of the texture to override. All textures with this name will be overriden.</param>
        /// <param name="filePath">Path to the texture to use.</param>
        public void SetTextureOverride(string textureName, string filePath)
        {
            BinaryTextureImage btiData = new BinaryTextureImage();
            btiData.LoadImageFromDisk(filePath);
            if (m_textureOverrides.ContainsKey(textureName))
                m_textureOverrides[textureName].Dispose();

            m_textureOverrides[textureName] = new Texture(textureName, btiData);
        }

        /// <summary>
        /// This is used to emulate a feature used in The Legend of Zelda: The Wind Waker. In WW Link and Tetra's eye/eyebrows are
        /// made from three material/mesh layers. The first layer writes to the alpha buffer a black and white image. The second layer
        /// is the one you actually see in game, and the third layer writes to the alpha buffer - but inverted from what the first layer 
        /// did. This lets them use the alpha buffer as a mask for the second layer. However, there's no support in the bmd/bdl formats to
        /// selectively disable color writes, which are required to keep the first and third layers from being visible. To solve this in
        /// Wind Waker, they apparently modify the bmd/bdl models as they're being sent to the GX - using Link and Tetra's models as rooms
        /// (which do not have the player code rendering them) and their eyes show up incorrectly.
        /// </summary>
        /// <param name="materialName">The name of the model's material that you want to enable/disable color writes for.</param>
        /// <param name="writesToColorBuffer">Whether or not this material should write to the color buffer.</param>
        public void SetColorWriteOverride(string materialName, bool writesToColorBuffer)
        {
            m_colorWriteOverrides[materialName] = writesToColorBuffer;
        }

        private void LoadTagDataFromFile(EndianBinaryReader reader, int tagCount, bool dumpTextures, bool dumpShaders)
        {
            for (int i = 0; i < tagCount; i++)
            {
                long tagStart = reader.BaseStream.Position;

                string tagName = reader.ReadString(4);
                int tagSize = reader.ReadInt32();

                switch (tagName)
                {
                    // INFO - Vertex Count, Scene Hierarchy
                    case "INF1":
                        INF1Tag = new INF1();
                        INF1Tag.LoadINF1FromStream(reader, tagStart);
                        break;
                    // VERTEX - Stores vertex arrays for pos/normal/color0/tex0 etc.
                    // Contains VertexAttributes which describe how the data is stored/laid out.
                    case "VTX1":
                        VTX1Tag = new VTX1();
                        VTX1Tag.LoadVTX1FromStream(reader, tagStart, tagSize);
                        break;
                    // ENVELOPES - Defines vertex weights for skinning
                    case "EVP1":
                        EVP1Tag = new EVP1();
                        EVP1Tag.LoadEVP1FromStream(reader, tagStart);
                        break;
                    // DRAW (Skeletal Animation Data) - Stores which matrices (?) are weighted, and which are used directly
                    case "DRW1":
                        DRW1Tag = new DRW1();
                        DRW1Tag.LoadDRW1FromStream(reader, tagStart);
                        break;
                    // JOINTS - Stores the skeletal joints (position, rotation, scale, etc...)
                    case "JNT1":
                        JNT1Tag = new JNT1();
                        JNT1Tag.LoadJNT1FromStream(reader, tagStart);
                        JNT1Tag.CalculateParentJointsForSkeleton(INF1Tag.HierarchyRoot);
                        break;
                    // SHAPE - Face/Triangle information for model.
                    case "SHP1":
                        SHP1Tag = new SHP1();
                        SHP1Tag.ReadSHP1FromStream(reader, tagStart, VTX1Tag.VertexData);
                        break;
                    // MATERIAL - Stores materials (which describes how textures, etc. are drawn)
                    case "MAT3":
                        MAT3Tag = new MAT3();
                        MAT3Tag.LoadMAT3FromStream(reader, tagStart);
                        break;
                    // TEXTURES - Stores binary texture images.
                    case "TEX1":
                        TEX1Tag = new TEX1();
                        TEX1Tag.LoadTEX1FromStream(reader, tagStart, dumpTextures);
                        break;
                    // MODEL - Seems to be bypass commands for Materials and invokes GX registers directly.
                    case "MDL3":
                        break;
                }

                // Skip the stream reader to the start of the next tag since it gets moved around during loading.
                reader.BaseStream.Position = tagStart + tagSize;
            }

            // To generate shaders we need to know which vertex attributes need to be enabled for the shader. However,
            // the shader has no knowledge in our book as to what attributes are enabled. Theoretically we could enable
            // them on the fly as something requested it, but that'd involve more code that I don't want to do right now.
            // To resolve, we iterate once through the hierarchy to see which mesh is called after a material and bind the
            // vertex descriptions.
            Material dummyMat = null;
            AssignVertexAttributesToMaterialsRecursive(INF1Tag.HierarchyRoot, ref dummyMat, MAT3Tag);

            // Now that the vertex attributes are assigned to the materials, generate a shader from the data.
            GenerateShadersForMaterials(MAT3Tag, dumpShaders);

            // Iterate through the shapes and calculate a bounding box which encompasses all of them.
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var shape in SHP1Tag.Shapes)
            {
                Vector3 sMin = shape.BoundingBox.Min;
                Vector3 sMax = shape.BoundingBox.Max;

                if (sMin.X < min.X)
                    min.X = sMin.X;
                if (sMax.X > max.X)
                    max.X = sMax.X;

                if (sMin.Y < min.Y)
                    min.Y = sMin.Y;
                if (sMax.Y > max.Y)
                    max.Y = sMax.Y;

                if (sMin.Z < min.Z)
                    min.Z = sMin.Z;
                if (sMax.Z > max.Z)
                    max.Z = sMax.Z;
            }

            BoundingBox = new FAABox(min, max);
            BoundingSphere = new FSphere(BoundingBox.Center, BoundingBox.Max.Length);
        }

        public void GenerateShadersForMaterials(MAT3 mat3Tag, bool dumpShaders = false)
        {
            foreach (var material in mat3Tag.MaterialList)
            {
                if (material.VtxDesc == null)
                {
                    Console.WriteLine("Skipping generating Shader for Unreferenced Material: {0}", material);
                    continue;
                }
                material.Shader = TEVShaderGenerator.GenerateShader(material, mat3Tag, dumpShaders);

                // Bind the Light Block uniform to the shader
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, (int)ShaderUniformBlockIds.LightBlock, m_hardwareLightBuffer);
                GL.UniformBlockBinding(material.Shader.Program, material.Shader.UniformLightBlock, (int)ShaderUniformBlockIds.LightBlock);

                // Bind the Pixel Shader uniform to the shader
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, (int)ShaderUniformBlockIds.PixelShaderBlock, material.Shader.PSBlockUBO);
                GL.UniformBlockBinding(material.Shader.Program, material.Shader.UniformPSBlock, (int)ShaderUniformBlockIds.PixelShaderBlock);
            }
        }

        public void AssignVertexAttributesToMaterialsRecursive(HierarchyNode curNode, ref Material curMaterial, MAT3 matTag)
        {
            switch (curNode.Type)
            {
                case HierarchyDataType.Material: curMaterial = matTag.MaterialList[matTag.MaterialRemapTable[curNode.Value]]; break;
                //case HierarchyDataType.Batch: curMaterial.VtxDesc = SHP1Tag.Shapes[SHP1Tag.ShapeRemapTable[curNode.Value]].VertexDescription; break;
            }

            foreach (var child in curNode.Children)
                AssignVertexAttributesToMaterialsRecursive(child, ref curMaterial, matTag);
        }

        public void Tick(float deltaTime)
        {
            foreach (var boneAnim in m_boneAnimations)
                boneAnim.Tick(deltaTime);

            foreach (var matAnim in m_materialAnimations)
                matAnim.Tick(deltaTime);

            foreach (var regAnim in m_registerAnimations)
                regAnim.Tick(deltaTime);

			if (m_currentBoneAnimation != null)
			{
				m_currentBoneAnimation.ApplyAnimationToPose(JNT1Tag.AnimatedJoints);
				m_skinningInvalid = true;
			}

			if (m_currentMaterialAnimation != null)
                m_currentMaterialAnimation.ApplyAnimationToMaterials(MAT3Tag);

            if (m_currentRegisterAnimation != null)
                m_currentRegisterAnimation.ApplyAnimationToMaterials(MAT3Tag, m_tevColorOverrides);
        }

        public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, Matrix4 modelMatrix, 
			bool bRenderOpaque = true, bool bRenderTranslucent = true, bool bRenderDepthOnlyPrePass = false)
        {
            m_viewMatrix = viewMatrix;
            m_projMatrix = projectionMatrix;
            m_modelMatrix = modelMatrix;

            IList<SkeletonJoint> boneList = (m_currentBoneAnimation != null) ? JNT1Tag.AnimatedJoints : JNT1Tag.BindJoints;
            DRW1Tag.UpdateMatrices(boneList, EVP1Tag);

            RenderMeshRecursive(INF1Tag.HierarchyRoot, bRenderOpaque, bRenderTranslucent, bRenderDepthOnlyPrePass);

            // We're going to restore some semblance of state after rendering ourselves, as models often modify weird and arbitrary GX values.
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Enable(EnableCap.Dither);
        }

        private void ApplyBonePositionsToAnimationTransforms(IList<SkeletonJoint> boneList, Matrix4[] boneTransforms)
        {
            for (int i = 0; i < boneList.Count; i++)
            {
                SkeletonJoint curJoint, origJoint;
                curJoint = origJoint = boneList[i];

                Matrix4 cumulativeTransform = Matrix4.Identity;
                while (true)
                {
                    Matrix4 jointMatrix = Matrix4.CreateScale(curJoint.Scale) * Matrix4.CreateFromQuaternion(curJoint.Rotation) * Matrix4.CreateTranslation(curJoint.Translation);
                    cumulativeTransform *= jointMatrix;
                    if (curJoint.Parent == null)
                        break;

                    curJoint = curJoint.Parent;
                }

                boneTransforms[i] = cumulativeTransform;

                if (origJoint.Parent != null)
                {
                    Vector3 curPos = cumulativeTransform.ExtractTranslation();
                    Vector3 parentPos = boneTransforms[boneList.IndexOf(origJoint.Parent)].ExtractTranslation();

                    //m_lineBatcher.DrawLine(curPos, parentPos, WLinearColor.Red, 1, 0);
                }
            }
        }

        private int m_shapeIndex;

        private void RenderMeshRecursive(HierarchyNode curNode, bool bRenderOpaque, bool bRenderTranslucent, bool bDepthOnlyPrePass)
        {
            switch (curNode.Type)
            {
                case HierarchyDataType.Material:
                    BindMaterialByIndex(curNode.Value, bDepthOnlyPrePass);
                    break;

                case HierarchyDataType.Batch:
                    if (m_currentBoundMat.IsTranslucent && bRenderTranslucent)
                    {
                        RenderBatchByIndex(curNode.Value);
                    }
					if(!m_currentBoundMat.IsTranslucent && bRenderOpaque)
					{
						RenderBatchByIndex(curNode.Value);
					}
					break;
            }

            switch (curNode.Type)
            {
                case HierarchyDataType.Batch:
                    if (m_currentBoundMat.IsTranslucent && bRenderTranslucent)
                    {
                        RenderBatchByIndex(curNode.Value);
                    }
                    break;
            }

            var reversedChildList = new List<HierarchyNode>(curNode.Children);
			reversedChildList.Reverse();
            foreach (var child in reversedChildList)
                RenderMeshRecursive(child, bRenderOpaque, bRenderTranslucent, bDepthOnlyPrePass);
        }

        private void BindMaterialByIndex(ushort index, bool bDepthOnlyPrePass)
        {
            MAT3 mat3 = MAT3Tag;
            TEX1 tex1 = TEX1Tag;

            if (m_currentExternalMaterial != null)
            {
                if (m_currentExternalMaterial.MaterialsCount != 0)
                    mat3 = m_currentExternalMaterial.MAT3;
                if (m_currentExternalMaterial.TexturesCount != 0)
                    tex1 = m_currentExternalMaterial.TEX1;
            }

            // While the game collapses duplicate materials via the material index remap table,
            // the actual original names are preserved with their original indexes through the
            // string table.
            string materialName = mat3.MaterialNameTable[index];

            Material material = mat3.MaterialList[mat3.MaterialRemapTable[index]];
            material.Bind();
            m_currentBoundMat = material;

            Shader shader = material.Shader;

            GL.UniformMatrix4(shader.UniformModelMtx, false, ref m_modelMatrix);
            GL.UniformMatrix4(shader.UniformViewMtx, false, ref m_viewMatrix);
            GL.UniformMatrix4(shader.UniformProjMtx, false, ref m_projMatrix);

            for (int i = 0; i < 8; i++)
            {
                int idx = material.TextureIndexes[i];
                if (idx < 0) continue;

                //int glTextureIndex = GL.GetUniformLocation(shader.Program, string.Format("Texture[{0}]", i));
                Texture tex = tex1.Textures[mat3.TextureRemapTable[idx]];

                // Before we bind the texture, we need to check if this particular texture has been overriden.
                // This allows textures to be replaced on a per-name basis with another file. Used in cases of
                // broken/incorrect texture included by default in models, ie: The Wind Waker toon textures.
                if (m_textureOverrides.ContainsKey(tex.Name))
                    tex = m_textureOverrides[tex.Name];

                GL.Uniform1(shader.UniformTextureSamplers[i], i); // Everything dies without this, don't forget this bit.
                tex.Bind(i);
            }

            if (shader.UniformTexMtx >= 0)
            {
                for (int i = 0; i < material.TexMatrixIndexes.Length; i++)
                {
                    Matrix4 matrix = material.TexMatrixIndexes[i].TexMtx;
                    string matrixString = string.Format("TexMtx[{0}]", i);
					if(material.TexMatrixIndexes[i].MatrixUniformLocationForGPU < 0)
					{
						// If the shader's broke this will try to re-assign every frame, but should vastly improve it most use cases.
						material.TexMatrixIndexes[i].MatrixUniformLocationForGPU = GL.GetUniformLocation(shader.Program, matrixString);
					}

                    GL.UniformMatrix4(material.TexMatrixIndexes[i].MatrixUniformLocationForGPU, true, ref matrix);
                }
            }

			if(shader.UniformPostTexMtx >= 0)
			{
				for(int i = 0; i < material.PostTexMatrixIndexes.Length; i++)
				{
					Matrix4 matrix = material.PostTexMatrixIndexes[i].TexMtx;

					string matrixString = string.Format("PostMtx[{0}]", i);

					if (material.PostTexMatrixIndexes[i].MatrixUniformLocationForGPU < 0)
					{
						material.PostTexMatrixIndexes[i].MatrixUniformLocationForGPU = GL.GetUniformLocation(shader.Program, matrixString);
					}

					GL.UniformMatrix4(material.PostTexMatrixIndexes[i].MatrixUniformLocationForGPU, true, ref matrix);
				}
			}

            var color0Amb = material.AmbientColorIndexes[0];
            var color0Mat = material.MaterialColorIndexes[0];
            var color1Amb = material.AmbientColorIndexes[1];
            var color1Mat = material.MaterialColorIndexes[1];

            if (shader.UniformColor0Amb >= 0) GL.Uniform4(shader.UniformColor0Amb, color0Amb.R, color0Amb.G, color0Amb.B, color0Amb.A);
            if (shader.UniformColor0Mat >= 0) GL.Uniform4(shader.UniformColor0Mat, color0Mat.R, color0Mat.G, color0Mat.B, color0Mat.A);
            if (shader.UniformColor1Amb >= 0) GL.Uniform4(shader.UniformColor1Amb, color1Amb.R, color1Amb.G, color1Amb.B, color1Amb.A);
            if (shader.UniformColor1Mat >= 0) GL.Uniform4(shader.UniformColor1Mat, color1Mat.R, color1Mat.G, color1Mat.B, color1Mat.A);

            // Set the OpenGL State
            GXToOpenGL.SetBlendState(material.BlendModeIndex);
            GXToOpenGL.SetCullState(material.CullMode);
            GXToOpenGL.SetDepthState(material.ZModeIndex, bDepthOnlyPrePass);
            GXToOpenGL.SetDitherEnabled(material.DitherIndex);

            // Check to see if we've overridden the material's ability to write to the color channel. This is used
            // to add support for bmd/bdl models who have this setting changed through game-code since the bmd/bdl
            // format does not appear to otherwise specify.
            if(m_colorWriteOverrides.ContainsKey(materialName))
            {
                bool enabled = m_colorWriteOverrides[materialName];
                GL.ColorMask(enabled, enabled, enabled, true);
            }
			else if(bDepthOnlyPrePass)
			{
				GL.ColorMask(false, false, false, false);
			}
            else
            {
                GL.ColorMask(true, true, true, true);
            }
            //if (WInput.GetKey(System.Windows.Input.Key.U))
            //    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            //else
            //    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);


            // Update the data in the PS Block
            PSBlock psData = new PSBlock();
            m_tevColorOverrides.SetPSBlockForMaterial(material, ref psData);
            UpdateTextureDimensionsForPSBlock(ref psData, material);
            UpdateFogForPSBlock(ref psData, material);

            // Upload the PS Block to the GPU
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, (int)ShaderUniformBlockIds.PixelShaderBlock, shader.PSBlockUBO);
            GL.BufferData<PSBlock>(BufferTarget.UniformBuffer, (IntPtr)(Marshal.SizeOf(psData)), ref psData, BufferUsageHint.DynamicDraw);
        }

        private void UpdateFogForPSBlock(ref PSBlock psData, Material mat)
        {
            psData.FogColor = mat.FogModeIndex.Color;
        }

        private void UpdateTextureDimensionsForPSBlock(ref PSBlock psData, Material mat)
        {
            for (int i = 0; i < 8; i++)
            {
                if (mat.TextureIndexes[i] < 0)
                    continue;

                Texture texture = TEX1Tag.Textures[MAT3Tag.TextureRemapTable[mat.TextureIndexes[i]]];
                Vector4 texDimensions = new Vector4(texture.CompressedData.Width, texture.CompressedData.Height, 0, 0);

                switch (i)
                {
                    case 0: psData.TexDimension0 = texDimensions; break;
                    case 1: psData.TexDimension1 = texDimensions; break;
                    case 2: psData.TexDimension2 = texDimensions; break;
                    case 3: psData.TexDimension3 = texDimensions; break;
                    case 4: psData.TexDimension4 = texDimensions; break;
                    case 5: psData.TexDimension5 = texDimensions; break;
                    case 6: psData.TexDimension6 = texDimensions; break;
                    case 7: psData.TexDimension7 = texDimensions; break;
                }
            }
        }


        private void RenderBatchByIndex(ushort index)
        {
            GL.CullFace(CullFaceMode.Back);
            GL.FrontFace(FrontFaceDirection.Cw);

            SHP1.Shape shape = SHP1Tag.Shapes[SHP1Tag.ShapeRemapTable[index]];
            //shape.Bind();
            //shape.Draw();
            //shape.Unbind();
        }

        public void DrawBoundsForJoints(bool boundingBox, bool boundingSphere, IDebugLineDrawer lineDrawer)
        {
            IList<SkeletonJoint> boneList = (m_currentBoneAnimation != null) ? JNT1Tag.AnimatedJoints : JNT1Tag.BindJoints;

            Matrix4[] boneTransforms = new Matrix4[boneList.Count];
            ApplyBonePositionsToAnimationTransforms(boneList, boneTransforms);

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                SkeletonJoint curJoint, origJoint;
                curJoint = origJoint = JNT1Tag.BindJoints[i];

                //Matrix4 cumulativeTransform = Matrix4.Identity;
                //while (true)
                //{
                //    Matrix4 jointMatrix = Matrix4.CreateScale(curJoint.Scale) * Matrix4.CreateFromQuaternion(curJoint.Rotation) * Matrix4.CreateTranslation(curJoint.Translation);
                //    cumulativeTransform *= jointMatrix;
                //    if (curJoint.Parent == null)
                //        break;

                //    curJoint = curJoint.Parent;
                //}

                //boneTransforms[i] = cumulativeTransform;
                boneTransforms[i].Transpose();
                Vector3 curPos = boneTransforms[i].ExtractTranslation();
                Quaternion curRot = boneTransforms[i].ExtractRotation();

                WLinearColor jointColor = origJoint.Unknown1 == 0 ? WLinearColor.Yellow : WLinearColor.Blue;
                if (boundingSphere)
                {
                    // Many bones have no radius, simply skip them to avoid adding them to the line renderer.
                    if (origJoint.BoundingSphereDiameter == 0f)
                        continue;

                    lineDrawer.DrawSphere(curPos, origJoint.BoundingSphereDiameter/2, 12, jointColor, 0f, 0f);
                }
                if (boundingBox)
                {
                    Vector3 extents = (origJoint.BoundingBox.Max - origJoint.BoundingBox.Min) / 2;

                    // Many bones have no extents, simply skip them to avoid adding them to the line renderer.
                    if (extents.LengthSquared == 0f)
                        continue;

                    lineDrawer.DrawBox(curPos, extents, curRot, jointColor, 0f, 0f);
                }
            }
        }

        public void DrawBoundsForShapes(bool boundingBox, bool boundingSphere, IDebugLineDrawer lineDrawer)
        {
            foreach (var shape in SHP1Tag.Shapes)
            {
                if (boundingSphere)
                {
                    lineDrawer.DrawSphere(shape.BoundingBox.Center, shape.BoundingSphereDiameter/2f, 12, WLinearColor.White, 0f, 0f);
                }
                if (boundingBox)
                {
                    lineDrawer.DrawBox(shape.BoundingBox.Min, shape.BoundingBox.Max, WLinearColor.Green, 0f, 0f);
                }
            }
        }

        public void DrawBones(IDebugLineDrawer lineDrawer)
        {
            Matrix4[] boneTransforms = new Matrix4[JNT1Tag.BindJoints.Count];
            Vector3 lastPos = Vector3.Zero;

            for (int i = 0; i < JNT1Tag.BindJoints.Count; i++)
            {
                SkeletonJoint curJoint, origJoint;
                curJoint = origJoint = JNT1Tag.BindJoints[i];

                Matrix4 cumulativeTransform = Matrix4.Identity;
                while (true)
                {
                    Matrix4 jointMatrix = Matrix4.CreateScale(curJoint.Scale) * Matrix4.CreateFromQuaternion(curJoint.Rotation) * Matrix4.CreateTranslation(curJoint.Translation);
                    cumulativeTransform *= jointMatrix;
                    if (curJoint.Parent == null)
                        break;

                    curJoint = curJoint.Parent;
                }

                boneTransforms[i] = cumulativeTransform;
                Vector3 curPos = cumulativeTransform.ExtractTranslation();
                Quaternion curRot = cumulativeTransform.ExtractRotation();

                WLinearColor jointColor = origJoint.Unknown1 == 0 ? WLinearColor.Yellow : WLinearColor.Blue;
                lineDrawer.DrawLine(lastPos, curPos, jointColor, 0f, 0f);
                lastPos = curPos;
            }
        }

        public bool Raycast(FRay ray, out float hitDistance, bool returnFirstHit = false)
        {
            // Raycast against the bounding box of the entire mesh first to see if we can save ourself a bunch of time.
            bool hitsAABB = WMath.RayIntersectsAABB(ray, BoundingBox.Min, BoundingBox.Max, out hitDistance);

            if (!hitsAABB)
                return false;

            // Okay, they've intersected with our big bounding box, so now we'll trace against individual mesh bounding box.
            // However, if they've applied skinning data to the meshes then these bounding boxes are no longer valid, so this
            // optimization step only counts if they're not applying any skinning.
            bool canSkipShapeTriangles = m_currentBoneAnimation == null;
            bool rayDidHit = false;
            foreach (var shape in SHP1Tag.Shapes)
            {
                if (canSkipShapeTriangles)
                {
                    hitsAABB = WMath.RayIntersectsAABB(ray, shape.BoundingBox.Min, shape.BoundingBox.Max, out hitDistance);

                    // If we didn't intersect with this shape, just go onto the next one.
                    if (!hitsAABB)
                        continue;
                }

                // We either intersected with this shape's AABB or they have skinning data applied (and thus we can't skip it),
                // thus, we're going to test against every (skinned!) triangle in this shape.
                bool hitTriangle = false;
                //var vertexList = shape.OverrideVertPos.Count > 0 ? shape.OverrideVertPos : shape.VertexData.Position;

                /*for (int i = 0; i < shape.Indexes.Count; i += 3)
                {
                    float triHitDist;
                    hitTriangle = WMath.RayIntersectsTriangle(ray, vertexList[shape.Indexes[i]], vertexList[shape.Indexes[i + 1]], vertexList[shape.Indexes[i + 2]], true, out triHitDist);

                    // If we hit this triangle and we're OK to just return the first hit on the model, then we can early out.
                    if (hitTriangle && returnFirstHit)
                    {
                        hitDistance = triHitDist;
                        return true;
                    }

                    // Otherwise, we need to test to see if this hit is closer than the previous hit.
                    if (hitTriangle)
                    {
                        if (triHitDist < hitDistance)
                            hitDistance = triHitDist;
                        rayDidHit = true;
                    }
                }*/
            }

            return rayDidHit;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region IDisposable Support
        ~J3D()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        protected virtual void Dispose(bool manualDispose)
        {
            if (!m_hasBeenDisposed)
            {
                if (manualDispose)
                {
                    // Dispose managed state (managed objects).
                    foreach (var texture in TEX1Tag.Textures)
                        texture.Dispose();

                    foreach (var shape in SHP1Tag.Shapes)
                    {
                        foreach (var pak in shape.Packets)
                            pak.Dispose();
                    }

                    foreach (var kvp in m_textureOverrides)
                        kvp.Value.Dispose();

                    foreach (var material in MAT3Tag.MaterialList)
                        if(material.Shader != null)
                            material.Shader.Dispose();

                    foreach (var externalMaterial in m_externalMaterials)
                        externalMaterial.Dispose();
                }

                GL.DeleteBuffer(m_hardwareLightBuffer);

                // Null out our large managed arrays since we keep a lot of data in them.
                INF1Tag = null;
                VTX1Tag = null;
                MAT3Tag = null;
                SHP1Tag = null;
                JNT1Tag = null;
                TEX1Tag = null;
                EVP1Tag = null;
                DRW1Tag = null;

                m_boneAnimations = null;
                m_materialAnimations = null;
                m_tevColorOverrides = null;
                m_textureOverrides = null;
                m_externalMaterials = null;

                m_hasBeenDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
