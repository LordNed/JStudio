using GameFormatReader.Common;
using JStudio.J3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JStudio.JStudio.J3D.ExternalTypes
{
    public class BMT : IDisposable
    {
        public string Name { get; private set; }
        public string Magic { get; private set; }
        public string FileType { get; private set; }
        public int MaterialsCount { get { return MAT3.MaterialList.Count; } }
        public int TexturesCount { get { return TEX1.Textures.Count; } }

        public MAT3 MAT3 { get { return m_mat3Section; } }
        public TEX1 TEX1 { get { return m_tex1Section; } }

        private MAT3 m_mat3Section;
        private TEX1 m_tex1Section;

        public BMT(string name)
        {
            Name = name;
        }

        public void LoadFromStream(EndianBinaryReader reader)
        {
            // Read the J3D Header
            Magic = new string(reader.ReadChars(4)); // "J3D2"
            FileType = new string(reader.ReadChars(4)); // bmt3

            int fileSize = reader.ReadInt32();
            int tagCount = reader.ReadInt32();

            // Skip over an unused space.
            reader.Skip(16); // SVR3 header

            LoadTagDataFromStream(reader, tagCount);
        }

        private void LoadTagDataFromStream(EndianBinaryReader reader, int tagCount)
        {
            for (int i = 0; i < tagCount; i++)
            {
                long tagStart = reader.BaseStream.Position;

                string tagName = reader.ReadString(4);
                int tagSize = reader.ReadInt32();

                switch (tagName)
                {
                    case "MAT3":
                        LoadMAT3FromStream(reader, tagStart);
                        break;
                    case "TEX1":
                        LoadTEX1FromStream(reader, tagStart);
                        break;
                    default:
                        Console.WriteLine("Unsupported section in BMT File: {0}", tagName); break;
                }

                // Skip the stream reader to the start of the next tag since it gets moved around during loading.
                reader.BaseStream.Position = tagStart + tagSize;
            }
        }

        private void LoadMAT3FromStream(EndianBinaryReader reader, long tagStart)
        {
            m_mat3Section = new MAT3();
            m_mat3Section.LoadMAT3FromStream(reader, tagStart);
        }

        private void LoadTEX1FromStream(EndianBinaryReader reader, long tagStart)
        {
            m_tex1Section = new TEX1();
            m_tex1Section.LoadTEX1FromStream(reader, tagStart, false);
        }

        #region IDisposable Support
        // To detect redundant calls
        private bool m_hasBeenDisposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!m_hasBeenDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    foreach (var texture in TEX1.Textures)
                        texture.Dispose();

                    foreach (var material in MAT3.MaterialList)
                        if (material.Shader != null)
                            material.Shader.Dispose();
                }

                m_hasBeenDisposed = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~BMT()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
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
}
