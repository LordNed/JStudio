using GameFormatReader.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JStudio.J3D.Animation
{
    public enum LoopType
    {
        Once = 0, // 1 also plays once.
        Loop = 2,
        YoYo = 3, // Play once forward, then once backward.
        YoYoLoop = 4, // Play forward, then backwards, then repeat.
    }

    public enum TangentType : ushort
    {
        TangentIn = 0,
        TangentInOut = 1
    }

    public abstract class BaseJ3DAnimation
    {
        protected struct AnimIndex
        {
            public ushort Count;
            public ushort Index;
            public TangentType KeyTangentType;
        }

        protected struct AnimComponent
        {
            public AnimIndex Scale;
            public AnimIndex Rotation;
            public AnimIndex Translation;
        }

        protected struct AnimatedJoint
        {
            public AnimComponent X;
            public AnimComponent Y;
            public AnimComponent Z;
        }

        protected class Key
        {
            public float Time;
            public float Value;
            public float TangentIn;
            public float TangentOut;

            public Key(float time, float val, float tangentIn, float tangentOut)
            {
                Time = time;
                Value = val;
                TangentIn = tangentIn;
                TangentOut = tangentOut;
            }

            public Key(float time, float val, float tangentIn) : this(time, val, tangentIn, tangentIn) { }
        }

        public string Name { get; protected set; }
        public string Magic { get; protected set; }
        public string AnimType { get; protected set; }
        public LoopType LoopMode { get; set; }
        public short AnimLengthInFrames { get; protected set; }

        public string AnimLengthInSeconds { get { return string.Format("{0}s", (AnimLengthInFrames / kAnimFramerate).ToString("0.00")); } }

        protected const float kAnimFramerate = 30f;
        protected float m_timeSinceStartedPlaying;
        protected bool m_isPlaying;

        protected OpenTK.Matrix4 m_hermiteMatrix = new OpenTK.Matrix4(2, -2, 1, 1, -3, 3, -2, -1, 0, 0, 1, 0, 1, 0, 0, 0);

        public BaseJ3DAnimation(string name)
        {
            Name = name;
        }

        public virtual void Tick(float deltaTime)
        {
            if(m_isPlaying)
                m_timeSinceStartedPlaying += deltaTime;
        }

        public virtual void Start()
        {
            m_isPlaying = true;
            m_timeSinceStartedPlaying = 0f;
        }

        public virtual void Stop()
        {
            m_isPlaying = false;
            m_timeSinceStartedPlaying = 0f;
        }

        public virtual void Pause()
        {
            m_isPlaying = false;
        }

        public virtual void Resume()
        {
            m_isPlaying = true;
        }

        public virtual void SetCurrentFrame(int frameIndex)
        {
            m_timeSinceStartedPlaying = frameIndex / kAnimFramerate;
        }

        protected virtual float GetAnimValue(List<Key> keys, float frameTime)
        {
            if (keys.Count == 0)
                return 0f;

            if (keys.Count == 1)
                return keys[0].Value;

            int i = 1;
            while (keys[i].Time < frameTime)
            {
                i++;
                // This fixes the case where the last frame of the animation doesn't have a key, we'll just hold on the last key.
                if (i >= keys.Count)
                {
                    i = keys.Count - 1;
                    frameTime = keys[keys.Count - 1].Time;

                    break;
                }
            }
            
            float time = (frameTime - keys[i - 1].Time) / (keys[i].Time - keys[i - 1].Time); // Scale to [0, 1]

            return HermiteInterpolation(keys[i - 1], keys[i], time);
        }

        protected virtual float CubicInterpolation(Key key1, Key key2, float t)
        {
            float a = 2 * (key1.Value - key2.Value) + key1.TangentOut + key2.TangentIn;
            float b = -3 * key1.Value + 3 * key2.Value - 2 * key1.TangentOut - key2.TangentIn;
            float c = key1.TangentOut;
            float d = key1.Value;

            return ((a * t + b) * t + c) * t + d;   
        }

        protected virtual float HermiteInterpolation(Key key1, Key key2, float t)
        {
            float numFramesBetweenKeys = key2.Time - key1.Time;

            OpenTK.Vector4 s = new OpenTK.Vector4(t * t * t, t * t, t, 1);
            OpenTK.Vector4 c = new OpenTK.Vector4(key1.Value, key2.Value, key1.TangentOut * numFramesBetweenKeys, key2.TangentIn * numFramesBetweenKeys);
            OpenTK.Vector4 result = OpenTK.Vector4.Transform(s, m_hermiteMatrix);
            result = OpenTK.Vector4.Multiply(result, c);

            return result[0] + result[1] + result[2] + result[3];

            /*float h1 = (2 * (float)Math.Pow(t, 3)) - (3 * (float)Math.Pow(t, 2)) + 1;
            float h2 = (-2 * (float)Math.Pow(t, 3)) + (3 * (float)Math.Pow(t, 2)) + t;
            float h3 = (float)Math.Pow(t, 3) - (2 * (float)Math.Pow(t, 2)) + t;
            float h4 = (float)Math.Pow(t, 3) - (float)Math.Pow(t, 2);
            float dist = key2.Value - key1.Value;

            float output = (h1 * key1.Value) + (h2 * key2.Value) + (h3 * (key1.TangentOut * dist)) + (h4 * (key2.TangentIn * dist));
            //Console.WriteLine(output);

            return output;*/
        }

        protected virtual float LinearInterpolation(Key key1, Key key2, float t)
        {
            return WindEditor.WMath.Lerp(key1.Value, key2.Value, t);
        }

        protected void ConvertRotation(List<Key> rots, float scale)
        {
            for (int j = 0; j < rots.Count; j++)
            {
                rots[j].Value *= scale;
                rots[j].TangentIn *= scale;
                rots[j].TangentOut *= scale;
            }
        }

        protected AnimIndex ReadAnimIndex(EndianBinaryReader stream)
        {
            return new AnimIndex { Count = stream.ReadUInt16(), Index = stream.ReadUInt16(), KeyTangentType = (TangentType)stream.ReadUInt16() };
        }

        protected AnimComponent ReadAnimComponent(EndianBinaryReader stream)
        {
            return new AnimComponent { Scale = ReadAnimIndex(stream), Rotation = ReadAnimIndex(stream), Translation = ReadAnimIndex(stream) };
        }

        protected AnimatedJoint ReadAnimJoint(EndianBinaryReader stream)
        {
            return new AnimatedJoint { X = ReadAnimComponent(stream), Y = ReadAnimComponent(stream), Z = ReadAnimComponent(stream) };
        }

        protected List<Key> ReadComp(float[] src, AnimIndex index)
        {
            List<Key> ret = new List<Key>();

            if (index.Count == 1)
            {
                ret.Add(new Key(0f, src[index.Index], 0f, 0f));
            }
            else
            {
                for (int j = 0; j < index.Count; j++)
                {
                    Key key = null;
                    if (index.KeyTangentType == TangentType.TangentIn)
                        key = new Key(src[index.Index + 3 * j + 0], src[index.Index + 3 * j + 1], src[index.Index + 3 * j + 2]);
                    else
                        key = new Key(src[index.Index + 4 * j + 0], src[index.Index + 4 * j + 1], src[index.Index + 4 * j + 2], src[index.Index + 4 * j + 3]);

                    ret.Add(key);
                }
            }

            return ret;
        }
    }
}
