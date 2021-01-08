using Coocoo3D.Components;
using Coocoo3D.MMDSupport;
using Coocoo3D.Present;
using Coocoo3D.ResourceWarp;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class MMDRendererComponent
    {
        public MMDMesh mesh;
        public MMDMeshAppend meshAppend = new MMDMeshAppend();
        public int meshVertexCount;
        public int meshIndexCount;
        public byte[] meshPosData;//ref type

        public TwinBuffer meshParticleBuffer;
        public List<RuntimeMaterial> Materials = new List<RuntimeMaterial>();
        public List<RuntimeMaterial.InnerStruct> materialsBaseData = new List<RuntimeMaterial.InnerStruct>();
        public List<RuntimeMaterial.InnerStruct> computedMaterialsData = new List<RuntimeMaterial.InnerStruct>();
        public List<Texture2D> textures = new List<Texture2D>();

        public PObject POSkinning;
        public PObject PODraw;
        public PObject POParticleDraw;
        public ComputePO ParticleCompute;

        public Vector3[] meshPosData1;
        public Vector3[] meshPosData2;
        public bool meshNeedUpdateA;
        public bool meshNeedUpdateB;
        bool flip;
        bool prevflip;
        public float amountAB;

        public List<MorphVertexDesc[]> vertexMorphsA;
        public List<MorphVertexDesc[]> vertexMorphsB;


        public void SetPose(MMDMorphStateComponent morphStateComponent)
        {
            ComputeVertexMorph(morphStateComponent);
            ComputeMaterialMorph(morphStateComponent);
        }

        public void ComputeVertexMorph(MMDMorphStateComponent morphStateComponent)
        {
            prevflip = flip;
            flip = ((int)(morphStateComponent.currentTimeA / MMDMorphStateComponent.c_frameInterval) & 1) == 1;
            amountAB = flip ? (1 - morphStateComponent.amountAB) : morphStateComponent.amountAB;
            if (prevflip != flip) morphStateComponent.FlipAB();
            for (int i = 0; i < morphStateComponent.morphs.Count; i++)
            {
                if (morphStateComponent.morphs[i].Type == MorphType.Vertex)
                {
                    MorphVertexDesc[] morphVertexStructs2 = morphStateComponent.morphs[i].MorphVertexs;

                    MorphVertexDesc[] morphVertexA = vertexMorphsA[i];
                    MorphVertexDesc[] morphVertexB = vertexMorphsB[i];
                    if (!flip)
                    {
                        if (morphStateComponent.ComputedWeightNotEqualsPrevA(i, out float computedWeightA))
                        {
                            //for optimization
                            if (computedWeightA != 0)
                                for (int j = 0; j < morphVertexA.Length; j++)
                                    morphVertexA[j].Offset = morphVertexStructs2[j].Offset * computedWeightA;
                            meshNeedUpdateA = true;
                        }
                        if (morphStateComponent.ComputedWeightNotEqualsPrevB(i, out float computedWeightB))
                        {
                            if (computedWeightB != 0)
                                for (int j = 0; j < morphVertexB.Length; j++)
                                    morphVertexB[j].Offset = morphVertexStructs2[j].Offset * computedWeightB;
                            meshNeedUpdateB = true;
                        }
                    }
                    else
                    {
                        if (morphStateComponent.ComputedWeightNotEqualsPrevA(i, out float computedWeightA))
                        {
                            if (computedWeightA != 0)
                                for (int j = 0; j < morphVertexB.Length; j++)
                                    morphVertexB[j].Offset = morphVertexStructs2[j].Offset * computedWeightA;
                            meshNeedUpdateB = true;
                        }
                        if (morphStateComponent.ComputedWeightNotEqualsPrevB(i, out float computedWeightB))
                        {
                            if (computedWeightB != 0)
                                for (int j = 0; j < morphVertexA.Length; j++)
                                    morphVertexA[j].Offset = morphVertexStructs2[j].Offset * computedWeightB;
                            meshNeedUpdateA = true;
                        }
                    }
                }
            }
            if (meshNeedUpdateA)
            {
                MMDMesh.CopyPosData(meshPosData1, meshPosData);
                ComputeMorphVertex(meshPosData1, vertexMorphsA, flip ? morphStateComponent.WeightComputedB : morphStateComponent.WeightComputedA);
            }
            if (meshNeedUpdateB)
            {
                MMDMesh.CopyPosData(meshPosData2, meshPosData);
                ComputeMorphVertex(meshPosData2, vertexMorphsB, flip ? morphStateComponent.WeightComputedA : morphStateComponent.WeightComputedB);
            }
        }

        private static void ComputeMorphVertex(Vector3[] output, List<MorphVertexDesc[]> morphs, float[] weightTest)
        {
            for (int i = 0; i < morphs.Count; i++)
            {
                if (morphs[i] == null) continue;
                MorphVertexDesc[] morphVertexStructs = morphs[i];
                if (weightTest[i] != 0)
                    for (int j = 0; j < morphVertexStructs.Length; j++)
                    {
                        output[morphVertexStructs[j].VertexIndex] += morphVertexStructs[j].Offset;
                    }
            }
        }

        public void ComputeMaterialMorph(MMDMorphStateComponent morphStateComponent)
        {
            for (int i = 0; i < computedMaterialsData.Count; i++)
            {
                computedMaterialsData[i] = materialsBaseData[i];
            }
            for (int i = 0; i < morphStateComponent.morphs.Count; i++)
            {
                if (morphStateComponent.morphs[i].Type == MorphType.Material && morphStateComponent.WeightComputed[i] != morphStateComponent.WeightComputedPrev[i])
                {
                    MorphMaterialDesc[] morphMaterialStructs = morphStateComponent.morphs[i].MorphMaterials;
                    float computedWeight = morphStateComponent.WeightComputed[i];
                    for (int j = 0; j < morphMaterialStructs.Length; j++)
                    {
                        MorphMaterialDesc morphMaterialStruct = morphMaterialStructs[j];
                        int k = morphMaterialStruct.MaterialIndex;
                        RuntimeMaterial.InnerStruct struct1 = computedMaterialsData[k];
                        if (morphMaterialStruct.MorphMethon == MorphMaterialMethon.Add)
                        {
                            struct1.AmbientColor += morphMaterialStruct.Ambient * computedWeight;
                            struct1.DiffuseColor += morphMaterialStruct.Diffuse * computedWeight;
                            struct1.EdgeColor += morphMaterialStruct.EdgeColor * computedWeight;
                            struct1.EdgeSize += morphMaterialStruct.EdgeSize * computedWeight;
                            struct1.SpecularColor += morphMaterialStruct.Specular * computedWeight;
                            struct1.SubTexture += morphMaterialStruct.SubTexture * computedWeight;
                            struct1.Texture += morphMaterialStruct.Texture * computedWeight;
                            struct1.ToonTexture += morphMaterialStruct.ToonTexture * computedWeight;
                        }
                        else if (morphMaterialStruct.MorphMethon == MorphMaterialMethon.Mul)
                        {
                            struct1.AmbientColor = Vector3.Lerp(struct1.AmbientColor, struct1.AmbientColor * morphMaterialStruct.Ambient, computedWeight);
                            struct1.DiffuseColor = Vector4.Lerp(struct1.DiffuseColor, struct1.DiffuseColor * morphMaterialStruct.Diffuse, computedWeight);
                            struct1.EdgeColor = Vector4.Lerp(struct1.EdgeColor, struct1.EdgeColor * morphMaterialStruct.EdgeColor, computedWeight);
                            struct1.EdgeSize = struct1.EdgeSize * morphMaterialStruct.EdgeSize * computedWeight + struct1.EdgeSize * (1 - computedWeight);
                            struct1.SpecularColor = Vector4.Lerp(struct1.SpecularColor, struct1.SpecularColor * morphMaterialStruct.Specular, computedWeight);
                            struct1.SubTexture = Vector4.Lerp(struct1.SubTexture, struct1.SubTexture * morphMaterialStruct.SubTexture, computedWeight);
                            struct1.Texture = Vector4.Lerp(struct1.Texture, struct1.Texture * morphMaterialStruct.Texture, computedWeight);
                            struct1.ToonTexture = Vector4.Lerp(struct1.ToonTexture, struct1.ToonTexture * morphMaterialStruct.ToonTexture, computedWeight);
                        }

                        computedMaterialsData[k] = struct1;
                        Materials[k].innerStruct = struct1;
                    }
                }
            }
        }
    }
    public enum DrawFlag
    {
        DrawDoubleFace = 1,
        DrawGroundShadow = 2,
        CastSelfShadow = 4,
        DrawSelfShadow = 8,
        DrawEdge = 16,
    }
    public class RuntimeMaterial
    {
        public const int c_materialDataSize = 256;

        public Texture2D tex;
        public string Name;
        public string NameEN;
        public int indexCount;
        public int texIndex;
        public int toonIndex;
        public DrawFlag DrawFlags;

        public InnerStruct innerStruct;
        public struct InnerStruct
        {
            public Vector4 DiffuseColor;
            public Vector4 SpecularColor;
            public Vector3 AmbientColor;
            public float EdgeSize;
            public Vector4 EdgeColor;

            public Vector4 Texture;
            public Vector4 SubTexture;
            public Vector4 ToonTexture;
            public uint IsTransparent;
            public float Metallic;
            public float Roughness;
            public float Emission;
            public float Subsurface;
            public float Specular;
            public float SpecularTint;
            public float Anisotropic;
            public float Sheen;
            public float SheenTint;
            public float Clearcoat;
            public float ClearcoatGloss;
        }
        public override string ToString()
        {
            return string.Format("{0}_{1}", Name, NameEN);
        }
    }
}
namespace Coocoo3D.FileFormat
{
    public static partial class PMXFormatExtension
    {
        public static void Reload(this MMDRendererComponent rendererComponent, ModelPack modelPack)
        {
            rendererComponent.POSkinning = null;
            rendererComponent.PODraw = null;
            rendererComponent.ParticleCompute = null;
            rendererComponent.POParticleDraw = null;

            ReloadModel(rendererComponent, modelPack);
        }

        public static void ReloadModel(this MMDRendererComponent rendererComponent, ModelPack modelPack)
        {
            rendererComponent.Materials.Clear();
            rendererComponent.materialsBaseData.Clear();
            rendererComponent.computedMaterialsData.Clear();

            rendererComponent.mesh = modelPack.GetMesh();
            rendererComponent.meshPosData = modelPack.verticesDataPosPart;
            rendererComponent.meshVertexCount = rendererComponent.mesh.m_vertexCount;
            rendererComponent.meshIndexCount = rendererComponent.mesh.m_indexCount;
            //rendererComponent.meshParticleBuffer = new TwinBuffer();
            //rendererComponent.meshParticleBuffer.Reload(rendererComponent.mesh.m_vertexCount * 32);
            rendererComponent.meshPosData1 = new Vector3[rendererComponent.mesh.m_vertexCount];
            rendererComponent.meshPosData2 = new Vector3[rendererComponent.mesh.m_vertexCount];

            rendererComponent.meshAppend.Reload(rendererComponent.meshVertexCount);

            var modelResource = modelPack.pmx;
            for (int i = 0; i < modelResource.Materials.Count; i++)
            {
                var mmdMat = modelResource.Materials[i];

                RuntimeMaterial mat = new RuntimeMaterial
                {
                    Name = mmdMat.Name,
                    NameEN = mmdMat.NameEN,
                    texIndex = mmdMat.TextureIndex,
                    indexCount = mmdMat.TriangeIndexNum,
                    innerStruct =
                    {
                        DiffuseColor = mmdMat.DiffuseColor,
                        SpecularColor = mmdMat.SpecularColor,
                        EdgeSize = mmdMat.EdgeScale,
                        EdgeColor = mmdMat.EdgeColor,
                        AmbientColor = new Vector3(MathF.Pow(mmdMat.AmbientColor.X, 2.2f), MathF.Pow(mmdMat.AmbientColor.Y, 2.2f), MathF.Pow(mmdMat.AmbientColor.Z, 2.2f)),
                        Roughness = 0.5f,
                        Specular = 0.5f,
                    },
                    DrawFlags = (DrawFlag)mmdMat.DrawFlags,
                    toonIndex = mmdMat.ToonIndex,
                };
                rendererComponent.Materials.Add(mat);
                rendererComponent.materialsBaseData.Add(mat.innerStruct);
                rendererComponent.computedMaterialsData.Add(mat.innerStruct);
            }

            int morphCount = modelResource.Morphs.Count;
            rendererComponent.vertexMorphsA = new List<MorphVertexDesc[]>();
            rendererComponent.vertexMorphsB = new List<MorphVertexDesc[]>();
            for (int i = 0; i < morphCount; i++)
            {
                if (modelResource.Morphs[i].Type == PMX_MorphType.Vertex)
                {
                    MorphVertexDesc[] morphVertexStructs = new MorphVertexDesc[modelResource.Morphs[i].MorphVertexs.Length];
                    PMX_MorphVertexDesc[] sourceMorph = modelResource.Morphs[i].MorphVertexs;
                    for (int j = 0; j < morphVertexStructs.Length; j++)
                    {
                        morphVertexStructs[j].VertexIndex = sourceMorph[j].VertexIndex;
                    }
                    rendererComponent.vertexMorphsA.Add(morphVertexStructs);
                    rendererComponent.vertexMorphsB.Add(morphVertexStructs);
                }
                else
                {
                    rendererComponent.vertexMorphsA.Add(null);
                    rendererComponent.vertexMorphsB.Add(null);
                }
            }
            //Dictionary<int, int> reportFrequency = new Dictionary<int, int>(10000);
            //for (int i = 0; i < morphCount; i++)
            //{
            //    if (modelResource.Morphs[i].Type == PMX_MorphType.Vertex)
            //    {
            //        PMX_MorphVertexDesc[] sourceMorph = modelResource.Morphs[i].MorphVertexs;
            //        for (int j = 0; j < sourceMorph.Length; j++)
            //        {
            //            if (!reportFrequency.TryAdd(sourceMorph[j].VertexIndex, 1))
            //            {
            //                reportFrequency[sourceMorph[j].VertexIndex]++;
            //            }
            //        }
            //    }
            //}
            //int[] freqResult = new int[32];
            //foreach (int value1 in reportFrequency.Values)
            //{
            //    if (value1 < 32)
            //    {
            //        freqResult[value1]++;
            //    }
            //    else
            //    {

            //    }
            //}
        }
    }
}
