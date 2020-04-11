using GameFormatReader.Common;
using OpenTK;
using System;
using System.Collections.Generic;

namespace JStudio.J3D.Animation
{
    public class BRK : BaseJ3DAnimation
    {
        private class RegisterAnim
        {
            public List<Key> RedChannel = new List<Key>();
            public List<Key> GreenChannel = new List<Key>();
            public List<Key> BlueChannel = new List<Key>();
            public List<Key> AlphaChannel = new List<Key>();
            public int ColorID;
        }

        private List<RegisterAnim> m_colorAnimationData;
        private List<RegisterAnim> m_konstAnimationData;

        private StringTable m_colorStringTable;
        private StringTable m_konstStringTable;

        private int[] m_colorRemapTable;
        private int[] m_konstRemapTable;

        public BRK(string name) : base(name) { }

        public void LoadFromStream(EndianBinaryReader reader)
        {
            // Read the J3D Header
            Magic = new string(reader.ReadChars(4)); // "J3D1"
            AnimType = new string(reader.ReadChars(4)); // "brk1"

            int fileSize = reader.ReadInt32();
            int tagCount = reader.ReadInt32();

            // Skip over an unused space.
            reader.Skip(16);

            LoadTagDataFromFile(reader);
        }

        public void ApplyAnimationToMaterials(MAT3 pose, TevColorOverride overrides)
        {
            float ftime = (m_timeSinceStartedPlaying * kAnimFramerate) % AnimLengthInFrames;

            for (int i = 0; i < m_colorAnimationData.Count; i++)
            {
                Material mat = null;

                foreach (var materal in pose.MaterialList)
                {
                    if (materal.Name == m_colorStringTable.Strings[i].String)
                    {
                        mat = materal;
                        break;
                    }
                }

                if (mat == null)
                    continue;

                var col = mat.TevColorIndexes[m_colorAnimationData[i].ColorID];
                float newR = GetAnimValue(m_colorAnimationData[i].RedChannel, ftime);
                float newG = GetAnimValue(m_colorAnimationData[i].GreenChannel, ftime);
                float newB = GetAnimValue(m_colorAnimationData[i].BlueChannel, ftime);
                float newA = GetAnimValue(m_colorAnimationData[i].AlphaChannel, ftime);

                mat.TevColorIndexes[m_colorAnimationData[i].ColorID] = new WindEditor.WLinearColor(newR, newG, newB, newA);
            }

            for (int i = 0; i < m_konstAnimationData.Count; i++)
            {
                Material mat = null;

                foreach (var materal in pose.MaterialList)
                {
                    if (materal.Name == m_konstStringTable.Strings[i].String)
                    {
                        mat = materal;
                        break;
                    }
                }

                if (mat == null)
                    continue;

                var col = mat.TevKonstColorIndexes[m_konstAnimationData[i].ColorID];
                float newR = GetAnimValue(m_konstAnimationData[i].RedChannel, ftime);
                float newG = GetAnimValue(m_konstAnimationData[i].GreenChannel, ftime);
                float newB = GetAnimValue(m_konstAnimationData[i].BlueChannel, ftime);
                float newA = GetAnimValue(m_konstAnimationData[i].AlphaChannel, ftime);

                mat.TevKonstColorIndexes[m_konstAnimationData[i].ColorID] = new WindEditor.WLinearColor(newR, newG, newB, newA);
            }
        }

        private void LoadTagDataFromFile(EndianBinaryReader reader)
        {
            long tagStart = reader.BaseStream.Position;

            string tagName = reader.ReadString(4);
            int tagSize = reader.ReadInt32();

            LoopMode = (LoopType)reader.ReadByte();
            byte angleMultiplier = reader.ReadByte(); // Probably just padding in BRK

            AnimLengthInFrames = reader.ReadInt16();
            short colorAnimEntryCount = reader.ReadInt16();
            short konstAnimEntryCount = reader.ReadInt16();

            short numColorREntries = reader.ReadInt16();
            short numColorGEntries = reader.ReadInt16();
            short numColorBEntries = reader.ReadInt16();
            short numColorAEntries = reader.ReadInt16();

            short numKonstREntries = reader.ReadInt16();
            short numKonstGEntries = reader.ReadInt16();
            short numKonstBEntries = reader.ReadInt16();
            short numKonstAEntries = reader.ReadInt16();

            int colorAnimDataOffset = reader.ReadInt32();
            int konstAnimDataOffset = reader.ReadInt32();

            int colorRemapTableOffset = reader.ReadInt32();
            int konstRemapTableOffset = reader.ReadInt32();

            int colorStringTableOffset = reader.ReadInt32();
            int konstStringTableOffset = reader.ReadInt32();

            int colorRTableOffset = reader.ReadInt32();
            int colorGTableOffset = reader.ReadInt32();
            int colorBTableOffset = reader.ReadInt32();
            int colorATableOffset = reader.ReadInt32();

            int konstRTableOffset = reader.ReadInt32();
            int konstGTableOffset = reader.ReadInt32();
            int konstBTableOffset = reader.ReadInt32();
            int konstATableOffset = reader.ReadInt32();

            reader.Skip(8); // padding

            float[] colorRData = new float[numColorREntries];
            reader.BaseStream.Position = tagStart + colorRTableOffset;
            for (int i = 0; i < numColorREntries; i++)
                colorRData[i] = reader.ReadInt16();

            float[] colorGData = new float[numColorGEntries];
            reader.BaseStream.Position = tagStart + colorGTableOffset;
            for (int i = 0; i < numColorGEntries; i++)
                colorGData[i] = reader.ReadInt16();

            float[] colorBData = new float[numColorBEntries];
            reader.BaseStream.Position = tagStart + colorBTableOffset;
            for (int i = 0; i < numColorBEntries; i++)
                colorBData[i] = reader.ReadInt16();

            float[] colorAData = new float[numColorAEntries];
            reader.BaseStream.Position = tagStart + colorATableOffset;
            for (int i = 0; i < numColorAEntries; i++)
                colorAData[i] = reader.ReadInt16();

            float[] konstRData = new float[numKonstREntries];
            reader.BaseStream.Position = tagStart + konstRTableOffset;
            for (int i = 0; i < numKonstREntries; i++)
                konstRData[i] = reader.ReadInt16();

            float[] konstGData = new float[numKonstGEntries];
            reader.BaseStream.Position = tagStart + konstGTableOffset;
            for (int i = 0; i < numKonstGEntries; i++)
                konstGData[i] = reader.ReadInt16();

            float[] konstBData = new float[numKonstBEntries];
            reader.BaseStream.Position = tagStart + konstBTableOffset;
            for (int i = 0; i < numKonstBEntries; i++)
                konstBData[i] = reader.ReadInt16();

            float[] konstAData = new float[numKonstAEntries];
            reader.BaseStream.Position = tagStart + konstATableOffset;
            for (int i = 0; i < numKonstAEntries; i++)
                konstAData[i] = reader.ReadInt16();

            m_colorRemapTable = new int[colorAnimEntryCount];
            reader.BaseStream.Position = tagStart + colorRemapTableOffset;
            for (int i = 0; i < colorAnimEntryCount; i++)
                m_colorRemapTable[i] = reader.ReadInt16();

            m_konstRemapTable = new int[konstAnimEntryCount];
            reader.BaseStream.Position = tagStart + konstRemapTableOffset;
            for (int i = 0; i < konstAnimEntryCount; i++)
                m_konstRemapTable[i] = reader.ReadInt16();

            reader.BaseStream.Position = tagStart + colorStringTableOffset;
            m_colorStringTable = StringTable.FromStream(reader);

            reader.BaseStream.Position = tagStart + konstStringTableOffset;
            m_konstStringTable = StringTable.FromStream(reader);

            m_colorAnimationData = new List<RegisterAnim>();

            reader.BaseStream.Position = tagStart + colorAnimDataOffset;
            for (int i = 0; i < colorAnimEntryCount; i++)
            {
                AnimIndex colorRIndex = ReadAnimIndex(reader);
                AnimIndex colorGIndex = ReadAnimIndex(reader);
                AnimIndex colorBIndex = ReadAnimIndex(reader);
                AnimIndex colorAIndex = ReadAnimIndex(reader);
                int colorID = reader.ReadByte();
                reader.Skip(3);

                RegisterAnim regAnim = new RegisterAnim();
                regAnim.ColorID = colorID;

                regAnim.RedChannel = ReadComp(colorRData, colorRIndex);
                regAnim.GreenChannel = ReadComp(colorGData, colorGIndex);
                regAnim.BlueChannel = ReadComp(colorBData, colorBIndex);
                regAnim.AlphaChannel = ReadComp(colorAData, colorAIndex);

                foreach (Key key in regAnim.RedChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                foreach (Key key in regAnim.GreenChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                foreach (Key key in regAnim.BlueChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                foreach (Key key in regAnim.AlphaChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                m_colorAnimationData.Add(regAnim);
            }

            m_konstAnimationData = new List<RegisterAnim>();

            reader.BaseStream.Position = tagStart + konstAnimDataOffset;
            for (int i = 0; i < konstAnimEntryCount; i++)
            {
                AnimIndex konstRIndex = ReadAnimIndex(reader);
                AnimIndex konstGIndex = ReadAnimIndex(reader);
                AnimIndex konstBIndex = ReadAnimIndex(reader);
                AnimIndex konstAIndex = ReadAnimIndex(reader);
                int colorID = reader.ReadByte();
                reader.Skip(3);

                RegisterAnim regAnim = new RegisterAnim();
                regAnim.ColorID = colorID;

                regAnim.RedChannel = ReadComp(konstRData, konstRIndex);
                regAnim.GreenChannel = ReadComp(konstGData, konstGIndex);
                regAnim.BlueChannel = ReadComp(konstBData, konstBIndex);
                regAnim.AlphaChannel = ReadComp(konstAData, konstAIndex);

                foreach (Key key in regAnim.RedChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                foreach (Key key in regAnim.GreenChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                foreach (Key key in regAnim.BlueChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                foreach (Key key in regAnim.AlphaChannel)
                {
                    key.Value = key.Value / 255.0f;
                    key.TangentIn = (float)key.TangentIn / 65535.0f;
                    key.TangentOut = (float)key.TangentOut / 65535.0f;
                }

                m_konstAnimationData.Add(regAnim);
            }
        }
    }
}
