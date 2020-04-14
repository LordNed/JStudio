﻿using GameFormatReader.Common;
using JStudio.OpenGL;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using WindEditor;
using System.Runtime.InteropServices;
namespace JStudio.J3D
{
    public class ShapeVertexAttribute
    {
        public VertexArrayType ArrayType;
        public VertexDataType DataType;

        public ShapeVertexAttribute(VertexArrayType arrayType, VertexDataType dataType)
        {
            ArrayType = arrayType;
            DataType = dataType;
        }

        public override string ToString()
        {
            return string.Format("ArrayType: {0} DataType: {1}", ArrayType, DataType);
        }
    }

    public class SHP1
    {
        public class SkinDataTable
        {
            public int Unknown0 { get; private set; }
            public int FirstRelevantVertexIndex;
            public int LastRelevantVertexIndex;
            public List<ushort> MatrixTable { get; private set; }

            public SkinDataTable(int unknown0)
            {
                Unknown0 = unknown0;
                MatrixTable = new List<ushort>();
            }
        }

        public class Shape
        {
            public byte MatrixType { get; set; }
            public float BoundingSphereDiameter { get; set; }
            public FAABox BoundingBox { get; set; }

            public List<Packet> Packets { get; set; }
            public List<ShapeVertexAttribute> Attributes { get; internal set; }

            public Shape()
            {
                Packets = new List<Packet>();
                Attributes = new List<ShapeVertexAttribute>();
            }
        }

        public class Packet : IDisposable
        {
            // To detect redundant calls
            private bool m_hasBeenDisposed = false;

            public MeshVertexHolder VertexData { get; internal set; }
            public List<int> Indexes { get; internal set; }
            public VertexDescription VertexDescription { get; private set; }

            // This is a list of all Matrix Table entries for all sub-primitives. 
            public SkinDataTable MatrixDataTable { get; set; }

            public int[] m_glBufferIndexes;
            public int m_glIndexBuffer;

            public Matrix4[] SkinningMatrices;

            public Packet()
            {
                VertexData = new MeshVertexHolder();
                Indexes = new List<int>();
                VertexDescription = new VertexDescription();
                MatrixDataTable = new SkinDataTable(0);

                m_glBufferIndexes = new int[15];
                for (int i = 0; i < m_glBufferIndexes.Length; i++)
                    m_glBufferIndexes[i] = -1;

                SkinningMatrices = new Matrix4[10];
                m_glIndexBuffer = GL.GenBuffer();
            }

            #region Attribute Uploading
            public void UploadBuffersToGPU()
            {
                // Upload the Indexes
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, m_glIndexBuffer);
                GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * Indexes.Count), Indexes.ToArray(), BufferUsageHint.StaticDraw);

                if (VertexData.Position.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Position, VertexData.Position.ToArray());
                if (VertexData.Normal.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Normal, VertexData.Normal.ToArray());
                if (VertexData.Binormal.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Binormal, VertexData.Binormal.ToArray());
                if (VertexData.Color0.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Color0, VertexData.Color0.ToArray());
                if (VertexData.Color1.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Color1, VertexData.Color1.ToArray());
                if (VertexData.Tex0.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex0, VertexData.Tex0.ToArray());
                if (VertexData.Tex1.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex1, VertexData.Tex1.ToArray());
                if (VertexData.Tex2.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex2, VertexData.Tex2.ToArray());
                if (VertexData.Tex3.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex3, VertexData.Tex3.ToArray());
                if (VertexData.Tex4.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex4, VertexData.Tex4.ToArray());
                if (VertexData.Tex5.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex5, VertexData.Tex5.ToArray());
                if (VertexData.Tex6.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex6, VertexData.Tex6.ToArray());
                if (VertexData.Tex7.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.Tex7, VertexData.Tex7.ToArray());
                if (VertexData.PositionMatrixIndexes.Count > 0) UpdateAttributeAndBuffers(ShaderAttributeIds.PosMtxIndex, VertexData.PositionMatrixIndexes.ToArray());
            }

            private void UpdateAttributeAndBuffers<T>(ShaderAttributeIds attribute, T[] data) where T : struct
            {
                // See if this attribute is already enabled. If it's not already enabled, we need to generate a buffer for it.
                if (!VertexDescription.AttributeIsEnabled(attribute))
                {
                    m_glBufferIndexes[(int)attribute] = GL.GenBuffer();
                    VertexDescription.EnableAttribute(attribute);
                }

                // Bind the buffer before updating the data.
                GL.BindBuffer(BufferTarget.ArrayBuffer, m_glBufferIndexes[(int)attribute]);

                // Finally, update the data.
                int stride = VertexDescription.GetStride(attribute);
                GL.BufferData<T>(BufferTarget.ArrayBuffer, (IntPtr)(data.Length * stride), data, BufferUsageHint.StaticDraw);
            }
            #endregion

            #region Rendering
            public void Bind(Shader bound_shader)
            {
                for (int i = 0; i < m_glBufferIndexes.Length; i++)
                {
                    ShaderAttributeIds id = (ShaderAttributeIds)i;
                    if (VertexDescription.AttributeIsEnabled(id))
                    {
                        GL.BindBuffer(BufferTarget.ArrayBuffer, m_glBufferIndexes[i]);
                        GL.EnableVertexAttribArray(i);

                        if (id == ShaderAttributeIds.PosMtxIndex)
                        {
                            GL.VertexAttribIPointer(i, VertexDescription.GetAttributeSize(id), VertexAttribIntegerType.Int, VertexDescription.GetStride(id), (IntPtr)0);
                        }
                        else
                        {
                            GL.VertexAttribPointer(i, VertexDescription.GetAttributeSize(id), VertexDescription.GetAttributePointerType(id), false, VertexDescription.GetStride(id), 0);
                        }
                    }
                }

                for (int i = 0; i < 10; i++)
                {
                    int loc = GL.GetUniformLocation(bound_shader.Program, $"SkinningMtxs[{ i }]");
                    GL.UniformMatrix4(loc, false, ref SkinningMatrices[i]);
                }

                // Bind the Element Array Buffer as well
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, m_glIndexBuffer);
            }

            public void Unbind()
            {
                for (int i = 0; i < m_glBufferIndexes.Length; i++)
                    GL.DisableVertexAttribArray(i);
            }

            public void Draw()
            {
                GL.DrawElements(BeginMode.Triangles, Indexes.Count, DrawElementsType.UnsignedInt, 0);
            }
            #endregion

            #region IDisposable Support
            ~Packet()
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
                        // TODO: dispose managed state (managed objects).
                    }

                    GL.DeleteBuffer(m_glIndexBuffer);
                    for (int i = 0; i < m_glBufferIndexes.Length; i++)
                        if (m_glBufferIndexes[i] >= 0)
                            GL.DeleteBuffer(m_glBufferIndexes[i]);

                    // Set large fields to null.
                    VertexData = null;
                    Indexes = null;
                    VertexDescription = null;
                    MatrixDataTable = null;

                    m_hasBeenDisposed = true;
                }
            }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            #endregion
        }

        public short ShapeCount { get; private set; }
        public List<Shape> Shapes { get; private set; }
        public List<short> ShapeRemapTable;

        public SHP1()
        {
            Shapes = new List<Shape>();
        }

        public void ReadSHP1FromStream(EndianBinaryReader reader, long tagStart, MeshVertexHolder compressedVertexData)
        {
            ShapeCount = reader.ReadInt16();
            Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding
            int shapeOffset = reader.ReadInt32();

            // Another index remap table.
            int remapTableOffset = reader.ReadInt32();

            Trace.Assert(reader.ReadInt32() == 0);
            int attributeOffset = reader.ReadInt32();
            
            // Offset to the Matrix Table which holds a list of ushorts used for ??
            int matrixTableOffset = reader.ReadInt32();

            // Offset to the array of primitive's data.
            int primitiveDataOffset = reader.ReadInt32();
            int matrixDataOffset = reader.ReadInt32();
            int packetLocationOffset = reader.ReadInt32();

            reader.BaseStream.Position = tagStart + remapTableOffset;
            ShapeRemapTable = new List<short>();
            for (int i = 0; i < ShapeCount; i++)
                ShapeRemapTable.Add(reader.ReadInt16());

            for (int s = 0; s < ShapeCount; s++)
            {
                // Shapes can have different attributes for each shape. (ie: Some have only Position, while others have Pos & TexCoord, etc.) Each 
                // shape (which has a consistent number of attributes) it is split into individual packets, which are a collection of geometric primitives.
                // Each packet can have individual unique skinning data.

                reader.BaseStream.Position = tagStart + shapeOffset + (0x28 * s) /* 0x28 is the size of one Shape entry*/;
                long shapeStart = reader.BaseStream.Position;
                Shape shape = new Shape();
                shape.MatrixType = reader.ReadByte();
                Trace.Assert(reader.ReadByte() == 0xFF); // Padding

                // Number of Packets (of data) contained in this Shape
                ushort packetCount = reader.ReadUInt16();

                // Offset from the start of the Attribute List to the attributes this particular batch uses.
                ushort batchAttributeOffset = reader.ReadUInt16();

                ushort firstMatrixIndex = reader.ReadUInt16();
                ushort firstPacketIndex = reader.ReadUInt16();
                Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding

                float boundingSphereDiameter = reader.ReadSingle();
                Vector3 bboxMin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 bboxMax = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                // Determine which Attributes this particular shape uses.
                reader.BaseStream.Position = tagStart + attributeOffset + batchAttributeOffset;
                List<ShapeVertexAttribute> attributes = new List<ShapeVertexAttribute>();
                do
                {
                    ShapeVertexAttribute attribute = new ShapeVertexAttribute((VertexArrayType)reader.ReadInt32(), (VertexDataType)reader.ReadInt32());
                    if (attribute.ArrayType == VertexArrayType.NullAttr) 
                        break;

                    attributes.Add(attribute);
                } while (true);

                shape.BoundingSphereDiameter = boundingSphereDiameter;
                shape.BoundingBox = new FAABox(bboxMin, bboxMax);
                shape.Attributes = attributes;
                Shapes.Add(shape);

                for (ushort p = 0; p < packetCount; p++)
                {
                    int numVertexRead = 0;
                    Packet pak = new Packet();

                    // The packets are all stored linearly and then they point to the specific size and offset of the data for this particular packet.
                    reader.BaseStream.Position = tagStart + packetLocationOffset + ((firstPacketIndex + p) * 0x8); /* 0x8 is the size of one Packet entry */

                    int packetSize = reader.ReadInt32();
                    int packetOffset = reader.ReadInt32();

                    // Read Matrix Data for Packet
                    reader.BaseStream.Position = tagStart + matrixDataOffset + (firstMatrixIndex + p) * 0x08; /* 0x8 is the size of one Matrix Data */
                    ushort matrixUnknown0 = reader.ReadUInt16();
                    ushort matrixCount = reader.ReadUInt16();
                    uint matrixFirstIndex = reader.ReadUInt32();

                    SkinDataTable matrixData = new SkinDataTable(matrixUnknown0);
                    pak.MatrixDataTable = matrixData;
                    matrixData.FirstRelevantVertexIndex = pak.VertexData.Position.Count;

                    // Read Matrix Table data. The Matrix Table is skinning information for the packet which indexes into the DRW1 section for more info.
                    reader.BaseStream.Position = tagStart + matrixTableOffset + (matrixFirstIndex * 0x2); /* 0x2 is the size of one Matrix Table entry */
                    for (int m = 0; m < matrixCount; m++)
                        matrixData.MatrixTable.Add(reader.ReadUInt16());

                    // Read the Primitive Data
                    reader.BaseStream.Position = tagStart + primitiveDataOffset + packetOffset;

                    uint numPrimitiveBytesRead = 0;
                    while(numPrimitiveBytesRead < packetSize)
                    {
                        // The game pads the chunk out with zeros, so if there's a primitive with type zero (invalid) then we early out of the loop.
                        GXPrimitiveType type = (GXPrimitiveType)reader.ReadByte();
                        if (type == 0 || numPrimitiveBytesRead >= packetSize)
                            break;

                        // The number of vertices this primitive has indexes for
                        ushort vertexCount = reader.ReadUInt16();
                        numPrimitiveBytesRead += 0x3; // 2 bytes for vertex count, one byte for GXPrimitiveType.

                        List<MeshVertexIndex> primitiveVertices = new List<MeshVertexIndex>();

                        for(int v = 0; v < vertexCount; v++)
                        {
                            MeshVertexIndex newVert = new MeshVertexIndex();
                            primitiveVertices.Add(newVert);

                            // Each vertex has an index for each ShapeAttribute specified by the Shape that we belong to. So we'll loop through
                            // each index and load it appropriately (as vertices can have different data sizes).
                            foreach (ShapeVertexAttribute curAttribute in attributes)
                            {
                                int index = 0;
                                uint numBytesRead = 0;

                                switch (curAttribute.DataType)
                                {
                                    case VertexDataType.Unsigned8:
                                    case VertexDataType.Signed8:
                                        index = reader.ReadByte();
                                        numBytesRead = 1;
                                        break;
                                    case VertexDataType.Unsigned16:
                                    case VertexDataType.Signed16:
                                        index = reader.ReadUInt16();
                                        numBytesRead = 2;
                                        break;
                                    case VertexDataType.Float32:
                                    case VertexDataType.None:
                                    default:
                                        System.Console.WriteLine("Unknown Data Type {0} for ShapeAttribute!", curAttribute.DataType);
                                        break;
                                }

                                // We now have the index into the datatype this array points to. We can now inspect the array type of the 
                                // attribute to get the value out of the correct source array.
                                switch (curAttribute.ArrayType)
                                {
                                    case VertexArrayType.Position: newVert.Position = index; break;
                                    case VertexArrayType.PositionMatrixIndex: newVert.PosMtxIndex = index; break;
                                    case VertexArrayType.Normal: newVert.Normal = index; break;
                                    case VertexArrayType.Color0: newVert.Color0 = index; break;
                                    case VertexArrayType.Color1: newVert.Color1 = index; break;
                                    case VertexArrayType.Tex0:  newVert.Tex0 = index; break;
                                    case VertexArrayType.Tex1:  newVert.Tex1 = index; break;
                                    case VertexArrayType.Tex2:  newVert.Tex2 = index; break;
                                    case VertexArrayType.Tex3:  newVert.Tex3 = index; break;
                                    case VertexArrayType.Tex4:  newVert.Tex4 = index; break;
                                    case VertexArrayType.Tex5:  newVert.Tex5 = index; break;
                                    case VertexArrayType.Tex6:  newVert.Tex6 = index; break;
                                    case VertexArrayType.Tex7:  newVert.Tex7 = index; break;
                                    default:
                                        System.Console.WriteLine("Unsupported ArrayType {0} for ShapeAttribute!", curAttribute.ArrayType);
                                        break;
                                }

                                numPrimitiveBytesRead += numBytesRead;
                            }
                        }

                        // All vertices have now been loaded into the primitiveIndexes array. We can now convert them if needed
                        // to triangle lists, instead of triangle fans, strips, etc.
                        var triangleList = ConvertTopologyToTriangles(type, primitiveVertices);
                        for(int i = 0; i < triangleList.Count; i++)
                        {
                            pak.Indexes.Add(numVertexRead);
                            numVertexRead++;

                            var tri = triangleList[i];
                            if (tri.Position >= 0) pak.VertexData.Position.Add(compressedVertexData.Position[tri.Position]);
                            if (tri.Normal >= 0) pak.VertexData.Normal.Add(compressedVertexData.Normal[tri.Normal]);
                            if (tri.Binormal >= 0) pak.VertexData.Binormal.Add(compressedVertexData.Binormal[tri.Binormal]);
                            if (tri.Color0 >= 0) pak.VertexData.Color0.Add(compressedVertexData.Color0[tri.Color0]);
                            if (tri.Color1 >= 0) pak.VertexData.Color1.Add(compressedVertexData.Color1[tri.Color1]);
                            if (tri.Tex0 >= 0) pak.VertexData.Tex0.Add(compressedVertexData.Tex0[tri.Tex0]);
                            if (tri.Tex1 >= 0) pak.VertexData.Tex1.Add(compressedVertexData.Tex1[tri.Tex1]);
                            if (tri.Tex2 >= 0) pak.VertexData.Tex2.Add(compressedVertexData.Tex2[tri.Tex2]);
                            if (tri.Tex3 >= 0) pak.VertexData.Tex3.Add(compressedVertexData.Tex3[tri.Tex3]);
                            if (tri.Tex4 >= 0) pak.VertexData.Tex4.Add(compressedVertexData.Tex4[tri.Tex4]);
                            if (tri.Tex5 >= 0) pak.VertexData.Tex5.Add(compressedVertexData.Tex5[tri.Tex5]);
                            if (tri.Tex6 >= 0) pak.VertexData.Tex6.Add(compressedVertexData.Tex6[tri.Tex6]);
                            if (tri.Tex7 >= 0) pak.VertexData.Tex7.Add(compressedVertexData.Tex7[tri.Tex7]);

                            // We pre-divide the index here just to make life simpler. For some reason the index is multiplied by three
                            // and it's not entirely clear why. Might be related to doing skinning on the GPU and offsetting the correct
                            // number of floats in a matrix?
                            if (tri.PosMtxIndex >= 0) pak.VertexData.PositionMatrixIndexes.Add(tri.PosMtxIndex/3);
                            else pak.VertexData.PositionMatrixIndexes.Add(0);
                        }
                    }

                    // Set the last relevant vertex for this packet.
                    matrixData.LastRelevantVertexIndex = pak.VertexData.Position.Count;
                    pak.UploadBuffersToGPU();
                    shape.Packets.Add(pak);
                }
            }

            FixPacketSkinningIndices();

            RecalculateBoundingBoxes();
        }

        /// <summary>
        /// Replaces skinning indices of 65535 (ushort.max) with the proper indices from previous packets.
        /// </summary>
        private void FixPacketSkinningIndices()
        {
            foreach (Shape s in Shapes)
            {
                for (int i = 0; i < s.Packets.Count; i++)
                {
                    SHP1.Packet cur_packet = s.Packets[i];

                    for (int j = 0; j < cur_packet.MatrixDataTable.MatrixTable.Count; j++)
                    {
                        ushort cur_index = cur_packet.MatrixDataTable.MatrixTable[j];

                        if (cur_index == ushort.MaxValue)
                        {
                            for (int k = i - 1; k > -1; k--)
                            {
                                ushort last_index = s.Packets[k].MatrixDataTable.MatrixTable[j];

                                if (last_index != ushort.MaxValue)
                                {
                                    cur_packet.MatrixDataTable.MatrixTable[j] = last_index;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recalculates the bounding box for any shapes that have all zero-extent bounding boxes read from the file.
        /// This is so raycasting against these shapes works.
        /// </summary>
        private void RecalculateBoundingBoxes()
        {
            foreach (var shape in Shapes)
            {
                if (shape.BoundingBox.Extents != new Vector3(0.0f, 0.0f, 0.0f))
                    continue;

                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var packet in shape.Packets)
                {
                    foreach (var pos in packet.VertexData.Position)
                    {
                        if (pos.X < min.X)
                            min.X = pos.X;
                        if (pos.X > max.X)
                            max.X = pos.X;

                        if (pos.Y < min.Y)
                            min.Y = pos.Y;
                        if (pos.Y > max.Y)
                            max.Y = pos.Y;

                        if (pos.Z < min.Z)
                            min.Z = pos.Z;
                        if (pos.Z > max.Z)
                            max.Z = pos.Z;
                    }
                }

                shape.BoundingBox = new FAABox(min, max);
            }
        }

        public List<MeshVertexIndex> ConvertTopologyToTriangles(GXPrimitiveType fromType, List<MeshVertexIndex> indexes)
        {
            List<MeshVertexIndex> sortedIndexes = new List<MeshVertexIndex>();
            if(fromType == GXPrimitiveType.TriangleStrip)
            {
                for (int v = 2; v < indexes.Count; v++)
                {
                    bool isOdd = v % 2 != 0;
                    MeshVertexIndex[] newTri = new MeshVertexIndex[3];

                    newTri[0] = indexes[v - 2];
                    newTri[1] = isOdd ? indexes[v] : indexes[v - 1];
                    newTri[2] = isOdd ? indexes[v - 1] : indexes[v];

                    // Check against degenerate triangles (a triangle which shares indexes)
                    if (newTri[0] != newTri[1] && newTri[1] != newTri[2] && newTri[2] != newTri[0])
                        sortedIndexes.AddRange(newTri);
                    else
                        System.Console.WriteLine("Degenerate triangle detected, skipping TriangleStrip conversion to triangle.");
                }
            }
            else if(fromType == GXPrimitiveType.TriangleFan)
            {
                for(int v = 1; v < indexes.Count-1; v++)
                {
                    // Triangle is always, v, v+1, and index[0]?
                    MeshVertexIndex[] newTri = new MeshVertexIndex[3];
                    newTri[0] = indexes[v];
                    newTri[1] = indexes[v + 1];
                    newTri[2] = indexes[0];

                    // Check against degenerate triangles (a triangle which shares indexes)
                    if (newTri[0] != newTri[1] && newTri[1] != newTri[2] && newTri[2] != newTri[0])
                        sortedIndexes.AddRange(newTri);
                    else
                        System.Console.WriteLine("Degenerate triangle detected, skipping TriangleFan conversion to triangle.");
                }
            }
            else if(fromType == GXPrimitiveType.Triangles)
            {
                // The good news is, Triangles just go straight though!
                sortedIndexes.AddRange(indexes);
            }
            else
            {
                System.Console.WriteLine("Unsupported GXPrimitiveType: {0} in conversion to Triangle List.", fromType);
            }

            return sortedIndexes;
        }
    }
}
