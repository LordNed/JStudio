using GameFormatReader.Common;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK;

namespace JStudio.J3D
{
    public class DRW1
    {
        public List<bool> IsPartiallyWeighted;
        public List<ushort> TransformIndexTable;

        public Matrix4[] Matrices;

        public void LoadDRW1FromStream(EndianBinaryReader reader, long tagStart)
        {
            ushort sectionCount = reader.ReadUInt16();
            Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding

            uint isWeightedOffset = reader.ReadUInt32();
            uint indexOffset = reader.ReadUInt32();

            IsPartiallyWeighted = new List<bool>();
            TransformIndexTable = new List<ushort>();

            reader.BaseStream.Position = tagStart + isWeightedOffset;
            for (int k = 0; k < sectionCount; k++)
                IsPartiallyWeighted.Add(reader.ReadBoolean());

            reader.BaseStream.Position = tagStart + indexOffset;
            for (int k = 0; k < sectionCount; k++)
                TransformIndexTable.Add(reader.ReadUInt16());

            Matrices = new Matrix4[sectionCount];
        }

        public void UpdateMatrices(IList<SkeletonJoint> bones, EVP1 envelopes)
        {
            Matrix4[] bone_mats = new Matrix4[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                bone_mats[i] = bones[i].TransformMatrix;
            }

            for (int i = 0; i < Matrices.Length; i++)
            {
                if (IsPartiallyWeighted[i])
                {
                    EVP1.Envelope env = envelopes.Envelopes[TransformIndexTable[i]];

                    Matrix4 result = Matrix4.Zero;
                    for (int j = 0; j < env.NumBones; j++)
                    {
                        Matrix4 sm1 = envelopes.InverseBindPose[env.BoneIndexes[j]];
                        Matrix4 sm2 = bone_mats[env.BoneIndexes[j]];

                        result += Matrix4.Mult(Matrix4.Mult(sm1, sm2), env.BoneWeights[j]);
                    }

                    Matrices[i] = result;
                }
                else
                {
                    Matrices[i] = bone_mats[TransformIndexTable[i]];
                }
            }
        }
    }
}
