﻿using GameFormatReader.Common;
using JStudio.OpenGL;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using WindEditor;

namespace JStudio.J3D
{
    public class Material : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get { return m_name; } set { m_name = value; OnPropertyChanged(); } }
        public Shader Shader { get; internal set; }
        public VertexDescription VtxDesc { get; internal set; }

        public byte Flag { get { return m_flag; } set { m_flag = value; OnPropertyChanged(); } }
        public bool IsTranslucent { get { return (Flag & 3) == 0; } }

        public GXCullMode CullMode { get { return m_cullMode; } set { m_cullMode = value; OnPropertyChanged(); } }
        public byte NumChannelControls { get { return m_numChannelControls; } set { m_numChannelControls = value; OnPropertyChanged(); } }
        public byte NumTexGensIndex { get; internal set; }
        public byte NumTevStages { get; internal set; }
        public bool ZCompLocIndex { get; internal set; }
        public ZMode ZModeIndex { get; internal set; }
        public bool DitherIndex { get; internal set; }
        public WLinearColor[] MaterialColorIndexes { get; internal set; }
        public ColorChannelControl[] ColorChannelControls { get; internal set; }
        public WLinearColor[] AmbientColorIndexes { get; internal set; }
        public WLinearColor[] LightingColorIndexes { get; internal set; }
        public TexCoordGen[] TexGenInfoIndexes { get; internal set; }
        public TexCoordGen[] PostTexGenInfoIndexes { get; internal set; }
        public TexMatrix[] TexMatrixIndexes { get; internal set; }
        public TexMatrix[] PostTexMatrixIndexes { get; internal set; }
        public short[] TextureIndexes { get; internal set; }
        public WLinearColor[] TevKonstColorIndexes { get; internal set; }
        public GXKonstColorSel[] TEVKonstColorSelectors { get; internal set; }
        public GXKonstAlphaSel[] TEVKonstAlphaSelectors { get; internal set; }
        public TevOrder[] TevOrderInfoIndexes { get; internal set; }
        public WLinearColor[] TevColorIndexes { get; internal set; }
        public TevStage[] TevStageInfoIndexes { get; internal set; }
        public TevSwapMode[] TevSwapModeIndexes { get; internal set; }
        public TevSwapModeTable[] TevSwapModeTableIndexes { get; internal set; }
        public short[] UnknownIndexes { get; internal set; }
        public FogInfo FogModeIndex { get; internal set; }
        public AlphaTest AlphaTest { get; internal set; }
        public BlendMode BlendModeIndex { get; internal set; }
        public NBTScale UnknownIndex2 { get; internal set; } // Tentatively named NBTScale


        private string m_name;
        private byte m_flag;
        private GXCullMode m_cullMode;
        private byte m_numChannelControls;

        public void Bind()
        {
            Shader.Bind();
        }

        public override string ToString()
        {
            return Name;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MAT3
    {
        public BindingList<Material> MaterialList { get; protected set; }
        public List<short> MaterialRemapTable { get; protected set; }
        public StringTable MaterialNameTable { get; protected set; }
        public List<short> TextureRemapTable { get; protected set; }

        /// <summary> Delegate defines a function that decodes one instance of type T.</summary>
        /// <param name="stream">The stream to decode the instance from</param>
        private delegate T LoadTypeFromStream<T>(EndianBinaryReader stream);

        internal void LoadMAT3FromStream(EndianBinaryReader reader, long chunkStart)
        {
            short materialCount = reader.ReadInt16();
            Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding

            MaterialList = new BindingList<Material>();

            // Nintendo sets unused offsets to zero, so we can't just use the next variable name in the list. Instead we have to search
            // until we find a non-zero one and calculate the difference that way. Thus, we convert all of the offsets into an int[] for
            // array operations.
            int[] offsets = new int[30];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = reader.ReadInt32();

            /* MATERIAL REMAP TABLE (See start of Material loader below) */
            MaterialRemapTable = new List<short>();

            for (int i = 0; i < materialCount; i++)
            {
                var val = ReadEntry(reader, ReadShort, chunkStart, offsets, 1, i, 2);
                MaterialRemapTable.Add(val);
            }

            /* STRING TABLE */
            reader.BaseStream.Position = chunkStart + offsets[2];
            MaterialNameTable = StringTable.FromStream(reader);

            // Unknowns:
            // Indirect Textures don't have an index from the Material
            // NBTSCale doesn't have a known index from the Material.

            // The Texture Remap table is a global table that all materials can reference, which allows the (up to) 8
            // textures in a specific material remap to any other texture they want via this remap table. There's no
            // pre-made count of these, so we subtract the two offsets and divide by 0x2 to get the approximate number
            // of remaps. This may pick up an extra junk remap at the end due to padding in some files.
            int numRemapEntries = (offsets[16] - offsets[15]) / 2;
            TextureRemapTable = new List<short>();
            for (int i = 0; i < numRemapEntries; i++)
                TextureRemapTable.Add(ReadEntry(reader, ReadShort, chunkStart, offsets, 15, i, 2));

            for (int m = 0; m < materialCount; m++)
            {
                // A Material entry is 0x14c long.
                reader.BaseStream.Position = chunkStart + offsets[0] + (MaterialRemapTable[m] * 0x14c);

                // The first byte of a material is some form of flag. Values found so far are 1, 4. 1 is the most common.
                // bmdview2 documentation says that means "draw on way down" while 4 means "draw on way up" (of INF1 heirarchy)
                // Realistically, this just seems to imply some sort of draw order.
                byte flag = reader.ReadByte();
				Trace.Assert(flag == 1 || flag == 4);

                // Now that we've read the contents of the material section, we can load their values into a material 
                // class which keeps it nice and tidy and full of class references and not indexes.
                Material material = new Material();
                MaterialList.Add(material);

                material.Name = MaterialNameTable[m];
                material.Flag = flag;
                material.CullMode = ReadEntry(reader, ReadCullMode, chunkStart, offsets, 4, reader.ReadByte(), 4);
                material.NumChannelControls = ReadEntry(reader, ReadByte, chunkStart, offsets, 6, reader.ReadByte(), 1);
                material.NumTexGensIndex = ReadEntry(reader, ReadByte, chunkStart, offsets, 10, reader.ReadByte(), 1);
                material.NumTevStages = ReadEntry(reader, ReadByte, chunkStart, offsets, 19, reader.ReadByte(), 1);
                material.ZCompLocIndex = ReadEntry(reader, ReadBool, chunkStart, offsets, 27, reader.ReadByte(), 1);
                material.ZModeIndex = ReadEntry(reader, ReadZMode, chunkStart, offsets, 26, reader.ReadByte(), 4);
                material.DitherIndex = ReadEntry(reader, ReadBool, chunkStart, offsets, 28, reader.ReadByte(), 1);

                material.MaterialColorIndexes = new WLinearColor[2];
                for (int i = 0; i < material.MaterialColorIndexes.Length; i++)
                    material.MaterialColorIndexes[i] = ReadEntry(reader, ReadColor32, chunkStart, offsets, 5, reader.ReadInt16(), 4);


                material.ColorChannelControls = new ColorChannelControl[4];
                for (int i = 0; i < material.ColorChannelControls.Length; i++)
                {
                    var val = reader.ReadInt16();
                    if(val >= 0)
                        material.ColorChannelControls[i] = ReadEntry(reader, ReadChannelControl, chunkStart, offsets, 7, val, 8);
                }

                material.AmbientColorIndexes = new WLinearColor[2];
                for (int i = 0; i < material.AmbientColorIndexes.Length; i++)
                    material.AmbientColorIndexes[i] = ReadEntry(reader, ReadColor32, chunkStart, offsets, 8, reader.ReadInt16(), 4);

                var lightColorList = new List<WLinearColor>();
                for (int i = 0; i < 8; i++)
                {
                    var val = reader.ReadInt16();
                    if (val >= 0)
                        lightColorList.Add(ReadEntry(reader, ReadColorShort, chunkStart, offsets, 9, val, 8));
                }
                material.LightingColorIndexes = lightColorList.ToArray();

                var texGenInfoList = new List<TexCoordGen>();
                for (int i = 0; i < 8; i++)
                {
                    var val = reader.ReadInt16();
                    if (val >= 0)
                        texGenInfoList.Add(ReadEntry(reader, ReadTexCoordGen, chunkStart, offsets, 11, val, 4));
                }
                material.TexGenInfoIndexes = texGenInfoList.ToArray();

                var postTexGenInfoList = new List<TexCoordGen>();
                for (int i = 0; i < 8; i++)
                {
                    var val = reader.ReadInt16();
					if (val >= 0)
						postTexGenInfoList.Add(ReadEntry(reader, ReadTexCoordGen, chunkStart, offsets, 12, val, 4));
					else
						postTexGenInfoList.Add(new TexCoordGen());
                }
                material.PostTexGenInfoIndexes = postTexGenInfoList.ToArray();

                var texMatrixList = new List<TexMatrix>();
                for (int i = 0; i < 10; i++)
                {
                    var val = reader.ReadInt16();
					if (val >= 0)
						texMatrixList.Add(ReadEntry(reader, ReadTexMatrix, chunkStart, offsets, 13, val, 100));
					else
						texMatrixList.Add(new TexMatrix());
                }
                material.TexMatrixIndexes = texMatrixList.ToArray();

                var postTexMatrixList = new List<TexMatrix>();
                for (int i = 0; i < 20; i++)
                {
                    var val = reader.ReadInt16();
					if (val >= 0)
					{
						var entry = ReadEntry(reader, ReadTexMatrix, chunkStart, offsets, 14, val, 100);

						// Models seem to specify that they have post tex matrices, but then don't have an offset with any post tex matrices.
						// this prevents us from inserting a null entry that then tries to get used.
						if (entry != null)
							postTexMatrixList.Add(entry);
					}
                }
                material.PostTexMatrixIndexes = postTexMatrixList.ToArray();

                material.TextureIndexes = new short[8];
                for (int i = 0; i < material.TextureIndexes.Length; i++)
                    material.TextureIndexes[i] = reader.ReadInt16();

                material.TevKonstColorIndexes = new WLinearColor[4];
                for (int i = 0; i < material.TevKonstColorIndexes.Length; i++)
                    material.TevKonstColorIndexes[i] = ReadEntry(reader, ReadColor32, chunkStart, offsets, 18, reader.ReadInt16(), 4);

                // Guessing that this one doesn't index anything else as it's just an enum value and there doesn't seem to be an offset for it in the header.
                material.TEVKonstColorSelectors = new GXKonstColorSel[16];
                for (int i = 0; i < material.TEVKonstColorSelectors.Length; i++)
                    material.TEVKonstColorSelectors[i] = (GXKonstColorSel)reader.ReadByte();

                // Guessing that this one doesn't index anything else as it's just an enum value and there doesn't seem to be an offset for it in the header.
                material.TEVKonstAlphaSelectors = new GXKonstAlphaSel[16];
                for (int i = 0; i < material.TEVKonstAlphaSelectors.Length; i++)
                    material.TEVKonstAlphaSelectors[i] = (GXKonstAlphaSel)reader.ReadByte();

                var tevOrderInfoList = new List<TevOrder>();
                for (int i = 0; i < 16; i++)
                {
                    var val = reader.ReadInt16();
                    if (val >= 0)
                        tevOrderInfoList.Add(ReadEntry(reader, ReadTevOrder, chunkStart, offsets, 16, val, 4));
                }
                material.TevOrderInfoIndexes = tevOrderInfoList.ToArray();

                material.TevColorIndexes = new WLinearColor[4];
                for (int i = 0; i < material.TevColorIndexes.Length; i++)
                {
                    var val = reader.ReadInt16();
                    material.TevColorIndexes[i] = ReadEntry(reader, ReadColorShort, chunkStart, offsets, 17, val, 8);
                }

                var tevStageInfoList = new List<TevStage>();
                for (int i = 0; i < 16; i++)
                {
                    var val = reader.ReadInt16();
                    if (val >= 0)
                        tevStageInfoList.Add(ReadEntry(reader, ReadTevCombinerStage, chunkStart, offsets, 20, val, 20));
                }
                material.TevStageInfoIndexes = tevStageInfoList.ToArray();

                var tevSwapModeList = new List<TevSwapMode>();
                for (int i = 0; i < 16; i++)
                {
                    var val = reader.ReadInt16();
                    if (val >= 0)
                        tevSwapModeList.Add(ReadEntry(reader, ReadTevSwapMode, chunkStart, offsets, 21, val, 4));
                }
                material.TevSwapModeIndexes = tevSwapModeList.ToArray();

                material.TevSwapModeTableIndexes = new TevSwapModeTable[4];
                for (int i = 0; i < material.TevSwapModeTableIndexes.Length; i++)
                    material.TevSwapModeTableIndexes[i] = ReadEntry(reader, ReadTevSwapModeTable, chunkStart, offsets, 22, reader.ReadInt16(), 4);

                // We're still not sure what these are, but they're not TevSwapModeTables, because those are 4 bytes each.
                material.UnknownIndexes = new short[12];
                for (int l = 0; l < material.UnknownIndexes.Length; l++)
                material.UnknownIndexes[l] = reader.ReadInt16();

                material.FogModeIndex = ReadEntry(reader, ReadFogInfo, chunkStart, offsets, 23, reader.ReadInt16(), 44);
                material.AlphaTest = ReadEntry(reader, ReadAlphaCompare, chunkStart, offsets, 24, reader.ReadInt16(), 8);
                material.BlendModeIndex = ReadEntry(reader, ReadBlendMode, chunkStart, offsets, 25, reader.ReadInt16(), 4);
                material.UnknownIndex2 = ReadEntry(reader, ReadNBTScale, chunkStart, offsets, 29, reader.ReadInt16(), 16);
            }
        }

        private static List<T> Collect<T>(EndianBinaryReader stream, LoadTypeFromStream<T> function, int count)
        {
            List<T> values = new List<T>();
            for (int i = 0; i < count; i++)
            {
                values.Add(function(stream));
            }

            return values;
        }

        private static List<T> ReadSection<T>(EndianBinaryReader stream, long chunkStart, int chunkSize, int[] offsets, int offset, LoadTypeFromStream<T> function, int itemSize)
        {
            // If there's none of this value, early out and return an empty list, otherwise it parses
            // everything from the chunk start up to the next one as the requested section.
            if (offsets[offset] == 0)
                return new List<T>();

            stream.BaseStream.Position = chunkStart + offsets[offset];
            return Collect<T>(stream, function, GetOffsetLength(offsets, offset, chunkSize) / itemSize);
        }

        // This version of the function just skips to the specific entry and loads one of them. More useful than trying to read the data into a list and then getting the entry
        // from the list.
        private static T ReadEntry<T>(EndianBinaryReader stream, LoadTypeFromStream<T> readFunction, long chunkStart, int[] offsets, int offset, int entryNumber, int itemSize)
        {
            if (offsets[offset] == 0)
                return default(T);

            long streamPos = stream.BaseStream.Position;
            stream.BaseStream.Position = (chunkStart + offsets[offset]) + (entryNumber * itemSize);
            T val = readFunction(stream);

            stream.BaseStream.Position = streamPos;
            return val;
        }

        #region Stream Decoding Functions
        private static WLinearColor ReadColor32(EndianBinaryReader stream)
        {
            return new WLinearColor(stream.ReadByte() / 255f, stream.ReadByte() / 255f, stream.ReadByte() / 255f, stream.ReadByte() / 255f);
        }

        private static WLinearColor ReadColorShort(EndianBinaryReader stream)
        {
            ushort r = stream.ReadUInt16();
            ushort g = stream.ReadUInt16();
            ushort b = stream.ReadUInt16();
            ushort a = stream.ReadUInt16();
            return new WLinearColor(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static GXCullMode ReadCullMode(EndianBinaryReader stream)
        {
            return (GXCullMode)stream.ReadInt32();
        }

        private static IndirectTexture ReadIndirectTexture(EndianBinaryReader stream)
        {
            IndirectTexture itm = new IndirectTexture();
            itm.HasLookup = stream.ReadBoolean();
            itm.IndTexStageNum = stream.ReadByte();
            itm.Unknown0 = stream.ReadUInt16();
            itm.Unknown1 = stream.ReadByte();
            itm.Unknown2 = stream.ReadByte();

            for (int i = 0; i < 7; i++)
                itm.Unknown3[i] = stream.ReadUInt16();

            for (int i = 0; i < 3; i++)
            {
                itm.Matrices[i] = new IndirectTextureMatrix(new OpenTK.Matrix2x3(stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle()), stream.ReadByte());
                Trace.Assert(stream.ReadByte() == 0xFF); // Padding
                Trace.Assert(stream.ReadByte() == 0xFF);
                Trace.Assert(stream.ReadByte() == 0xFF);
            }

            for (int i = 0; i < 4; i++)
            {
                itm.Scales[i] = new IndirectTextureScale(stream.ReadByte(), stream.ReadByte());
                Trace.Assert(stream.ReadByte() == 0xFF); // Padding
                Trace.Assert(stream.ReadByte() == 0xFF);
            }

            for (int i = 0; i < 16; i++)
            {
                var indirectTevOrder = new IndirectTevOrder();
                indirectTevOrder.TevStageID = stream.ReadByte();
                indirectTevOrder.IndTexFormat = stream.ReadByte();
                indirectTevOrder.IndTexBiasSel = stream.ReadByte();
                indirectTevOrder.IndTexMtxId = stream.ReadByte();
                indirectTevOrder.IndTexWrapS = stream.ReadByte();
                indirectTevOrder.IndTexWrapT = stream.ReadByte();
                indirectTevOrder.AddPrev = stream.ReadBoolean();
                indirectTevOrder.UtcLod = stream.ReadBoolean();
                indirectTevOrder.AlphaSel = stream.ReadByte();
                Trace.Assert(stream.ReadByte() == 0xFF); // Padding
                Trace.Assert(stream.ReadByte() == 0xFF);
                Trace.Assert(stream.ReadByte() == 0xFF);

                itm.TevOrders[i] = indirectTevOrder;
            }

            return itm;
        }

        private static NBTScale ReadNBTScale(EndianBinaryReader stream)
        {
            var nbtScale = new NBTScale();
            nbtScale.Unknown1 = stream.ReadByte();
            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            Trace.Assert(stream.ReadByte() == 0xFF);
            Trace.Assert(stream.ReadByte() == 0xFF);
            //stream.Skip(3);
            nbtScale.Scale = new OpenTK.Vector3(stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle());
            return nbtScale;
        }

        private static ZMode ReadZMode(EndianBinaryReader stream)
        {
            var retVal = new ZMode
            {
                Enable = stream.ReadBoolean(),
                Function = (GXCompareType)stream.ReadByte(),
                UpdateEnable = stream.ReadBoolean(),
            };

            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            return retVal;
        }

        private static AlphaTest ReadAlphaCompare(EndianBinaryReader stream)
        {
            var retVal = new AlphaTest
            {
                Comp0 = (GXCompareType)stream.ReadByte(),
                Reference0 = stream.ReadByte(),
                Operation = (GXAlphaOp)stream.ReadByte(),
                Comp1 = (GXCompareType)stream.ReadByte(),
                Reference1 = stream.ReadByte()
            };

            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            return retVal;
        }

        private static BlendMode ReadBlendMode(EndianBinaryReader stream)
        {
            return new BlendMode
            {
                Type = (GXBlendMode)stream.ReadByte(),
                SourceFactor = (GXBlendModeControl)stream.ReadByte(),
                DestinationFactor = (GXBlendModeControl)stream.ReadByte(),
                Operation = (GXLogicOp)stream.ReadByte()
            };
        }

        private static ColorChannelControl ReadChannelControl(EndianBinaryReader stream)
        {
            var retVal = new ColorChannelControl
            {
                LightingEnabled = stream.ReadBoolean(),
                MaterialSrc = (GXColorSrc)stream.ReadByte(),
                LitMask = (GXLightMask)stream.ReadByte(),
                DiffuseFunction = (GXDiffuseFunction)stream.ReadByte(),
                AttenuationFunction = (GXAttenuationFunction)stream.ReadByte(),
                AmbientSrc = (GXColorSrc)stream.ReadByte()
            };

            Trace.Assert(stream.ReadUInt16() == 0xFFFF); // Padding
            return retVal;
        }

        private static TexCoordGen ReadTexCoordGen(EndianBinaryReader stream)
        {
            var retVal = new TexCoordGen
            {
                Type = (GXTexGenType)stream.ReadByte(),
                Source = (GXTexGenSrc)stream.ReadByte(),
                TexMatrixSource = (GXTexMatrix)stream.ReadByte()
            };

            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            return retVal;
        }

        private static TexMatrix ReadTexMatrix(EndianBinaryReader stream)
        {
            var retVal = new TexMatrix();
            retVal.Projection = (TexMatrixProjection)stream.ReadByte();
            retVal.Type = stream.ReadByte();
            Trace.Assert(stream.ReadUInt16() == 0xFFFF); // Padding
            retVal.CenterS = stream.ReadSingle();
            retVal.CenterT = stream.ReadSingle();
            retVal.CenterW = stream.ReadSingle();
            retVal.ScaleS = stream.ReadSingle();
            retVal.ScaleT = stream.ReadSingle();
            retVal.Rotation = stream.ReadInt16() * (180 / 32768f);
            Trace.Assert(stream.ReadUInt16() == 0xFFFF); // Padding
            retVal.TranslateS = stream.ReadSingle();
            retVal.TranslateT = stream.ReadSingle();

            // We know this isn't always an Identity Matrix in the case of some advanced effects, but most of the time it is identity.
            retVal.Matrix = new OpenTK.Matrix4();
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    retVal.Matrix[x, y] = stream.ReadSingle();
                }
            }

            //Trace.Assert(retVal.Matrix == OpenTK.Matrix4.Identity);

            return retVal;
        }

        private static TevIn ReadTevIn(EndianBinaryReader stream)
        {
            return new TevIn { A = stream.ReadByte(), B = stream.ReadByte(), C = stream.ReadByte(), D = stream.ReadByte() };
        }

        private static TevOp ReadTevOp(EndianBinaryReader stream)
        {
            return new TevOp
            {
                Operation = stream.ReadByte(),
                Bias = stream.ReadByte(),
                Scale = stream.ReadByte(),
                Clamp = stream.ReadByte(),
                Out = stream.ReadByte()
            };
        }

        private static TevOrder ReadTevOrder(EndianBinaryReader stream)
        {
            var retVal = new TevOrder
            {
                TexCoordId = (GXTexCoordSlot)stream.ReadByte(),
                TexMap = stream.ReadByte(),
                ChannelId = (GXColorChannelId)stream.ReadByte()
            };

            Trace.Assert(stream.ReadByte() == 0xFF); // Padding
            return retVal;
        }

        private static TevStage ReadTevCombinerStage(EndianBinaryReader stream)
        {
            var retVal = new TevStage();
            retVal.Unknown0 = stream.ReadByte();
            for (int i = 0; i < 4; i++)
                retVal.ColorIn[i] = (GXCombineColorInput)stream.ReadByte();
            retVal.ColorOp = (GXTevOp)stream.ReadByte();
            retVal.ColorBias = (GXTevBias)stream.ReadByte();
            retVal.ColorScale = (GXTevScale)stream.ReadByte();
            retVal.ColorClamp = stream.ReadBoolean();
            retVal.ColorRegister = (GXRegister)stream.ReadByte();
            for (int i = 0; i < 4; i++)
                retVal.AlphaIn[i] = (GXCombineAlphaInput)stream.ReadByte();
            retVal.AlphaOp = (GXTevOp)stream.ReadByte();
            retVal.AlphaBias = (GXTevBias)stream.ReadByte();
            retVal.AlphaScale = (GXTevScale)stream.ReadByte();
            retVal.AlphaClamp = stream.ReadBoolean();
            retVal.AlphaRegister = (GXRegister)stream.ReadByte();
            retVal.Unknown1 = stream.ReadByte();

            Trace.Assert(retVal.Unknown0 == 0xFF);
            Trace.Assert(retVal.Unknown1 == 0xFF);
            return retVal;
        }

        private static TevSwapMode ReadTevSwapMode(EndianBinaryReader stream)
        {
            var retVal = new TevSwapMode
            {
                RasSel = stream.ReadByte(),
                TexSel = stream.ReadByte()
            };

            Trace.Assert(stream.ReadUInt16() == 0xFFFF); // Padding
            return retVal;
        }

        private static TevSwapModeTable ReadTevSwapModeTable(EndianBinaryReader stream)
        {
            return new TevSwapModeTable
            {
                R = stream.ReadByte(),
                G = stream.ReadByte(),
                B = stream.ReadByte(),
                A = stream.ReadByte()
            };
        }

        private static FogInfo ReadFogInfo(EndianBinaryReader stream)
        {
            var retVal = new FogInfo();

            retVal.Type = stream.ReadByte();
            retVal.Enable = stream.ReadBoolean();
            retVal.Center = stream.ReadUInt16();
            retVal.StartZ = stream.ReadSingle();
            retVal.EndZ = stream.ReadSingle();
            retVal.NearZ = stream.ReadSingle();
            retVal.FarZ = stream.ReadSingle();
            retVal.Color = ReadColor32(stream);

            retVal.RangeAdjustmentTable = new float[10];
            for (int i = 0; i < retVal.RangeAdjustmentTable.Length; i++)
            {
                ushort inVal = stream.ReadUInt16();
                float finalVal = (float)inVal / 256;

                retVal.RangeAdjustmentTable[i] = finalVal;
            }

            return retVal;
        }

        private static int ReadInt32(EndianBinaryReader stream)
        {
            return stream.ReadInt32();
        }

        private static byte ReadByte(EndianBinaryReader stream)
        {
            return stream.ReadByte();
        }

        private static short ReadShort(EndianBinaryReader stream)
        {
            return stream.ReadInt16();
        }

        private static bool ReadBool(EndianBinaryReader stream)
        {
            return stream.ReadBoolean();
        }
        #endregion

        private static int GetOffsetLength(int[] dataOffsets, int currentIndex, int endChunkOffset)
        {
            int currentOffset = dataOffsets[currentIndex];

            // Find the next available offset in the array, and subtract the two offsets to get the length of the data.
            for (int i = currentIndex + 1; i < dataOffsets.Length; i++)
            {
                if (dataOffsets[i] != 0)
                {
                    return dataOffsets[i] - currentOffset;
                }
            }

            // If we didn't find a dataOffset that was valid, then we go to the end of the chunk.
            return endChunkOffset - currentOffset;
        }
    }
}
