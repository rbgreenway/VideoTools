using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Videoinsight.LIB;
using Microsoft.Win32.SafeHandles;
using NVIDIA;
using System.Xml.Linq;
using System.IO.MemoryMappedFiles;
using System.ComponentModel;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Windows.Forms;

namespace VIClient
{

    public delegate void OnNewLiveFrame(byte[] frame, IntPtr frameGPU, int width, int height, int cameraID, PixelFormat pf, Exception exceptionState);

    public class MFTSoftwareDecoder
    {
        const string MFTSOFTWAREDECODERDLL_NAME = "mftlib32.dll";

        [DllImport(MFTSOFTWAREDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CreateMFTSoftwareDecoder")]
        public static extern IntPtr CreateMFTSoftwareDecoder32(int width, int height, int codec);

        [DllImport(MFTSOFTWAREDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Decode")]
        public static extern int Decode32(IntPtr pDecoder, byte[] frame, UInt32 frameLength, [MarshalAs(UnmanagedType.LPArray)] byte[] decodedFrame, int length, int width, int height, int bpp);

        [DllImport(MFTSOFTWAREDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DestroyDecoder")]
        public static extern void DestroyDecoder32(IntPtr pDecoder);

        const string MFTSOFTWAREDECODERDLL_NAME64 = "mftlib64.dll";

        [DllImport(MFTSOFTWAREDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CreateMFTSoftwareDecoder")]
        public static extern IntPtr CreateMFTSoftwareDecoder64(int width, int height, int codec);

        [DllImport(MFTSOFTWAREDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Decode")]
        public static extern int Decode64(IntPtr pDecoder, byte[] frame, UInt32 frameLength, [MarshalAs(UnmanagedType.LPArray)] byte[] decodedFrame, int length, int width, int height, int bpp);

        [DllImport(MFTSOFTWAREDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DestroyDecoder")]
        public static extern void DestroyDecoder64(IntPtr pDecoder);

    }

    public class IPPDecoder
    {
        const string IPPDECODERDLL_NAME = "ippdecoder32.dll";

        [DllImport(IPPDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CreateDecoder")]
        public static extern IntPtr CreateDecoder32(int codec);

        [DllImport(IPPDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "InitDecoder")]
        public static extern int InitDecoder32(IntPtr pDecoder, byte[] frame, UInt32 frameLength);

        [DllImport(IPPDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Decode")]
        public static extern int Decode32(IntPtr pDecoder, byte[] frame, UInt32 frameLength, [MarshalAs(UnmanagedType.LPArray)] byte[] decodedFrame, int length);

        [DllImport(IPPDECODERDLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DestroyDecoder")]
        public static extern void DestroyDecoder32(IntPtr pDecoder);

        const string IPPDECODERDLL_NAME64 = "ippdecoder64.dll";

        [DllImport(IPPDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CreateDecoder")]
        public static extern IntPtr CreateDecoder64(int codec);

        [DllImport(IPPDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "InitDecoder")]
        public static extern int InitDecoder64(IntPtr pDecoder, byte[] frame, UInt32 frameLength);

        [DllImport(IPPDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Decode")]
        public static extern int Decode64(IntPtr pDecoder, byte[] frame, UInt32 frameLength, [MarshalAs(UnmanagedType.LPArray)] byte[] decodedFrame, int length);

        [DllImport(IPPDECODERDLL_NAME64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DestroyDecoder")]
        public static extern void DestroyDecoder64(IntPtr pDecoder);
    }

    public class StreamAction
    {
        public StreamAction(int action, int val1)
        {
            m_action = action;
            m_val1 = val1;
        }
        public int m_action;
        public int m_val1;
    }

    internal class UncompressedFrame
    {
        private byte[] m_frame;
        private IntPtr m_frameGpu;
        private DecodingSession m_decodingSession;
        private UInt32 m_frameWidth;
        private UInt32 m_frameHeight;
        private DateTime m_frameTime;
        private UInt32 m_cameraID;
        private UInt32 m_frameRate;
        private UInt32 m_frameNum;
        public UncompressedFrame(UInt32 frameNum, byte[] frame, UInt32 frameWidth, UInt32 frameHeight, DateTime frameTime, UInt32 cameraID, UInt32 frameRate = 0)
        {
            m_frameNum = frameNum;
            m_frame = frame;
            m_frameWidth = frameWidth;
            m_frameHeight = frameHeight;
            m_frameTime = frameTime;
            m_cameraID = cameraID;
            m_frameRate = frameRate;
        }
        public UncompressedFrame(UInt32 frameNum, IntPtr frameGPU, DecodingSession decodingSession, UInt32 frameWidth, UInt32 frameHeight, DateTime frameTime, UInt32 cameraID, UInt32 frameRate = 0)
        {
            m_frameNum = frameNum;
            m_frame = null;
            m_frameGpu = frameGPU;
            m_decodingSession = decodingSession;
            m_frameWidth = frameWidth;
            m_frameHeight = frameHeight;
            m_frameTime = frameTime;
            m_cameraID = cameraID;
            m_frameRate = frameRate;
        }
        public UInt32 FrameNumber
        {
            get { return m_frameNum; }
        }
        public byte[] Frame
        {
            get { return m_frame; }
        }
        public UInt32 FrameWidth
        {
            get { return m_frameWidth; }
        }
        public UInt32 FrameHeight
        {
            get { return m_frameHeight; }
        }
        public DateTime FrameTime
        {
            get { return m_frameTime; }
        }
        public UInt32 CameraID
        {
            get { return m_cameraID; }
        }
        public UInt32 FrameRate
        {
            get { return m_frameRate; }
        }
        public IntPtr FrameGPU
        {
            get { return m_frameGpu; }
        }

        public DecodingSession DecodingSession
        {
            get { return m_decodingSession; }
        }
    }

    internal class LiveCompressedFrame
    {
        public LiveCompressedFrame(DATA_FRAME_HEADER dfh, byte[] frame)
        {
            m_dfh = dfh;
            m_frame = frame;
        }
        public byte[] Frame
        {
            get { return m_frame; }
        }
        public DATA_FRAME_HEADER DataFrameHeader
        {
            get { return m_dfh; }
        }
        private byte[] m_frame;
        private DATA_FRAME_HEADER m_dfh;
    }


    internal class VideoFrame
    {
        public byte[] frame = null;
        public UInt64 frameTime = 0;
    }

    internal class KeyVideoFrame : VideoFrame
    {
        public ConcurrentDictionary<UInt32, VideoFrame> dependentFrames;
    }

    internal class VideoFrames
    {
        public UInt32 frameWidth;
        public UInt32 frameHeight;
        public UInt32 frameRate;
        public UInt32 VideoFormat;
        public UInt32 AveGOPSize;
        public ConcurrentDictionary<UInt32, KeyVideoFrame> keyFrames;
    }


    internal class CompressedFrame
    {
        public CompressedFrame(PLAYER_FRAME pf, UInt32 frameWidth, UInt32 frameHeight, UInt32 frameRate, UInt32 totalFrames, UInt32 VideoFormat, UInt32 AveGOPSize, byte[] frame, bool key)
        {
            m_pf = pf;
            m_frame = frame;
            m_key = key;
            m_frameWidth = frameWidth;
            m_frameHeight = frameHeight;
            m_frameRate = frameRate;
            m_totalFrames = totalFrames;
            m_videoFormat = VideoFormat;
            m_aveGOPSize = AveGOPSize;
        }
        public byte[] Frame
        {
            get { return m_frame; }
        }
        public PLAYER_FRAME PlayerFrame
        {
            get { return m_pf; }
        }
        public bool key
        {
            get { return m_key; }
        }
        public UInt32 FrameWidth
        {
            get { return m_frameWidth; }
        }
        public UInt32 FrameHeight
        {
            get { return m_frameHeight; }
        }
        public UInt32 VideoFormat
        {
            get { return m_videoFormat; }
        }
        public UInt32 FrameRate
        {
            get { return m_frameRate; }
        }
        private byte[] m_frame;
        private PLAYER_FRAME m_pf;
        private bool m_key;
        private UInt32 m_frameWidth;
        private UInt32 m_frameHeight;
        private UInt32 m_frameRate;
        private UInt32 m_totalFrames;
        private UInt32 m_videoFormat;
        private UInt32 m_aveGOPSize;
    }

    
    internal class DecodingSession
    {
        public virtual byte[] Decode(byte[] frame, int width, int height, byte keyFlag, long pts = 0) { return null; }
        public virtual IntPtr DecodeGPU(IntPtr Cuda, byte[] frame, int width, int height, byte keyFlag, long pts = 0) { return IntPtr.Zero; }                       
        public virtual void CloseDecoder() { }
    }

    internal class MFTDecodingSession : DecodingSession
    {
        private IntPtr m_decoder;
        private byte[] decodedFrame = null;
        private bool m_initialized = false;
        private int width;
        private int height;
        private int codec;

        public MFTDecodingSession(int width, int height, int codec)
        {
            this.width = width;
            this.height = height;
            this.codec = codec;
        }

        public override byte[] Decode(byte[] frame, int width, int height, byte keyFlag, long pts = 0)
        {
            decodedFrame = null;
            try 
            {
                decodedFrame = new byte[width * height  * 4];
                if (Environment.Is64BitProcess)
                {
                    if(MFTSoftwareDecoder.Decode64(m_decoder, frame, (uint)frame.Length, decodedFrame, decodedFrame.Length, width, height, 32) == 0)
                    {
                        decodedFrame = null;
                    }
                }
                else
                {
                    if (MFTSoftwareDecoder.Decode32(m_decoder, frame, (uint)frame.Length, decodedFrame, decodedFrame.Length, width, height, 32) == 0)
                    {
                        decodedFrame = null;
                    }
                }
            }
            catch (Exception e)
            {
                // I don't like this.  Log?
                GC.Collect();
            }
            return decodedFrame;
        }

        public bool Init(byte[] frame)
        {
            CloseDecoder();
            if (Environment.Is64BitProcess)
            {
                try
                {
                    m_decoder = MFTSoftwareDecoder.CreateMFTSoftwareDecoder64(width, height, codec);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
                return m_initialized = m_decoder != null;
            }
            else
            {
                m_decoder = MFTSoftwareDecoder.CreateMFTSoftwareDecoder32(width, height, codec);
                return m_initialized = m_decoder != null;
            }


        }

        public override void CloseDecoder()
        {
            decodedFrame = null;
            if (m_decoder != IntPtr.Zero)
            {
                if (Environment.Is64BitProcess)
                {
                    MFTSoftwareDecoder.DestroyDecoder64(m_decoder);
                }
                else
                {
                    MFTSoftwareDecoder.DestroyDecoder32(m_decoder);
                }

                m_decoder = IntPtr.Zero;
            }
        }

        ~MFTDecodingSession()
        {
            CloseDecoder();
        }
    
    }

    internal class IPPDecodingSession : DecodingSession
    {
        public enum CODEC {
			NONE,
			MJPEG,
			H264,
			MPEG4,
		};

        private IntPtr m_decoder;
        private int m_cameraID;
        private int m_width;
        private int m_height;

        public IntPtr Decoder { get { return m_decoder; } }
        private byte[] decodedFrame;

        private bool m_initialized = false;
        private CODEC m_codec;

        public IPPDecodingSession(int cameraID, int width, int height, CODEC codec)
        {
            m_decoder = IntPtr.Zero;
            m_cameraID = cameraID;
            m_width = width;
            m_height = height;
            m_codec = codec;
        }

        ~IPPDecodingSession()
        {
            CloseDecoder();
        }

        public override void CloseDecoder()
        {
            decodedFrame = null;
            if (m_decoder != IntPtr.Zero)
            {
                if(Environment.Is64BitProcess)
                {
                    IPPDecoder.DestroyDecoder64(m_decoder);
                }
                else
                {
                    IPPDecoder.DestroyDecoder32(m_decoder);
                }
                
                m_decoder = IntPtr.Zero;
            }
        }

        public bool Init(byte[] frame)
        {
            CloseDecoder();
            if (Environment.Is64BitProcess) 
            { 
                m_decoder = IPPDecoder.CreateDecoder64((int)m_codec);
                return m_initialized = IPPDecoder.InitDecoder64(m_decoder, frame, (uint)frame.Length) == 1;
            }
            else
            {
                m_decoder = IPPDecoder.CreateDecoder32((int)m_codec);
                return m_initialized = IPPDecoder.InitDecoder32(m_decoder, frame, (uint)frame.Length) == 1;
            }
            
         
        }

        public override byte[] Decode(byte[] frame, int width, int height, byte keyFlag, long pts = 0)
        {
            decodedFrame = null;
            try 
            {
                //if(decodedFrame==null)
                //{
                decodedFrame = new byte[width * height * 3];
                //}
                if (Environment.Is64BitProcess)
                {
                    IPPDecoder.Decode64(m_decoder, frame, (uint)frame.Length, decodedFrame, decodedFrame.Length);
                }
                else
                {
                    IPPDecoder.Decode32(m_decoder, frame, (uint)frame.Length, decodedFrame, decodedFrame.Length);
                }

            }
            catch (Exception e)
            {
                // I don't like this.  Log?
                GC.Collect();
            }
            return decodedFrame;
        }
    }

    internal class VIDecodingSession : DecodingSession
    {

        private VI_Decoder m_decoder;
        private int m_cameraID;
        private int m_width;
        private int m_height;
        private Videoinsight.LIB.VITypes.StreamingMode m_codec;

        public VI_Decoder Decoder { get { return m_decoder; } }
        private byte[] decodedFrame;

        private bool m_initialized = false;

        public VIDecodingSession(int cameraID, int width, int height, byte codec)
        {
            m_decoder = null;
            m_cameraID = cameraID;
            m_width = width;
            m_height = height;
            m_codec = (Videoinsight.LIB.VITypes.StreamingMode)codec;
        }

        ~VIDecodingSession()
        {
            CloseDecoder();
        }

        public override void CloseDecoder()
        {
            decodedFrame = null;
            if (m_decoder != null)
            {
                m_decoder.Close();
                m_decoder = null;
            }
        }

        public bool Init()
        {
            MediaShare.FourCCValue fourcc = MediaShare.StreamingMode2Fourcc(m_codec);
            bool bUseHarwareDecoder = false;
            CloseDecoder();
            m_initialized = false;
            return ((m_decoder = MediaShare.GetDecoder(m_width, m_height, 24, 24, fourcc, MediaShare.FourCCValue.BI_RGB, 25, 1, VI_Decoder.CodecsType.VIDEOINSIGHT, true, bUseHarwareDecoder)) != null);
        }

        public override byte[] Decode(byte[] frame, int width, int height, byte keyFlag, long pts = 0)
        {
            IntPtr imageAddr = IntPtr.Zero;
            int imageAddrLength = 0;
            bool key = keyFlag == 1 ? true : false;

            byte[] result = null;

            if (width == m_width && height == m_height)
            {
                if (m_decoder == null)
                {
                    Init();
                }
                if (m_decoder != null)
                {

                    if (m_decoder.DecodingOneFrm(frame, frame.Length, ref imageAddr, ref key, ref imageAddrLength, ref width, ref height, true))
                    {
                        m_initialized = true;
                        if (width == m_width && height == m_height)
                        {
                            if (decodedFrame == null)
                            {
                                decodedFrame = new byte[imageAddrLength];
                            }

                            bool bFlip = true;

                            if (!bFlip)
                            {
                                Marshal.Copy(imageAddr, decodedFrame, 0, imageAddrLength);
                            }
                            else
                            {
                                int stride = GetStride(width, PixelFormat.Format24bppRgb);
                                Bitmap bitmap = new Bitmap(width, height, stride, PixelFormat.Format24bppRgb, imageAddr);
                                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                                BitmapData bd = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                                Marshal.Copy(bd.Scan0, decodedFrame, 0, decodedFrame.Length);
                                bitmap.UnlockBits(bd);
                                bitmap.Dispose();
                            }

                            result = new byte[decodedFrame.Length];
                            Buffer.BlockCopy(decodedFrame, 0, result, 0, decodedFrame.Length);
                        }
                    }
                    else
                    {
                        //Debug.Print("Decoding error");
                        /*
                        if (m_initialized == false && key == false)
                        {

                        }
                        else
                            throw new Exception("No available decoder for the given codec");
                         * */
                    }
                }
            }
            return result;
        }
        public static int GetStride(int width, PixelFormat pxFormat)
        {
            //float bitsPerPixel = System.Drawing.Image.GetPixelFormatSize(format);
            int bitsPerPixel = ((int)pxFormat >> 8) & 0xFF;
            //Number of bits used to store the image data per line (only the valid data)
            int validBitsPerLine = width * bitsPerPixel;
            //4 bytes for every int32 (32 bits)
            int stride = ((validBitsPerLine + 31) / 32) * 4;
            return stride;
        }
    }
    

    internal class CudaDecodingSession : DecodingSession
    {
        private IntPtr m_cuda;
        private IntPtr m_cudaContext;
        private IntPtr m_videoDecoder;

        private VI_Decoder m_decoder;
        private int m_cameraID;
        private int m_width;
        private int m_height;
        private Videoinsight.LIB.VITypes.StreamingMode m_codec;

        public VI_Decoder Decoder { get { return m_decoder; } }
        private byte[] decodedFrame;

        private bool m_leaveFramesOnGpu;

        private Semaphore m_InputQueueSemaphore;
        private Semaphore m_OutputQueueSemaphore;
        private ManualResetEvent m_DecoderStoppedEvent;

        private bool m_initialized = false;

        public IntPtr GetDecoder()
        {
            return m_videoDecoder;
        }

        public IntPtr GetCuda()
        {
            return m_cuda;
        }

        public CudaDecodingSession(int cameraID, int width, int height, byte codec, bool leaveFramesOnGpu)
        {
            m_cuda = IntPtr.Zero;
            m_cudaContext = IntPtr.Zero;

            m_cameraID = cameraID;
            m_width = width;
            m_height = height;
            m_codec = (Videoinsight.LIB.VITypes.StreamingMode)codec;

            m_leaveFramesOnGpu = leaveFramesOnGpu;

        }

        ~CudaDecodingSession()
        {
            CloseDecoder();
        }

        public override void CloseDecoder()
        {
            decodedFrame = null;
            if (m_videoDecoder != IntPtr.Zero)
            {
                CudaTools.VideoDecoder_Free64(m_videoDecoder);
                m_videoDecoder = IntPtr.Zero;
            }
            if (m_cuda != IntPtr.Zero)
            {
                CudaTools.Cuda_Free64(m_cuda);
                m_cuda = IntPtr.Zero;
            }
            m_initialized = false;
        }

        public void AddCompressedFrame(byte[] frame, int width, int height, byte keyFlag)
        {
            CudaTools.VideoDecoder_NewInputFrame64(m_videoDecoder, frame, frame.Length);
        }


        public IntPtr GetUncompressedFramePtr()
        {
            IntPtr pData = IntPtr.Zero;
            int numBytes;
            int width;
            int height; 
            int format;
            ulong timeStamp;

            int success = CudaTools.VideoDecoder_GetNextDecodedFrameGPU64(m_videoDecoder, out pData, out numBytes, out width, out height, out format, out timeStamp);

            // Do something with the frame

            CudaTools.VideoDecoder_ReleaseFrameGPU64(m_videoDecoder);

            return pData;
        }

        public byte[] GetUncompressedFrame()
        {
            byte[] result = null;
            // Get the new frame
            if (decodedFrame == null)
            {
                decodedFrame = new byte[m_width*m_height*4];
            }
            int width;
            int height;
            int format;
            UInt64 timeStamp;
            IntPtr frame;
            int numBytes;

            CudaTools.VideoDecoder_GetNextDecodedFrame64(m_videoDecoder, out frame, out numBytes, out width, out height, out format, out timeStamp);
            
            // Why the double copy?
            Marshal.Copy(frame, decodedFrame, 0, (int)numBytes);
            CudaTools.VideoDecoder_ReleaseFrame(m_videoDecoder);
            result = new byte[decodedFrame.Length];
            Buffer.BlockCopy(decodedFrame, 0, result, 0, decodedFrame.Length);
            return result;
        }

        public bool Init(IntPtr Cuda = new IntPtr(), CudaTools.CODEC codec = CudaTools.CODEC.H264)
        {
            bool retVal = false;
            CloseDecoder();
            m_cuda = Cuda;

            if(m_cuda != IntPtr.Zero) //CudaTools.Cuda_Create64()) != IntPtr.Zero)  
            {
                ulong t;
                ulong f;
                CudaTools.Cuda_GetDeviceMemory64(m_cuda, out t, out f);
                if (CudaTools.Cuda_GetContext64(m_cuda, out m_cudaContext) == true)
                {
                    if ((m_videoDecoder = CudaTools.VideoDecoder_Create64(m_cudaContext)) != IntPtr.Zero)
                    {

                        if (CudaTools.VideoDecoder_Init64(m_videoDecoder))
                        {
                            if (m_leaveFramesOnGpu)
                                CudaTools.VideoDecoder_ConfigureDecoder64(m_videoDecoder, m_width, m_height, (int)CudaTools.DECODER_MODE.GPU_ARGB, (int)codec);
                            else
                                CudaTools.VideoDecoder_ConfigureDecoder64(m_videoDecoder, m_width, m_height, (int)CudaTools.DECODER_MODE.CPU_ARGB, (int)codec);


                            IntPtr semaphoreInput;
                            IntPtr semaphoreOutput;
                            IntPtr eventStopped;
                            CudaTools.VideoDecoder_GetWindowsHandles64(m_videoDecoder, out semaphoreInput, out semaphoreOutput, out eventStopped);

                            m_InputQueueSemaphore = new Semaphore(0, int.MaxValue);
                            m_InputQueueSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreInput, false);

                            m_OutputQueueSemaphore = new Semaphore(0, int.MaxValue);
                            m_OutputQueueSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreOutput, false);


                            CudaTools.VideoDecoder_Start64(m_videoDecoder);
                            retVal = true;
                        }
                        
                    }
                }
            }
            return retVal;
        }

        public override IntPtr DecodeGPU(IntPtr Cuda, byte[] frame, int width, int height, byte keyFlag, long pts)
        {
            IntPtr result = IntPtr.Zero;

            if (width == m_width && height == m_height)
            {
                if (m_videoDecoder == IntPtr.Zero)
                {
                    Init(Cuda);
                }
                if (m_videoDecoder != IntPtr.Zero)
                {
                    if (m_InputQueueSemaphore.WaitOne(5000))
                    {
                        CudaTools.VideoDecoder_NewInputFrame64(m_videoDecoder, frame, frame.Length);
                        //Debug.Print("FRAME IN");
                    }
                    if (m_OutputQueueSemaphore.WaitOne(0))
                    {
                        // Get the new frame
                        //if (decodedFrame == null)
                        //{
                        //    decodedFrame = new byte[m_width * m_height * 4];
                        //}
                        //Debug.Print("FRAME OUT");
                        int width1;
                        int height1;
                        int format;
                        UInt64 timeStamp;
                        IntPtr frame1;
                        int numBytes;

                        
                           
                            CudaTools.VideoDecoder_GetNextDecodedFrameGPU64(m_videoDecoder, out frame1, out numBytes, out width1, out height1, out format, out timeStamp);

                            result = frame1;

                            

                            // TEMP - for testing purposes, copy the frame from the GPU to the CPU                           

                            
                            //GCHandle pinnedArray = GCHandle.Alloc(decodedFrame, GCHandleType.Pinned);
                            //IntPtr ptr = pinnedArray.AddrOfPinnedObject();
                            //CudaTools.VideoDecoder_CopyGpuToCpu(m_videoDecoder, ptr, frame1, (uint)numBytes);
                            //pinnedArray.Free();



                            // END TEMP

                            CudaTools.VideoDecoder_ReleaseFrameGPU64(m_videoDecoder);
                     
                    }

                }
            }
            return result;
        }

        public override byte[] Decode(byte[] frame, int width, int height, byte keyFlag, long pts)
        {
            byte[] result = null;

            if (width == m_width && height == m_height)
            {
                if (m_videoDecoder == IntPtr.Zero)
                {
                    Init();
                }
                if (m_videoDecoder != IntPtr.Zero)
                {
                    if (m_InputQueueSemaphore.WaitOne(5000))
                    {
                        CudaTools.VideoDecoder_NewInputFrame64(m_videoDecoder, frame, frame.Length);
                        //Debug.Print("FRAME IN");
                    }
                    if (m_OutputQueueSemaphore.WaitOne(0))
                    {
                        // Get the new frame
                        if (decodedFrame == null)
                        {
                            decodedFrame = new byte[m_width*m_height*4];
                        }
                        //Debug.Print("FRAME OUT");
                        int width1;
                        int height1;
                        int format;
                        UInt64 timeStamp;
                        IntPtr frame1;
                        int numBytes;

                        if(m_leaveFramesOnGpu)
                        {
                            CudaTools.VideoDecoder_GetNextDecodedFrameGPU64(m_videoDecoder, out frame1, out numBytes, out width1, out height1, out format, out timeStamp);

                            // TEMP - for testing purposes, copy the frame from the GPU to the CPU                           
     
                            result = new byte[numBytes];
                            GCHandle pinnedArray = GCHandle.Alloc(decodedFrame,GCHandleType.Pinned);
                            IntPtr ptr = pinnedArray.AddrOfPinnedObject();
                            CudaTools.VideoDecoder_CopyGpuToCpu(m_videoDecoder, ptr, frame1, (uint)numBytes);
                            pinnedArray.Free();

                           
                                
                            // END TEMP

                            CudaTools.VideoDecoder_ReleaseFrameGPU64(m_videoDecoder);
                        }
                        else
                        {
                            CudaTools.VideoDecoder_GetNextDecodedFrame64(m_videoDecoder, out frame1, out numBytes, out width1, out height1, out format, out timeStamp);
                            // Why the double copy?
                            Marshal.Copy(frame1, decodedFrame, 0, (int)numBytes);
                            CudaTools.VideoDecoder_ReleaseFrame(m_videoDecoder);
                            result = new byte[decodedFrame.Length];
                            Buffer.BlockCopy(decodedFrame, 0, result, 0, decodedFrame.Length);
                        }
                    }

                }
            }
            return result;
        }

        public static int GetStride(int width, PixelFormat pxFormat)
        {
            //float bitsPerPixel = System.Drawing.Image.GetPixelFormatSize(format);
            int bitsPerPixel = ((int)pxFormat >> 8) & 0xFF;
            //Number of bits used to store the image data per line (only the valid data)
            int validBitsPerLine = width * bitsPerPixel;
            //4 bytes for every int32 (32 bits)
            int stride = ((validBitsPerLine + 31) / 32) * 4;
            return stride;
        }
    }

    public class HiResTimer
    {
        private bool isPerfCounterSupported = false;
        private Int64 frequency = 0;

        // Windows CE native library with QueryPerformanceCounter().
        private const string lib = "Kernel32.dll";
        [DllImport(lib)]
        private static extern int QueryPerformanceCounter(ref Int64 count);
        [DllImport(lib)]
        private static extern int QueryPerformanceFrequency(ref Int64 frequency);

        public HiResTimer()
        {
            // Query the high-resolution timer only if it is supported.
            // A returned frequency of 1000 typically indicates that it is not
            // supported and is emulated by the OS using the same value that is
            // returned by Environment.TickCount.
            // A return value of 0 indicates that the performance counter is
            // not supported.
            int returnVal = QueryPerformanceFrequency(ref frequency);

            if (returnVal != 0 && frequency != 1000)
            {
                // The performance counter is supported.
                isPerfCounterSupported = true;
            }
            else
            {
                // The performance counter is not supported. Use
                // Environment.TickCount instead.
                frequency = 1000;
            }
        }

        public Int64 Frequency
        {
            get
            {
                return frequency;
            }
        }

        public Int64 Value
        {
            get
            {
                Int64 tickCount = 0;

                if (isPerfCounterSupported)
                {
                    // Get the value here if the counter is supported.
                    QueryPerformanceCounter(ref tickCount);
                    return tickCount;
                }
                else
                {
                    // Otherwise, use Environment.TickCount.
                    return (Int64)Environment.TickCount;
                }
            }
        }
    }


    
    internal delegate void OnNewPlaybackFrame(byte[] frame, int width, int height, DateTime frameTime, Exception exceptionState);
    internal delegate void OnNewPlaybackFrameEx(UncompressedFrame uf);
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FILE_INFO_HEADER
    {
        public UInt32 FrameWidth;
        public UInt32 FrameHeight;
        public UInt32 FrameRate;
        public UInt32 TotalFrames;
        public UInt32 VideoFormat;
        public UInt32 AveGOPSize;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DATA_FRAME_HEADER
    {
        public byte V;
        public byte A;
        public Int32 TotalLength;
        public Int32 CameraID;
        public byte StreamingMode;
        public byte KeyFlag;
        public Int16 FrameWidth;
        public Int16 FrameHeight;
        public Int16 MotionTime;
    };

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    internal struct PLAYER_FRAME
    {
	    public UInt32 headerLength;
	    public UInt64 frameTime;
	    public UInt32 fileID;
	    public UInt32 totalFramesInFile;
	    public UInt32 frameNumber;
    }

    internal class VIServerDescriptor : PeerAddress   
    {
        private string m_user;
        private string m_password;
        private Int32 m_connectionToken;

        public VIServerDescriptor(VIServerDescriptor sd)
            : base(sd.address, sd.port)
        {
            base.ipAddress = sd.ipAddress;
            m_user = sd.user;
            m_password = sd.password;
            GenerateConnectionToken();
        }

        public VIServerDescriptor(string address, short port, string user, string password)
            : base(address, port)
        {
            m_user = user;
            m_password = password;
            GenerateConnectionToken();
        }
        
        public VIServerDescriptor() : base("www.demovi.com", 4021)
        {
            m_user = "";
            m_password = "";
            GenerateConnectionToken();
        }

        private void GenerateConnectionToken()
        {
            m_connectionToken = (new Random((Int32)DateTime.Now.Ticks)).Next();
        }

        public string user
        {
            get { return m_user; }
            set
            {
                m_user = value;
                GenerateConnectionToken();
            }
        }

        public string password
        {
            get { return m_password; }
            set
            {
                m_password = value;
                GenerateConnectionToken();
            }
        }

        public Int32 connectionToken
        {
            get { return m_connectionToken; }
        }
    }

    class VIServices
    {
        public static string __Function()
        {
            StackTrace stackTrace = new StackTrace();
            return stackTrace.GetFrame(1).GetMethod().Name;
        }

        public static void __PrintCurrentThreadID()
        {
            StackTrace stackTrace = new StackTrace();
            Debug.Print(stackTrace.GetFrame(1).GetMethod().Name + " (" + Thread.CurrentThread.ManagedThreadId + ")");
        }

        public static T Deserialize<T>(Object pkt)
        {
            if(pkt is byte[])
            {
                byte[] bytes = (byte[])pkt;
                return (T)VISerialization.DeserializeObject(bytes, 0, bytes.Length);
            }
            else
            {
                throw new InvalidCastException();
            }
        }

        public static byte[] PacketGetServerVersion()
        {
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.GetServerVersion,
                                          "", "", 0, "");
        }

        public static byte[] PacketGetServerClass(VIServerDescriptor server)
        {
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.GetServerClass,
                                          server.user, server.password, server.connectionToken, "NOLOGIN");
        }

        public static byte[] PacketStartLiveStream(VIServerDescriptor server, Videoinsight.LIB.Camera[] cameras)
        {
            List<int> cams = new List<int>();
            foreach(Videoinsight.LIB.Camera c in cameras)
            {
                cams.Add(c.CameraID);
            }
            return PacketStartLiveStream(server, cams.ToArray());
        }

        public static byte[] StartPTZOperation(VIServerDescriptor server, int cameraID, string cmd, int speed)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = speed;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = "PTZAction";
            cmdHeader.StrParameter2 = cmd;
            cmdHeader.StrParameter3 = "";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.Ptz_Operation,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketGetFileInfoWithName(VIServerDescriptor server, int cameraID, string fileName)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = fileName;
            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.MediaFileStreamingHeaderInfo,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketRecallPTZPreset(VIServerDescriptor server, int cameraID, int presetID, string presetName)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = 50;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = "RecallPreset";
            cmdHeader.StrParameter2 = presetID.ToString();
            cmdHeader.StrParameter3 = presetName;
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.Ptz_Operation,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketDeletePTZPreset(VIServerDescriptor server, int cameraID, int presetID, string presetName)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = 50;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = "DeletePreset";
            cmdHeader.StrParameter2 = presetID.ToString();
            cmdHeader.StrParameter3 = presetName;
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.Ptz_Operation,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketSetPTZPreset(VIServerDescriptor server, int cameraID, int presetID, string presetName)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = 50;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = "SetPreset";
            cmdHeader.StrParameter2 = presetID.ToString();
            cmdHeader.StrParameter3 = presetName;
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.Ptz_Operation,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketGetPTZPresets(VIServerDescriptor server, int cameraID)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = "GetPresets";
            cmdHeader.StrParameter2 = "1";
            cmdHeader.StrParameter3 = "";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.Ptz_Operation,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketFileAndFrameFromTime(VIServerDescriptor server, int cameraID, DateTime dateTime)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = cameraID;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = dateTime.ToString();
            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "";

            /*
            ClientRequestHeader crh = new ClientRequestHeader();
            crh.CommandCode = (Videoinsight.LIB.VITypes.CommandCodes)50037; //Videoinsight.LIB.VITypes.CommandCodes.GetFrameIndexFromTime;
            crh.CookieID = server.connectionToken;
            crh.UID = server.user;
            crh.PWD = server.password;
            crh.Obj = cmdHeader;
            
            byte[] tmpArray = VISerialization.SoapSeserializeToArray(crh);

            string xml = Encoding.ASCII.GetString(tmpArray);
            xml = xml.Replace("GetFrameIndexFromTime", "50037");
            tmpArray = Encoding.ASCII.GetBytes(xml);

            int tmpLen = tmpArray.Length + 5;
            byte[] frameLength = BitConverter.GetBytes(tmpLen);

            byte[] retVal = new byte[tmpLen];
            retVal[0] = 6;
            Array.Copy(frameLength, 0, retVal, 1, frameLength.Length);
            Array.Copy(tmpArray, 0, retVal, 5, tmpLen - 5);

            return retVal;
            */

            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          (Videoinsight.LIB.VITypes.CommandCodes)50037,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public enum PAYLOAD_TYPE
        {
            eos,
            xml,
            h264,
            jpeg,
            mpeg4,
            file
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class STREAM_PACKET_HEADER
        {
            public STREAM_PACKET_HEADER(Int64 payloadLength = 0, PAYLOAD_TYPE payloadType = PAYLOAD_TYPE.eos, byte payloadFlags = 0)
            {
                this.payloadLength = payloadLength;
                this.payloadType = (byte)payloadType;
                this.payloadFlags = payloadFlags;
            }
            public Int64 payloadLength;
            public byte payloadType;
            public byte payloadFlags;
        }

        public class STREAM_VIDEO_HEADER
        {
            public STREAM_VIDEO_HEADER(Int64 timestamp = 0, Int64 reserved1 = 0, Int64 reserved2 = 0)
            {
                this.timestamp = timestamp;
                this.reserved1 = reserved1;
                this.reserved2 = reserved2;
            }
            public Int64 timestamp;
            public Int64 reserved1;
            public Int64 reserved2;
        }

        public static byte[] PacketGPUTranscode(VIServerDescriptor server)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = 0;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = new XElement("TranscodeSession",
                                      new XElement("Camera", new XElement("ID", "609795552")),
                                      new XElement("Search", new XElement("Begin", "2/29/2016 8:00"), new XElement("End", "3/1/2016 9:50:00 AM")),
                                      new XElement("Output", new XElement("Size", new XElement("Width", "640"),
                                      new XElement("Height", "480")),
                                      new XElement("Codec", new XAttribute("FourCC", "JPEG"), new XElement("Bitrate", "100000000"),
                                      new XElement("Quality", "80"), new XElement("KeyOnly", "false")),
                                      new XElement("Container", ""))).ToString();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(cmdHeader.StrParameter1);
            XmlNode scrubSession = xmlDoc.SelectSingleNode("//TranscodeSession");
            XmlNode size = scrubSession.SelectSingleNode("//Output/Size");
            if (size != null)
            {
                XmlNode width = size.SelectSingleNode("//Width/text()");
                if (width != null)
                {
                    
                }
                XmlNode height = size.SelectSingleNode("//Height/text()");
                if (height != null)
                {
                    
                }
                string file = size.Value;
            }

            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          (Videoinsight.LIB.VITypes.CommandCodes)52000,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

       public static byte[] PacketPlugin(VIServerDescriptor server)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = 0;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = new XElement("PluginSession", new XAttribute("Name", "VISearch64d.dll"),
                                      new XElement("Init", new XAttribute("SkipCount", "5"), new XAttribute("Sensitivity", "0.5"),
                                      new XElement("ROI", new XAttribute("x", "100"),new XAttribute("y", "100"),
                                          new XAttribute("width", "500"),new XAttribute("height", "500"))),
                                      new XElement("Camera", new XElement("ID", "555035755")),
                                      new XElement("Timeframe", new XElement("Begin", "5/17/2016 11:00 PM"), new XElement("End", "5/18/2016 6:00:00 AM"))).ToString();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(cmdHeader.StrParameter1);
            XmlNode scrubSession = xmlDoc.SelectSingleNode("//PluginSession");
            XmlNode size = scrubSession.SelectSingleNode("//Output/Size");
            if (size != null)
            {
                XmlNode width = size.SelectSingleNode("//Width/text()");
                if (width != null)
                {

                }
                XmlNode height = size.SelectSingleNode("//Height/text()");
                if (height != null)
                {

                }
                string file = size.Value;
            }

            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          (Videoinsight.LIB.VITypes.CommandCodes)1000,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketTplStream(VIServerDescriptor server)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = 0;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = new XElement("ScrubSession",
                                      new XElement("Camera", new XElement("ID", "116246428")),
                                      //new XElement("Camera", new XElement("ID", "609795552")),
                                      new XElement("Search", new XElement("Begin", "3/7/2016 7:00 AM"), new XElement("End", "3/7/2016 10:00 AM")),
                                      //new XElement("Search", new XElement("Begin", "2/29/2016 8:00"), new XElement("End", "2/29/2016 8:03")),
                                      new XElement("Output", new XElement("Size", new XElement("Width", "1920"),
                                      new XElement("Height", "1080")),
                                      new XElement("Codec", new XAttribute("FourCC", "JPEG"), new XElement("Bitrate", "100000000")),
                                      new XElement("Container", ""))).ToString();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(cmdHeader.StrParameter1);
            XmlNode scrubSession = xmlDoc.SelectSingleNode("//ScrubSession");
            XmlNode size = scrubSession.SelectSingleNode("//Output/Size");
            if (size != null)
            {
                XmlNode width = size.SelectSingleNode("//Width/text()");
                if (width != null)
                {
                    Debug.Print(width.Value);
                }
                XmlNode height = size.SelectSingleNode("//Height/text()");
                if (height != null)
                {
                    Debug.Print(height.Value);
                }
                string file = size.Value;
            }

            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          (Videoinsight.LIB.VITypes.CommandCodes)52000,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketGetPlaybackStream(VIServerDescriptor server, int fileID, int frame)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = fileID;
            cmdHeader.IntParameter2 = frame;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = "";
            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "original";
            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          (Videoinsight.LIB.VITypes.CommandCodes)50012,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static string BuildVISoapCommonHeader(string user, string pass, UInt32 cookie, UInt32 commandCode, UInt32 clicks, bool onlyCommonHeader)
        {
            // VideoInsight.Lib version 7.0.0.1?

            string soap = "<SOAP-ENV:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:SOAP-ENC=\"http://schemas.xmlsoap.org/soap/encoding/\" xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:clr=\"http://schemas.microsoft.com/soap/encoding/clr/1.0\" SOAP-ENV:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n" +
                          "<SOAP-ENV:Body>\r\n" +
                          "<a1:ClientRequestHeader id=\"ref-1\" xmlns:a1=\"http://schemas.microsoft.com/clr/nsassem/Videoinsight.LIB/Videoinsight.LIB%2C%20Version%3D4.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull\">\r\n" +
                          "<CommandCode xsi:type=\"a2:CommandCodes\" xmlns:a2=\"http://schemas.microsoft.com/clr/nsassem/Videoinsight.LIB.VITypes/Videoinsight.LIB%2C%20Version%3D4.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull\">";

            soap += commandCode.ToString();

            soap += "</CommandCode>\r\n" +
                    "<UID id=\"ref-3\">" + user + "</UID>\r\n" +
                    "<PWD id=\"ref-4\">" + pass + "</PWD>\r\n" +
                    "<CookieID>" + cookie.ToString() + "</CookieID>\r\n";

            if (onlyCommonHeader)
            {
                soap += "<Obj xsi:type=\"xsd:string\" />";
            }
            else
            {
                soap += "<m_obj href=\"#ref-5\" />";
            }

            soap += "\r\n<CTKS>" + clicks.ToString() + "</CTKS>\r\n";
            soap += "</a1:ClientRequestHeader>\r\n";
            return soap;
        }


public static string BuildVISoapCmdRequest( string user, string pass, UInt32 cookie, UInt32 commandCode, UInt32 int1, 
	                                      UInt32 int2, UInt32 int3, string str1, 
									      string str2, string str3, 
									      bool onlyCommonHeader )
{
	string soap = BuildVISoapCommonHeader( user, pass, cookie, commandCode, 0, onlyCommonHeader );


	// Data channel and command channel common to this point
    if(onlyCommonHeader==false) {	
	soap += "<a1:CommonCMDHeader id=\"ref-5\" xmlns:a1=\"http://schemas.microsoft.com/clr/nsassem/Videoinsight.LIB/Videoinsight.LIB%2C%20Version%3D4.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull\">\r\n"+
            "<IntParameter1>" + int1.ToString() + "</IntParameter1>\r\n" + 
            "<IntParameter2>" + int2.ToString() + "</IntParameter2>\r\n" + 
            "<IntParameter3>" + int3.ToString() + "</IntParameter3>\r\n";

    soap += "<StrParameter1 id=\"ref-6\"";

	if( str1.Length == 0 )
	{
		soap += " />\r\n";
	}
	else
	{
		soap += ">" + str1 + "</StrParameter1>\r\n";
	}

    soap += "<StrParameter2 id=\"ref-7\"";

	if( str2.Length == 0 )
	{
		soap += " />\r\n";
	}
	else
	{
		soap += ">" + str2 + "</StrParameter2>\r\n";
	}

    soap += "<StrParameter3";

	if( str3.Length == 0 )
	{
		soap += " />\r\n";
	}
	else
	{
		soap += ">" + str3 + "</StrParameter3>\r\n";
	}

    soap += "</a1:CommonCMDHeader>\r\n"+
            "</SOAP-ENV:Body>\r\n"+
            "</SOAP-ENV:Envelope>\r\n";

} else {

    soap += "\r\n" +
            "</SOAP-ENV:Body>\r\n" +
            "</SOAP-ENV:Envelope>\r\n";
}

    return soap;
    }

        public static byte[] PacketGetFileName(VIServerDescriptor server, int fileID)
        {

            string soap = BuildVISoapCmdRequest(server.user, server.password, 
                                               (UInt32)server.connectionToken, 
                                               50014, 
                                               0, 0, 0, 
                                               String.Format("SELECT * FROM dbo.VideoFiles WHERE fileID={0}", fileID),
                                               "", 
                                               "", 
                                               false);

            /*

            byte[] utfBytes = Encoding.UTF8.GetBytes(soap);

            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = 0;
            cmdHeader.IntParameter2 = 0;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = String.Format("SELECT * FROM dbo.VideoFiles WHERE fileID={0}", fileID);
            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = "";

            
            ClientRequestHeader crh = new ClientRequestHeader();
            crh.CommandCode = Videoinsight.LIB.VITypes.CommandCodes.MobileRequestFirst;
            crh.CookieID = server.connectionToken;
            crh.UID = server.user;
            // Server > 6.2.12.7
            //crh.UID = Crypto.EncryptString(server.user);
            crh.PWD = server.password;
            //crh.PWD = Crypto.EncryptString(server.password);
            if(cmdHeader.GetType().IsSerializable)
            {
                crh.Obj = cmdHeader;
            }
            byte[] tmpArray = VISerialization.SoapSeserializeToArray(crh);
            string xml = Encoding.ASCII.GetString(tmpArray);
            xml = xml.Replace("MobileRequestFirst", "50014");
        
            tmpArray = Encoding.ASCII.GetBytes(xml);
             */

            byte[] tmpArray = Encoding.ASCII.GetBytes(soap);

            int tmpLen = tmpArray.Length + 5;
            byte[] frameLength = BitConverter.GetBytes(tmpLen);

            byte[] retVal = new byte[tmpLen];
            retVal[0] = 6;
            Array.Copy(frameLength, 0, retVal, 1, frameLength.Length);
            Array.Copy(tmpArray, 0, retVal, 5, tmpLen - 5);

            return retVal;
            
        }

        public static byte[] PacketGetGOPWithNameAndIndexRange(VIServerDescriptor server, string fileName, int startIndex, int endIndex, bool keyOnly = true)
        {
            Videoinsight.LIB.CommonCMDHeader cmdHeader = new CommonCMDHeader();
            cmdHeader.IntParameter1 = startIndex;
            cmdHeader.IntParameter2 = endIndex;
            cmdHeader.IntParameter3 = 0;
            cmdHeader.StrParameter1 = fileName;
            cmdHeader.StrParameter2 = "";
            cmdHeader.StrParameter3 = keyOnly ? "key" : "";


            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.MediaFileStreaming,
                                          server.user, server.password, server.connectionToken, cmdHeader);
        }

        public static byte[] PacketStartLiveStream(VIServerDescriptor server, int[] cameras)
        {
            DataRequestHeader drh = new DataRequestHeader();
            drh.StreamingMode = Videoinsight.LIB.VITypes.StreamingMode.Automatic;
            drh.CameraList = cameras.ToArray();

            return VI.SetRequestHeaderObj(Videoinsight.LIB.VITypes.ClientType.NetworkClient,
                                          Videoinsight.LIB.VITypes.CommandCodes.DataStreaming,
                                          server.user, server.password, server.connectionToken, drh);

        }

        public static AsyncRequestObject RunLiveStream(Socket socket, OnNewLiveFrame NewLiveFrameCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            VIServices.__PrintCurrentThreadID();
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            ts = ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts;
            aro.task = Task.Factory.StartNew(() =>
            {
                VIServices.__PrintCurrentThreadID();
                byte[] bytes = null;
                Dictionary<int, DecodingSession> decodeCollection;

                // 
                bool useCuda = true;
                bool leaveFramesOnGpu = true;

                PixelFormat pf;
                if (useCuda)
                {
                    pf = PixelFormat.Format32bppRgb;
                }
                else
                {
                    pf = PixelFormat.Format24bppRgb;
                }
                decodeCollection = new Dictionary<int, DecodingSession>();



                //CudaTranscoder ct = new CudaTranscoder(0, 0);
                //ct.InitSink();
                //UInt32 frames = 0;

                //FileStream fs = new FileStream("TestVideo.H264", System.IO.FileMode.Create);

                HiResTimer t = new HiResTimer();

                Int64 t1 = t.Value;
                Int64 frames = 0;
                    
                while (!aro.CancellationToken.WaitHandle.WaitOne(0))
                {
                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, Marshal.SizeOf(typeof(DATA_FRAME_HEADER)));
                    if (bytes != null && bytes.Length!=0)
                    {
                        // Full header received
                        GCHandle pinnedPacket = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        DATA_FRAME_HEADER dfh = (DATA_FRAME_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(DATA_FRAME_HEADER));
                        pinnedPacket.Free();
                        
                        DecodingSession decodingSession;


                        
                        // Get frame
                        bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, dfh.TotalLength - Marshal.SizeOf(typeof(DATA_FRAME_HEADER)));
                        if (bytes != null && bytes.Length != 0)
                        {
                            // SUBTRACT THE VZ AT THE END?
                            try
                            {
                                //fs.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                                //fs.Write(bytes, 0, bytes.Length);


                                /*
                                if (frames < 100)
                                {
                                    ct.NewInputFrame(bytes, bytes.Length);
                                    ++frames;
                                    if(frames==100)
                                    {
                                        ct.Stop();
                                    }
                                }
                                */
                                if (!decodeCollection.ContainsKey(dfh.CameraID))
                                {
                                    bool initialized = false;
                                    if (!useCuda)
                                    {

                                        IPPDecodingSession.CODEC codec = IPPDecodingSession.CODEC.NONE;
                                        switch (dfh.StreamingMode)
                                        {
                                            case 2:
                                                codec = IPPDecodingSession.CODEC.MJPEG;
                                                break;
                                            case 5:
                                            case 7:
                                                codec = IPPDecodingSession.CODEC.MPEG4;
                                                break;
                                            case 9:
                                                codec = IPPDecodingSession.CODEC.H264;
                                                break;
                                            default:
                                                throw new Exception("Unsupported codec");
                                        }
                                        decodingSession = new IPPDecodingSession(dfh.CameraID, dfh.FrameWidth, dfh.FrameHeight, codec);
                                        initialized = ((IPPDecodingSession)decodingSession).Init(bytes);
                                    }
                                    else
                                    {                                        
                                        decodingSession = new CudaDecodingSession(dfh.CameraID, dfh.FrameWidth, dfh.FrameHeight, dfh.StreamingMode, leaveFramesOnGpu);
                                        initialized = ((CudaDecodingSession)decodingSession).Init();
                                    }
                                    if (initialized)
                                    {
                                        decodeCollection.Add(dfh.CameraID, decodingSession);
                                    }
                                }
                                else
                                {
                                    decodingSession = decodeCollection[dfh.CameraID];
                                }
                                byte[] decodedFrame = decodingSession.Decode(bytes, dfh.FrameWidth, dfh.FrameHeight, dfh.KeyFlag);
                                if (decodedFrame != null)
                                {
                                    Task.Factory.StartNew(() =>
                                    {
                                        if (NewLiveFrameCallback != null)
                                        {
                                            NewLiveFrameCallback(decodedFrame, IntPtr.Zero, dfh.FrameWidth, dfh.FrameHeight, dfh.CameraID, pf, null);
                                        }
                                    }, CancellationToken.None, TaskCreationOptions.None, ts);
                                }
                                
                            }
                            catch(Exception e)
                            {
                                Task.Factory.StartNew(() =>
                                {
                                    if (NewLiveFrameCallback != null)
                                    {
                                        NewLiveFrameCallback(null, IntPtr.Zero, 0, 0, 0, PixelFormat.DontCare, e);
                                    }
                                }, CancellationToken.None, TaskCreationOptions.None, ts);
                            }
                        }
                        else
                            break;
                    }
                    else
                    {
                        // Server closed connection?
                        break;
                    }

                    ++frames;
                    if(frames % 100 == 0)
                    {
                        Int64 timeTicks = t.Value - t1;
                        Int64 timeElapseInSeconds =
                         timeTicks / t.Frequency;
                        Debug.Print("Frame rate = {0}", (float)frames / (float)timeElapseInSeconds);

                    }
                }

                //fs.Close();

            }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return aro;
        }

        public static UInt32 CalcInterframeTime(Int32 frameRate, float speed)
        {
            float interFrameTime = (float)1.0 / (float)frameRate;
            interFrameTime /= speed;
            interFrameTime *= 1000;
            UInt32 sleepTime = (UInt32)interFrameTime;
            return sleepTime;
        }
        /*
        public static async void GetDisplayFrames(BufferBlock<UncompressedFrame> buffer, CancellationToken ct, OnNewPlaybackFrame NewPlaybackFrameCallback)
        {
            // UI THREAD
            UInt32 frameRate = 0;
            UInt32 interframeTime = 0;
            while (true)
            {
                UncompressedFrame uf = await buffer.ReceiveAsync();
                if(frameRate!=uf.FrameRate)
                {
                    interframeTime = CalcInterframeTime((Int32)uf.FrameRate, 1.0F);
                    frameRate = uf.FrameRate;
                }
                await Task.Delay((int)interframeTime);
                if (NewPlaybackFrameCallback != null)
                {
                    NewPlaybackFrameCallback(uf.Frame, (int)uf.FrameWidth, (int)uf.FrameHeight, uf.FrameTime, null);
                }
            }
        }
        */

        // VideoFile
        // FILE_INFO_HEADER
        // FrameDescriptor(s)
        // PLAYER_FRAME
        // frame[]
        // PLAYER_FRAME
        // <currentEOF>


        public class SharedMemory<T> where T : struct
        {
            // Constructor
            public SharedMemory(string name, int items)
            {
                smName = name;
                smItems = items;
            }

            // Methods
            public bool Open(bool throwExceptions = true)
            {
                try
                {
                    // Create named MMF
                    mmf = MemoryMappedFile.CreateOrOpen(smName, smItems * Marshal.SizeOf(typeof(T)));

                    // Create accessors to MMF
                    accessor = mmf.CreateViewAccessor(0, smItems * Marshal.SizeOf(typeof(T)),
                                   MemoryMappedFileAccess.ReadWrite);

                    // Create lock
                    smLock = new Mutex(true, smName+"_MUTEX", out locked);
                    if(locked)
                    {
                        smLock.ReleaseMutex();
                    }
                }
                catch(Exception ex)
                {
                    if(throwExceptions)
                    {
                        throw ex;
                    }
                    return false;
                }

                return true;
            }

            ~SharedMemory()
            {
                Close();
            }

            public void Close()
            {
                accessor.Dispose();
                mmf.Dispose();
                smLock.Close();
            }

            public T this[int index] 
            {
                get
                {
                    T dataStruct;
                    if (index >= 0 && index < smItems)
                    {
                        smLock.WaitOne();
                        accessor.Read<T>(index * Marshal.SizeOf(typeof(T)), out dataStruct);
                        smLock.ReleaseMutex();
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                    return dataStruct;
                }
                set
                {
                    if (index >= 0 && index < smItems)
                    {
                        smLock.WaitOne();
                        accessor.Write<T>(index * Marshal.SizeOf(typeof(T)), ref value);
                        smLock.ReleaseMutex();
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                }
            }

            // Data
            private string smName;
            private Mutex smLock;
            private int smItems;
            private bool locked;
            private MemoryMappedFile mmf;
            private MemoryMappedViewAccessor accessor;
        }

        public class SharedMemoryVD
        {
            // Constructor
            public SharedMemoryVD(string name, int items)
            {
                smName = name;
                smItems = items;
            }

            // Methods
            public bool Open(bool throwExceptions = true)
            {
                try
                {
                    // Create named MMF
                    mmf = MemoryMappedFile.CreateOrOpen(smName, smItems * Marshal.SizeOf(typeof(VideoFileDescriptor)));

                    // Create accessors to MMF
                    accessor = mmf.CreateViewAccessor(0, smItems * Marshal.SizeOf(typeof(VideoFileDescriptor)),
                                   MemoryMappedFileAccess.ReadWrite);

                    // Create lock
                    smLock = new Mutex(true, smName + "_MUTEX", out locked);
                    if (locked)
                    {
                        smLock.ReleaseMutex();
                    }
                }
                catch (Exception ex)
                {
                    if (throwExceptions)
                    {
                        throw ex;
                    }
                    return false;
                }

                return true;
            }

            ~SharedMemoryVD()
            {

            }

            public void Close()
            {
                accessor.Dispose();
                mmf.Dispose();
                smLock.Close();
            }

            public VideoFileDescriptor this[int index]
            {
                get
                {
                    VideoFileDescriptor dataStruct;
                    if (index >= 0 && index < smItems)
                    {
                        smLock.WaitOne();
                        accessor.Read<VideoFileDescriptor>(index * Marshal.SizeOf(typeof(VideoFileDescriptor)), out dataStruct);
                        smLock.ReleaseMutex();
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                    return dataStruct;
                }
                set
                {
                    if (index >= 0 && index < smItems)
                    {
                        smLock.WaitOne();
                        accessor.Write<VideoFileDescriptor>(index * Marshal.SizeOf(typeof(VideoFileDescriptor)), ref value);
                        smLock.ReleaseMutex();
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                }
            }

            // Data
            private string smName;
            private Mutex smLock;
            private int smItems;
            private bool locked;
            private MemoryMappedFile mmf;
            private MemoryMappedViewAccessor accessor;
        }




        //Causes issues...
        //[StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct VideoFileDescriptor
        {
            public Guid FileName;
            public FILE_INFO_HEADER fih;
            public int FileID;
            public int ServerID;
            public int CameraID;
            public int FirstFrame;
            public int LastFrame;
            public int TotalFrames;
            public DateTime StartTime;
            public DateTime EndTime;
            public int downloadInProgress;
            public int containsLastFrameInVideo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct FrameDescriptor
        {
            public UInt32 timestampBias;
            public byte isKey;
            public UInt32 offset;
            public int length;
            public UInt32 segmentFrameNumber;
        }

        public class RawVideoFrame
        {
            public UInt32 frameNumber;
            public int fileFrameNum;
            public DateTime timeStamp;
            public byte[] frame;
        }

        public class GOP
        {
            public FILE_INFO_HEADER fih;
            public int Key = -1;
            public List<RawVideoFrame> frames = new List<RawVideoFrame>();
        }



        // Timestamps not downloaded
        // VideoFileDescriptor.FileName == Guid.Empty
        // No frames yet
        // FirstFrame == -1
        // Last frame not written
        // LastFrame == -1 

        public class VideoFileReader
        {
            public VideoFileReader(SharedMemory<VideoFileDescriptor> vfds)
            {
                m_vfds = vfds;
                m_vfds.Open();
            }
            private SharedMemory<VideoFileDescriptor> m_vfds;
            // Current index into the video file descriptors
            private int m_fileIndex = 0;
            // Current frame number in a file
            private int m_frame = -1;
            private int m_currentFrame = -1;
            // Current video file descriptor
            VideoFileDescriptor m_vfd;
            MemoryMappedFile m_mmf = null;
            MemoryMappedViewAccessor m_va = null;
            FileStream m_fs = null;
            bool eof = false;

            public enum FrameState
            {
                empty,
                valid,
                bof,
                eof,
            };

            bool IndexFileValid(int index, bool mountIfValid)
            {
                bool retVal = false;
                VideoFileDescriptor vfd = m_vfds[index];
                MemoryMappedFile mmf = null;
                MemoryMappedViewAccessor va = null;
                if (vfd.FileName != Guid.Empty)
                {
                    // Timestamp (FrameDescriptor) file name
                    string currentTsFile = Path.Combine(new string[] { Path.GetTempPath(), vfd.FileName.ToString() + ".ts" });

                    // Create memory mapping for timestamp file
                    FileStream fs = null;
                    try
                    {
                        // It should exist if the Guid does
                        fs = new FileStream(currentTsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, new MemoryMappedFileSecurity(), HandleInheritability.Inheritable, false);
                    }
                    finally
                    {
                        if (fs != null)
                        {
                            fs.Dispose();
                        }
                    }

                    // Create view accessor for timestamps
                    va = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

                    if (mountIfValid)
                    {

                        if (m_va != null)
                        {
                            m_va.Dispose();
                        }
                        if (m_mmf != null)
                        {
                            m_mmf.Dispose();
                        }
                        if (m_fs != null)
                        {
                            m_fs.Dispose();
                            m_fs = null;
                        }

                        m_vfd = vfd;
                        m_mmf = mmf;
                        m_va = va;
                        m_fileIndex = index;
                        m_frame = m_vfd.FirstFrame;
                        if(m_frame!=-1)
                        {
                            FrameDescriptor fd = ReadFrameDescriptor(m_frame);
                            m_currentFrame = (int)fd.segmentFrameNumber;
                        }
                    }
                }
                return retVal;
            }

            bool MountIndexFile(int index)
            {
                return IndexFileValid(index, true);
            }

            bool MountFramesFile()
            {
                bool retVal = false;

                if (m_va != null)
                {
                    if (m_fs != null)
                    {
                        m_fs.Dispose();
                        m_fs = null;
                    }

                    string currentFramesFile = Path.Combine(new string[] { Path.GetTempPath(), m_vfd.FileName.ToString() + ".vd" });

                    // This may not exist yet.  Wait...
                    // May need to look at scenerio where streamer thread
                    // writes timestamp file then dies before any frames written
                    if (File.Exists(currentFramesFile))
                    {
                        m_fs = new FileStream(currentFramesFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    }
                    retVal = m_fs != null;
                }
                return retVal;
            }

            bool EnsureFiles()
            {
                bool retVal = false;
                if (m_fs == null)
                {
                    if (MountIndexFile(m_fileIndex))
                    {
                        if (MountFramesFile())
                        {
                            return true;
                        }
                    }
                }
                return retVal;
            }

            FrameDescriptor ReadFrameDescriptor(int frame)
            {
                FrameDescriptor fd = new FrameDescriptor();
                if (m_va != null)
                {
                    m_va.Read<FrameDescriptor>(frame * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                }
                return fd;
            }


            FrameState ReadNextFrame(out RawVideoFrame rvf)
            {
                FrameState fs = FrameState.empty;
                rvf = null;
                if (EnsureFiles())
                {
                    if (m_currentFrame == -1)
                    {
                        // Special case - not initialized
                        m_currentFrame = 0;
                        m_vfd = m_vfds[m_fileIndex];
                        m_frame = m_vfd.FirstFrame;
                    }
                    else
                    {
                        ++m_frame;
                        ++m_currentFrame;
                    }
                    if (m_frame == m_vfd.TotalFrames)
                    {
                        // end of segment file
                        ++m_fileIndex;
                        if (IndexFileValid(m_fileIndex, true))
                        {
                            m_frame = -1;
                            --m_currentFrame;
                            fs = ReadNextFrame(out rvf);
                        }
                        else
                        {
                            --m_fileIndex;
                        }
                    }
                    else
                    {
                        FrameDescriptor fd = ReadFrameDescriptor(m_frame);
                        if(fd.length != 0)
                        {

                        }
                    }
                }
                return fs;
            }


            FrameState ReadFrame(out RawVideoFrame rvf)
            {
                FrameState fs = FrameState.empty;
                rvf = null;
                FrameDescriptor fd = ReadFrameDescriptor(m_frame);
                if (fd.length != 0)
                {
                    if (fd.length == -1)
                    {
                        fs = FrameState.eof;
                        eof = true;
                    }
                    else
                    {
                        rvf = new RawVideoFrame();
                        rvf.frame = new byte[fd.length];
                        rvf.fileFrameNum = m_frame;
                        rvf.timeStamp = DateTime.FromBinary(m_vfd.StartTime.ToBinary() + ((long)(fd.timestampBias) * 10000));
                        rvf.frameNumber = fd.segmentFrameNumber;

                        m_fs.Seek((long)fd.offset, SeekOrigin.Begin);
                        int bytesRead;
                        if ((bytesRead = m_fs.Read(rvf.frame, 0, (int)fd.length)) == fd.length)
                        {
                            // NEW FRAME
                            fs = FrameState.valid;
                        }
                        else
                        {
                            throw new Exception("File read error");
                        }
                    }
                }
                else
                {
                    fs = FrameState.empty;
                }
                return fs;
            }

            GOP GetNextGOP()
            {
                GOP gop = null;
                if(EnsureFiles())
                {
                    while (true)
                    {
                        RawVideoFrame rvf;
                        FrameState fs = ReadFrame(out rvf);
                        if (fs == FrameState.valid)
                        {
                            ++m_frame;
                            if(m_frame == m_vfd.TotalFrames)
                            {
                                // end of segment file (and GOP)
                            }
                        }
                    }
                }
                return gop;
            }

            GOP GetPreviousGOP()
            {
                if (EnsureFiles())
                {

                }
                return null;
            }

            GOP GetGOPContaining(int frameNumber)
            {
               return null;
            }
        }

        static void FreeFramesFile(ref FileStream fsf)
        {
            if (fsf != null)
            {
                fsf.Dispose();
                fsf = null;
            }
        }

        static bool BindFramesFile(VideoFileDescriptor vfd, ref FileStream fsf)
        {
            FreeFramesFile(ref fsf);

            string currentFramesFile = Path.Combine(new string[] { Path.GetTempPath(), vfd.FileName.ToString() + ".vd" });

            int spinCount = 0;

            while (spinCount < 2)
            {
                // This may not exist yet.  Wait...
                // May need to look at scenerio where streamer thread
                // writes timestamp file then dies before any frames written
                if (File.Exists(currentFramesFile))
                {
                    fsf = new FileStream(currentFramesFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    break;
                }
                Thread.Sleep(1000);
                ++spinCount;
            }
            return fsf != null;
        }

        static void FreeTimestampFile(ref MemoryMappedFile mmf, ref MemoryMappedViewAccessor va)
        {
            if (mmf != null)
            {
                mmf.Dispose();
                mmf = null;
            }
            if (va != null)
            {
                va.Dispose();
                va = null;
            }
        }

        static bool BindTimestampFile(SharedMemoryVD vfds, int maxSize, int index, ref MemoryMappedFile mmf, ref MemoryMappedViewAccessor va, ref VideoFileDescriptor vfd)
        {
            bool retVal = false;

            FreeTimestampFile(ref mmf, ref va);

            // Sanity check
            if(index > -1 && index < maxSize)
            {
                vfd = vfds[index];
                if (vfd.FileName != Guid.Empty)
                {
                    // Timestamp (FrameDescriptor) file name
                    string currentTsFile = Path.Combine(new string[] { Path.GetTempPath(), vfd.FileName.ToString() + ".ts" });

                    // Create memory mapping for timestamp file
                    FileStream fs = null;
                    try
                    {
                        // It should exist if the Guid does
                        fs = new FileStream(currentTsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, new MemoryMappedFileSecurity(), HandleInheritability.Inheritable, false);
                    }
                    finally
                    {
                        if (fs != null)
                        {
                            fs.Dispose();
                        }
                    }

                    // Create view accessor for timestamps
                    va = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                    retVal = true;
                }
            }
            return retVal;
        }

        static bool FindFrame(SharedMemoryVD vfds, int maxSize, int frameIWant, out int frameInFile, out int keyFrameNumberInFile, out int endGopFrameNumberInFile)
        {
            frameInFile = -1;
            keyFrameNumberInFile = -1;
            endGopFrameNumberInFile = -1;


            // if frameIWant < 0, roll to last frame
            // if frameIWant > ending frame, roll to frame 0

            bool lastFile = true;

            for (int idx = maxSize - 1; idx > -1; idx--)
            {
                MemoryMappedFile mmfTmp = null;
                MemoryMappedViewAccessor vaTmp = null;
                VideoFileDescriptor vfd = new VideoFileDescriptor();
                if (BindTimestampFile(vfds, maxSize, idx, ref mmfTmp, ref vaTmp, ref vfd))
                {
                    if(lastFile)
                    {
                        // The last available frame for bounds checking
                        FrameDescriptor fd;
                        vaTmp.Read<FrameDescriptor>(vfd.LastFrame * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                        // Special checks for last file
                        if(frameIWant < 0)
                        {
                            // Roll to end of file
                            frameIWant = (int)fd.segmentFrameNumber;
                        } else if(frameIWant > fd.segmentFrameNumber)
                        {
                            // Roll over to the beginning
                            frameIWant = 0;
                        }
                        lastFile = false;
                    }


                    int lastFrame = vfd.LastFrame;
                    if (lastFrame != -1)
                    {
                        FrameDescriptor fd;

                        vaTmp.Read<FrameDescriptor>(lastFrame * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                        if (frameIWant <= fd.segmentFrameNumber)
                        {
                            frameInFile = idx;
                            // Frame number in the file
                            keyFrameNumberInFile = vfd.LastFrame - ((int)fd.segmentFrameNumber - (int)frameIWant);
                            // This should always work!  Never try to seek to a frame that has
                            // never been decoded or it could be trouble.
                            while (true)
                            {
                                // Find key frame
                                vaTmp.Read<FrameDescriptor>(keyFrameNumberInFile * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                                if (fd.isKey == 1)
                                {
                                    break;
                                }
                                --keyFrameNumberInFile;
                                if (keyFrameNumberInFile < 0)
                                {
                                    // Huh?
                                    frameInFile = -1;
                                    keyFrameNumberInFile = -1;
                                    break;
                                }
                            }
                            if (keyFrameNumberInFile != -1)
                            {
                                endGopFrameNumberInFile = keyFrameNumberInFile;
                                int f = keyFrameNumberInFile;
                                // Now find end of GOP
                                while (++f <= vfd.LastFrame)
                                {
                                    vaTmp.Read<FrameDescriptor>(f * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                                    if (fd.isKey == 1)
                                    {
                                        break;
                                    }
                                    endGopFrameNumberInFile = f;
                                }
                            }
                            break;
                        }
                    }
                }
            }


            /*

            for (int idx = 0; idx < maxSize; idx++)
            {
                MemoryMappedFile mmfTmp = null;
                MemoryMappedViewAccessor vaTmp = null;
                VideoFileDescriptor vfd = new VideoFileDescriptor();
                if(BindTimestampFile(vfds, maxSize, idx, ref mmfTmp, ref vaTmp, ref vfd))
                {
                    int lastFrame = vfd.LastFrame;
                    if (lastFrame != -1)
                    {
                        FrameDescriptor fd;

                        vaTmp.Read<FrameDescriptor>(lastFrame * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                        if (frameIWant <= fd.segmentFrameNumber)
                        {
                            frameInFile = idx;
                            // Frame number in the file
                            keyFrameNumberInFile = vfd.LastFrame - ((int)fd.segmentFrameNumber - (int)frameIWant);
                            // This should always work!  Never try to seek to a frame that has
                            // never been decoded or it could be trouble.
                            while (true)
                            {
                                // Find key frame
                                vaTmp.Read<FrameDescriptor>(keyFrameNumberInFile * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                                if (fd.isKey == 1)
                                {
                                    break;
                                }
                                --keyFrameNumberInFile;
                                if (keyFrameNumberInFile < 0)
                                {
                                    // Huh?
                                    frameInFile = -1;
                                    keyFrameNumberInFile = -1;
                                    break;
                                }
                            }
                            if (keyFrameNumberInFile != -1)
                            {
                                endGopFrameNumberInFile = keyFrameNumberInFile;
                                int f = keyFrameNumberInFile;
                                // Now find end of GOP
                                while (++f <= vfd.LastFrame)
                                {
                                    vaTmp.Read<FrameDescriptor>(f * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                                    if(fd.isKey == 1)
                                    {
                                        break;
                                    }
                                    endGopFrameNumberInFile = f;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            */
            return frameInFile != -1 && keyFrameNumberInFile != -1 && endGopFrameNumberInFile != -1;
        }

        delegate int ProcessGOP(GOP gop);

        static void InBandConsoleMsg(BlockingCollection<CompressedFrame> queueCompressed, string msg, CancellationToken ct)
        {
            // 4 = Console msg in "frame"
            byte[] data = Encoding.Unicode.GetBytes(msg);
            InBandSignal(queueCompressed, 4, data, ct);
        }

        static void InBandSignal(BlockingCollection<CompressedFrame> queueCompressed, uint signal, byte[] data, CancellationToken ct)
        {
            // 0 = End of GOP
            // 1 = BOF
            // 2 = EOF
            // 3 = Flush pipeline
            // 4 = Console msg in "frame"
            // All invalid frame widths
            PLAYER_FRAME pfTmp = new PLAYER_FRAME();
            queueCompressed.Add(new CompressedFrame(pfTmp, signal, 0, 0, 0, 0, 0, data, false), ct);
        }

        static void InBandConsoleMsg(BlockingCollection<UncompressedFrame> queueUncompressed, string msg, CancellationToken ct)
        {
            // 1 = BOF
            // 2 = EOF
            // 4 = Console msg in "frame"
            byte[] data = Encoding.Unicode.GetBytes(msg);
            InBandSignal(queueUncompressed, 4, data, ct);
        }

        static void InBandSignal(BlockingCollection<UncompressedFrame> queueUncompressed, uint signal, byte[] data, CancellationToken ct)
        {
            // 0 = Pipeline flushed
            // 4 = Console msg in "frame"
            queueUncompressed.Add(new UncompressedFrame(0, data, signal, 0, DateTime.MinValue, 0, 0), ct);
        }

        static void MarkFileDownloadProgress(SharedMemoryVD vfds, int fileIndex, int flag)
        {
            if (fileIndex > -1)
            {
                VideoFileDescriptor vfd = vfds[fileIndex];
                vfd.downloadInProgress = flag;
                vfds[fileIndex] = vfd;
            }
        }

        static void CloseVideoFiles(ref MemoryMappedFile mmf, ref MemoryMappedViewAccessor va, ref FileStream frames)
        {
            if (mmf != null)
            {
                mmf.Dispose();
                mmf = null;
            }

            if (va != null)
            {
                va.Dispose();
                va = null;
            }

            if (frames != null)
            {
                frames.Flush();
                frames.Dispose();
                frames = null;
            }

        }


        static void CloseVideoFiles(ref MemoryMappedFile mmf, ref MemoryMappedViewAccessor va, SharedMemoryVD vfds, int fileIndex, ref FileStream frames)
        {
            if (mmf != null)
            {
                mmf.Dispose();
                mmf = null;
            }

            if (va != null)
            {
                va.Dispose();
                va = null;
            }

            if (frames != null)
            {
                // Mark as complete
                MarkFileDownloadProgress(vfds, fileIndex, 0);
                frames.Flush();
                frames.Dispose();
                frames = null;
            }

        }

        enum ExitResult
        {
            unknown,
            badFile,
            reachedMaxTime,
            socketReceiveError,
            eof,
        };


        public static void RunPlaybackPipelineEx(BlockingCollection<StreamAction> streamActions, VIServerDescriptor Server, int cameraID, Socket socket, DateTime stop, OnNewPlaybackFrameEx NewPlaybackFrameCallbackEx, out BlockingCollection<UncompressedFrame> pQueue, out AsyncRequestObject streamingTask, out AsyncRequestObject decodingTask, out BlockingCollection<StreamAction> saFileReader, out BlockingCollection<StreamAction> saDecoder)
        {
            BlockingCollection<CompressedFrame> queueCompressed = new BlockingCollection<CompressedFrame>(50);
            BlockingCollection<UncompressedFrame> queueUncompressed = new BlockingCollection<UncompressedFrame>(30);

            BlockingCollection<StreamAction> sa_FileReader = new BlockingCollection<StreamAction>();
            saFileReader = sa_FileReader;
            BlockingCollection<StreamAction> sa_Decoder = new BlockingCollection<StreamAction>();
            saDecoder = sa_Decoder;

            // Left these out on this machine...
            //Guid FileDescriptors = Guid.Parse("331dbe14-1812-4705-9f60-2ed3ec2cee78");

            Guid FileDescriptors = Guid.NewGuid();

            const int MAX_FILES = 100;

            #region Streaming task

            streamingTask = new AsyncRequestObject();
            CancellationToken ctStream = streamingTask.CancellationToken;

            // *******************************************************************
            //   Streams video frames from the server        
            // *******************************************************************
            Task sTask = Task.Factory.StartNew(() =>
            {
                const int rTimeout = 30000;


                int fileIndex = -1;
                int totalFrames = 0;
                SharedMemoryVD vfds = new SharedMemoryVD(FileDescriptors.ToString(), MAX_FILES);
                if (!vfds.Open())
                {
                    // Huh?
                    return;
                }

                MemoryMappedViewAccessor va = null;
                MemoryMappedFile mmf = null;
                FileStream frames = null;


                ExitResult exitResult = ExitResult.unknown;

                Debug.Print("Streaming Task {0} started", Thread.CurrentThread.ManagedThreadId);
                try
                {
                    UInt32 sessionFrameNumber = 0;

                    byte[] bytes = null;
                    bool bExit = false;
                    bool bFirstFrame = true;
                    long firstFrameTime;
                    UInt32 currentFileID = 0;
                    FILE_INFO_HEADER fih;
                    fih.AveGOPSize = fih.FrameHeight = fih.FrameRate = fih.FrameWidth = fih.TotalFrames = fih.VideoFormat = 0;

                    Guid currentFileGuid = Guid.Empty;

                    while (!ctStream.WaitHandle.WaitOne(0) && !bExit)
                    {

                        #region Main Loop

                        // Receive the packet type or length
                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, rTimeout, Marshal.SizeOf(typeof(UInt32)));
                        if (bytes != null && bytes.Length == Marshal.SizeOf(typeof(UInt32)))
                        {
                            UInt32 pktType = BitConverter.ToUInt32(bytes, 0);

                            switch (pktType)
                            {
                                case 0:
                                    // EOF (Video)
                                    Debug.Print("File downloading stopped - EOF");
                                    exitResult = ExitResult.eof;
                                    bExit = true;
                                    break;

                                case 1:
                                    // Error opening/reading video file - fatal
                                    Debug.Print("File downloading stopped - Error opening file");
                                    exitResult = ExitResult.badFile;
                                    bExit = true;
                                    break;

                                case 2:
                                    // Timestamps - beginning of a new file
                                    #region Receive Timestamps

                                    // Timestamps for file
                                    {
                                        CloseVideoFiles(ref mmf, ref va, vfds, fileIndex, ref frames);

                                        // Receive the length of the timestamps
                                        UInt32 timestampsSize;
                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, rTimeout, 4);
                                        if (bytes != null && bytes.Length == 4)
                                        {
                                            timestampsSize = BitConverter.ToUInt32(bytes, 0);
                                        }
                                        else
                                        {
                                            Debug.Print("Error receiving timestamps size");
                                            exitResult = ExitResult.socketReceiveError;
                                            bExit = true;
                                            break;
                                        }

                                        // Receive the first timestamp - 8 bytes
                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, rTimeout, 8);
                                        if (bytes != null && bytes.Length == 8)
                                        {
                                            firstFrameTime = BitConverter.ToInt64(bytes, 0);
                                        }
                                        else
                                        {
                                            Debug.Print("Error receiving timestamps epoch");
                                            exitResult = ExitResult.socketReceiveError;
                                            bExit = true;
                                            break;
                                        }

                                        // Receive the remaining timestamps
                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, rTimeout, (int)timestampsSize - 8);
                                        if (bytes != null && bytes.Length == timestampsSize - 8)
                                        {
                                            long frameCount = (((timestampsSize - 8) / 4) + 1);

                                            currentFileGuid = Guid.NewGuid();

                                            string currentTsFile = Path.Combine(new string[] { Path.GetTempPath(), currentFileGuid.ToString() + ".ts" });

                                            MemoryMappedFileSecurity security = new MemoryMappedFileSecurity();
                                            SecurityIdentifier everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                                            security.AddAccessRule(new AccessRule<MemoryMappedFileRights>(everyoneSid, MemoryMappedFileRights.FullControl, AccessControlType.Allow));

                                            FileStream fs = new FileStream(currentTsFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                                            fs.SetLength(frameCount * Marshal.SizeOf(typeof(FrameDescriptor)));
                                            mmf = MemoryMappedFile.CreateFromFile(fs, null, frameCount * Marshal.SizeOf(typeof(FrameDescriptor)), MemoryMappedFileAccess.ReadWrite, security, HandleInheritability.Inheritable, false);
                                            fs.Dispose();

                                            va = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                                            // Write the first frame time
                                            FrameDescriptor fd;
                                            fd.timestampBias = 0;
                                            fd.isKey = 0;
                                            fd.offset = 0;
                                            fd.length = 0;
                                            fd.segmentFrameNumber = UInt32.MaxValue;

                                            long currentEOF = 0;

                                            va.Write<FrameDescriptor>(currentEOF, ref fd);

                                            int Idx = 0;
                                            while(Idx < bytes.Length)
                                            {
                                                currentEOF += Marshal.SizeOf(fd);
                                                fd.timestampBias = BitConverter.ToUInt32(bytes, Idx);
                                                fd.segmentFrameNumber = UInt32.MaxValue;
                                                va.Write<FrameDescriptor>(currentEOF, ref fd);
                                                Idx += 4;
                                            }

                                            ++fileIndex;

                                            VideoFileDescriptor vfd = vfds[fileIndex];
                                            vfd.FileName = currentFileGuid;
                                            vfd.FirstFrame = -1;
                                            vfd.LastFrame = -1;
                                            vfd.downloadInProgress = 1;
                                            vfds[fileIndex] = vfd;

                                            Debug.Print("File Downloading New File - {0} ", currentFileGuid);

                                        }
                                        else
                                        {
                                            Debug.Print("Error receiving timestamps");
                                            exitResult = ExitResult.socketReceiveError;
                                            bExit = true;
                                            break;
                                        }

                                    }
                                    break;

                                    #endregion

                                default:
                                    // Receive a frame
                                    #region Receive a frame

                                    if (bFirstFrame)
                                    {
                                        // SetInterframeTime
                                        UInt32 cmd = 4;
                                        Int32 val = 1; // 1 ms (really needs to be 0 but requires a server change)
                                        byte[] cmd1 = BitConverter.GetBytes(cmd);
                                        IPServices.TransmitOnSocket(socket, cmd1, ctStream);
                                        byte[] val1 = BitConverter.GetBytes(val);
                                        IPServices.TransmitOnSocket(socket, val1, ctStream);
                                        bFirstFrame = false;
                                    }

                                    // Receive the PLAYER_FRAME header
                                    bytes = IPServices.ReceiveOnSocket(socket, ctStream, rTimeout, Marshal.SizeOf(typeof(PLAYER_FRAME)));
                                    if (bytes != null && bytes.Length == Marshal.SizeOf(typeof(PLAYER_FRAME)))
                                    {
                                        // Full header received
                                        GCHandle pinnedPacket = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                                        PLAYER_FRAME pf = (PLAYER_FRAME)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(PLAYER_FRAME));
                                        pinnedPacket.Free();

                                        DateTime t = DateTime.FromBinary((long)pf.frameTime);
                                        if(t.AddTicks( - (t.Ticks % TimeSpan.TicksPerSecond))  > stop)
                                        {
                                            if (totalFrames == 0)
                                            {
                                                stop = t.AddMinutes(5);
                                            }
                                            else
                                            {
                                                // EOF
                                                Debug.Print("File Downloading EOF Total Frames - {0} ", totalFrames);
                                                exitResult = ExitResult.reachedMaxTime;
                                                bExit = true;
                                                break;
                                            }
                                        }


                                        #region Get File Information

                                        // Retrieve file information if necessary   
                                        if (pf.fileID != currentFileID)
                                        {
                                            fih.AveGOPSize = fih.FrameHeight = fih.FrameRate = fih.FrameWidth = fih.TotalFrames = fih.VideoFormat = 0;
                                            byte[] pkt = VIServices.PacketGetFileName(Server, (int)pf.fileID);
                                            Socket s = IPServices.ConnectTCP(Server.ipAddress, (short)Server.port, ctStream);
                                            if (s != null)
                                            {
                                                IPServices.TransmitOnSocket(s, pkt, ctStream);
                                                byte[] filename = IPServices.ReceiveOnSocket(s, ctStream, rTimeout);
                                                if (filename != null)
                                                {
                                                    s.Close();
                                                    string xml = Encoding.ASCII.GetString(filename);
                                                    XmlDocument xmlDoc = new XmlDocument();
                                                    xmlDoc.LoadXml(xml);
                                                    XmlNode node = xmlDoc.SelectSingleNode("//NewDataSet/Table/FileName/text()");
                                                    string file = node.Value;

                                                    VideoFileDescriptor vfd = vfds[fileIndex];

                                                    node = xmlDoc.SelectSingleNode("//NewDataSet/Table/FileID/text()");
                                                    if(node!=null)
                                                    {
                                                        vfd.FileID = int.Parse(node.Value);
                                                    }

                                                    node = xmlDoc.SelectSingleNode("//NewDataSet/Table/ServerID/text()");
                                                    if (node != null)
                                                    {
                                                        vfd.ServerID = int.Parse(node.Value);
                                                    }

                                                    node = xmlDoc.SelectSingleNode("//NewDataSet/Table/CameraID/text()");
                                                    if (node != null)
                                                    {
                                                        vfd.CameraID = int.Parse(node.Value);
                                                    }

                                                    node = xmlDoc.SelectSingleNode("//NewDataSet/Table/TotalFrames/text()");
                                                    if (node != null)
                                                    {
                                                        vfd.TotalFrames = int.Parse(node.Value);
                                                        if(vfd.TotalFrames == 0)
                                                        {
                                                            // Might be that file was in recording...
                                                            Debug.Print("ZERO FRAME FILE??");
                                                        }
                                                        Debug.Print("File Downloading New File - Frames {0} ", vfd.TotalFrames);

                                                    }

                                                    node = xmlDoc.SelectSingleNode("//NewDataSet/Table/StartTime/text()");
                                                    if (node != null)
                                                    {
                                                        vfd.StartTime = DateTime.Parse(node.Value);
                                                    }

                                                    node = xmlDoc.SelectSingleNode("//NewDataSet/Table/EndTime/text()");
                                                    if (node != null)
                                                    {
                                                        vfd.EndTime = DateTime.Parse(node.Value);
                                                    }
                                                    pkt = VIServices.PacketGetFileInfoWithName(Server, cameraID, file);
                                                    s = IPServices.ConnectTCP(Server.ipAddress, (short)Server.port, ctStream);
                                                    if (s != null)
                                                    {
                                                        IPServices.TransmitOnSocket(s, pkt, ctStream);
                                                        // Only receive header - also gets timestamps if wanted
                                                        pkt = IPServices.ReceiveOnSocket(s, ctStream, rTimeout, Marshal.SizeOf(typeof(FILE_INFO_HEADER)));
                                                        if (pkt != null && pkt.Length != 0)
                                                        {
                                                            pinnedPacket = GCHandle.Alloc(pkt, GCHandleType.Pinned);
                                                            fih = (FILE_INFO_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(FILE_INFO_HEADER));
                                                            vfd.fih = fih;
                                                            pinnedPacket.Free();
                                                        }
                                                        else
                                                        {
                                                            // Error reading FILE_INFO_HEADER
                                                            exitResult = ExitResult.socketReceiveError;
                                                            bExit = true;
                                                            break;
                                                        }
                                                        s.Close();
                                                    }
                                                    else
                                                    {
                                                        // Error connecting (TCP) to get file information
                                                        exitResult = ExitResult.socketReceiveError;
                                                        bExit = true;
                                                        break;
                                                    }
                                                    vfds[fileIndex] = vfd;
                                                }
                                                else
                                                {
                                                    // Error receiving XML file name
                                                    exitResult = ExitResult.socketReceiveError;
                                                    bExit = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                // Error connecting (TCP) to server to file information
                                                exitResult = ExitResult.socketReceiveError;
                                                bExit = true;
                                                break;
                                            }
                                            currentFileID = pf.fileID;
                                        }
                                        #endregion


                                        // Receive the actual frame
                                        UInt32 length = pktType - pf.headerLength;
                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, rTimeout, (int)length);
                                        if (bytes != null && bytes.Length == length)
                                        {

                                            #region Frame writer

                                            // MSB of frame number is a key flag.  Trim it off and store it.
                                            bool key = (0x1000000 & pf.frameNumber) > 0;


                                            if (totalFrames == 0 && !key)
                                            {
                                                // Only start on a key
                                                // Ignore until key
                                                Debug.Print("Ignoring frame - Need key to start");
                                            }
                                            else
                                            {
                                                pf.frameNumber &= 0xffffff;


                                                // Have we opened the frames files yet?
                                                if (frames == null)
                                                {
                                                    string currentFrameFile = Path.Combine(new string[] { Path.GetTempPath(), currentFileGuid.ToString() + ".vd" });
                                                    frames = File.Open(currentFrameFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                                                }

                                                // Write the current frame to the video file
                                                uint streamPos = (uint)frames.Position;
                                                frames.Write(bytes, 0, (int)length);


                                                // Now update the frame descriptor (timestamp) 
                                                FrameDescriptor fd;
                                                va.Read<FrameDescriptor>(pf.frameNumber * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                                                fd.offset = streamPos;
                                                fd.length = (int)length;
                                                fd.isKey = key ? (byte)1 : (byte)0;
                                                fd.segmentFrameNumber = sessionFrameNumber++;
                                                va.Write<FrameDescriptor>(pf.frameNumber * Marshal.SizeOf(fd), ref fd);


                                                // Update the video file descriptor so it reflects the first and
                                                // last file downloaded
                                                VideoFileDescriptor vfd = vfds[fileIndex];
                                                if (vfd.FirstFrame == -1)
                                                {
                                                    vfd.FirstFrame = (int)pf.frameNumber;
                                                }
                                                vfd.LastFrame = (int)pf.frameNumber;
                                                vfds[fileIndex] = vfd;

                                                // Total frames written for the entire video
                                                ++totalFrames;
                                            }
                                            #endregion
                                        }
                                        else
                                        {
                                            // Receive error receiving frame data
                                            exitResult = ExitResult.socketReceiveError;
                                            bExit = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        // Error receiving frame header - PLAYER_FRAME
                                        exitResult = ExitResult.socketReceiveError;
                                        bExit = true;
                                        break;
                                    }
                                    break;
                                    #endregion
                            }

                        }
                        else
                        {
                            // Server closed connection?
                            Debug.Print("Error receiving frame type or length from server");
                            exitResult = ExitResult.socketReceiveError;
                            bExit = true;
                        }

                        #endregion

                    } //while (!ctStream.WaitHandle.WaitOne(0) && !bExit)


                    // totalFrames - 1 = last absolute frame number
                    // fileIndex = last file index

                    switch(exitResult)
                    {
                        case ExitResult.reachedMaxTime:
                        case ExitResult.eof:
                            // Both normal endings
                            break;
                        case ExitResult.badFile:
                            InBandConsoleMsg(queueCompressed, "Video download stopped - There is a corrupt video file on the server which cannot be downloaded", ctStream);
                            break;
                        case ExitResult.socketReceiveError:
                            InBandConsoleMsg(queueCompressed, "Video download stopped - There was a connection issue with the server", ctStream);
                            break;
                    }


                }
                catch(OperationCanceledException)
                {
                    // This can be ignored
                }
                catch (Exception ex)
                {
                    InBandConsoleMsg(queueCompressed, "Video download stopped - " + ex.Message, ctStream);
                    Debug.Print("Exception in streamer {0}", ex.Message);
                }
                finally
                {
                    if (fileIndex != -1)
                    {
                        VideoFileDescriptor vfd = vfds[fileIndex];
                        vfd.containsLastFrameInVideo = 1;
                        vfds[fileIndex] = vfd;
                    }
                    CloseVideoFiles(ref mmf, ref va, vfds, fileIndex, ref frames);
                }

                // Close the socket 
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch (SocketException sex)
                {
                    // Can ignore any socket errors at this point
                }

                Debug.Print("Streaming Task {0} stopped", Thread.CurrentThread.ManagedThreadId);

            }, ctStream, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            #endregion

            #region File reader

            Task.Factory.StartNew(() =>
            {
                // Memory mapped file for timestamps (FrameDescriptor)
                MemoryMappedFile mmf = null;
                // View accessor for timestamps
                MemoryMappedViewAccessor va = null;
                // Open the file descriptors shared memory
                SharedMemoryVD vfds = new SharedMemoryVD(FileDescriptors.ToString(), MAX_FILES);
                FileStream fsf = null;
                vfds.Open();
                try
                {
                    ProcessGOP submitGOP = (GOP gop) =>
                    {
                        if (gop != null)
                        {
                            if (gop.Key != -1)
                            {
                                int idx;
                                for (idx = 0; idx < gop.frames.Count; idx++)
                                {
                                    //Debug.Print("Frame {0} - Key {1} - Time {2}", gop.frames[idx].fileFrameNum, idx == gop.Key ? "Yes" : "No", gop.frames[idx].timeStamp);

                                    PLAYER_FRAME pf = new PLAYER_FRAME();
                                    pf.frameTime = (ulong)gop.frames[idx].timeStamp.Ticks;
                                    pf.frameNumber = gop.frames[idx].frameNumber;
                                    //if (idx == gop.Key)
                                    //{
                                    queueCompressed.Add(new CompressedFrame(pf, gop.fih.FrameWidth, gop.fih.FrameHeight, gop.fih.FrameRate, gop.fih.TotalFrames, gop.fih.VideoFormat, gop.fih.AveGOPSize, gop.frames[idx].frame, idx == gop.Key), ctStream);
                                    //}

                                }
                                InBandSignal(queueCompressed, 0, null, ctStream);
                            }
                            else
                            {
                                // Huh? No Key? Ignore
                            }
                        }

                        return 0;
                    };


                    int testMod = 0;

                    //VideoFileReader vfr = new VideoFileReader(vfds);

                    // Current index into the video file descriptors
                    int fileIndex = 0;
                    // Current frame number in a file
                    int frame = 0;

                    bool forward = true;


                    #region Main reader loop


                    /***********************************************************************************************/
                    /* VideoFileDescriptor shared memory array describes each file and runs from 0-MAX_FILES

                        //public struct VideoFileDescriptor
                        //{
                        //    public Guid FileName;            <- Guid.Empty until the timestamp file is valid
                        //    public FILE_INFO_HEADER fih;
                        //    public int FileID;
                        //    public int ServerID;
                        //    public int CameraID;
                        //    public int FirstFrame;           <- -1 or the first valid frame in the video file
                        //    public int LastFrame;            <- -1 or the last valid frame in the video file
                        //    public int TotalFrames;
                        //    public DateTime StartTime;
                        //    public DateTime EndTime;
                        //}
                     *  
                     *  Downloader - 1) Reads and write's a files timestamps, then updates the appropriate
                     *                  VideoFileDescriptor, setting the FileName Guid and FirstFrame and
                     *                  LastFrame to -1.
                     *               2) For each frame received, the frame is written to the frames files
                     *                  and FirstFrame (if appropriate) is updated and LastFrame is updated
                     *                  to the last valid frame at that moment.
                     * 
                     * 
                     * 
                     * FrameDescriptor (timestamps file)
                        //public struct FrameDescriptor
                        //{
                        //    public UInt32 timestampBias;         <- Times 10000 + VideoFileDescriptor.StartTime
                        //    public byte isKey;
                        //    public UInt32 offset;
                        //    public int length;                   <- 0, not present in frames file, -1 eof (whole video)
                        //    public UInt32 segmentFrameNumber;
                        //}

                    /***********************************************************************************************/


                    int seekToFrame = -1;
                    int gopKey = -1;

                    bool atLeastOneFileReceived = false;

                    #region File reader loop

                    while (!ctStream.WaitHandle.WaitOne(0))
                    {
                        VideoFileDescriptor vfd = new VideoFileDescriptor();

                        // Bind to the timestamps file (ts)

                        if(BindTimestampFile(vfds, MAX_FILES, fileIndex, ref mmf, ref va, ref vfd))
                        {

                            #region Timestamp file exists

                            //Debug.Print("New file {0}", vfd.FileName);

                            // There is at least one file 
                            atLeastOneFileReceived = true;


                            // At this point wait until VideoFileDescriptor.FirstFrame != -1 
                            while (!ctStream.WaitHandle.WaitOne(0))
                            {
                                vfd = vfds[fileIndex];
                                if (forward)
                                {
                                    if ((frame = vfd.FirstFrame) != -1)
                                    {
                                        Debug.Print("First frame {0}", vfd.FirstFrame);
                                        break;
                                    }
                                }
                                else
                                {
                                    if ((frame = vfd.LastFrame) != -1)
                                    {
                                        Debug.Print("Last frame {0}", vfd.LastFrame);
                                        break;
                                    }
                                }
                                if(sTask.Wait(0))
                                {
                                    // There won't be anymore downloading - empty file
                                    break;
                                }
                                Thread.Sleep(100);
                            }

                            GOP gop = null;

                            if (frame != -1)
                            {

                                // Bind to the video data file (vd)

                                if (BindFramesFile(vfd, ref fsf))
                                {

                                    while (!ctStream.WaitHandle.WaitOne(0))
                                    {
                                        FrameDescriptor fd;
                                        va.Read<FrameDescriptor>(frame * Marshal.SizeOf(typeof(FrameDescriptor)), out fd);
                                        if (fd.length != 0)
                                        {
                                            #region Frame consumption

                                            RawVideoFrame rvf = new RawVideoFrame();

                                            rvf.frame = new byte[fd.length];
                                            rvf.fileFrameNum = frame;

                                            fsf.Seek((long)fd.offset, SeekOrigin.Begin);
                                            int bytesRead;
                                            if ((bytesRead = fsf.Read(rvf.frame, 0, (int)fd.length)) == fd.length)
                                            {
                                                // NEW FRAME
                                                rvf.timeStamp = DateTime.FromBinary(vfd.StartTime.ToBinary() + ((long)(fd.timestampBias) * 10000));
                                                rvf.frameNumber = fd.segmentFrameNumber;
                                                if (fd.isKey == 1)
                                                {
                                                    bool addFrame = true;
                                                    if (gop != null)
                                                    {
                                                        if (gop.Key == -1)
                                                        {
                                                            if (!forward)
                                                            {
                                                                // Add the key frame
                                                                gop.frames.Add(rvf);
                                                                gop.Key = gop.frames.Count - 1;
                                                                addFrame = false;
                                                            }
                                                            else
                                                            {
                                                                Debug.Print("Incomplete GOP - frames = {0} - discarding", gop.frames.Count);
                                                                gop = null;
                                                            }
                                                        }
                                                        if (gop != null)
                                                        {
                                                            // ********************* FLUSH ************************
                                                            //Debug.Print("Complete GOP - frames = {0}", gop.frames.Count);
                                                            // ********************* FLUSH ************************

                                                            submitGOP(gop);

                                                            StreamAction sa;
                                                            if (sa_FileReader.TryTake(out sa))
                                                            {
                                                                if (sa.m_action == 4)
                                                                {
                                                                    // Flush pipeline
                                                                    InBandSignal(queueCompressed, 3, null, ctStream);
                                                                    sa_Decoder.Add(new StreamAction(0, 0));
                                                                }

                                                                sa = sa_FileReader.Take(ctStream);
                                                                if (sa.m_action == 3)
                                                                {
                                                                    forward = sa.m_val1 == 1 ? true : false;
                                                                }

                                                                sa = sa_FileReader.Take(ctStream);
                                                                seekToFrame = sa.m_val1;
                                                                // NOTE: Need to deal with seekToFrame == -1 and 
                                                                //       seekToFrame greater than EOF as edge cases

                                                                Debug.Print("Seek to frame {0}", seekToFrame);

                                                                int frameInFile;
                                                                int keyFrameNumberInFile;
                                                                int endOfGopFrameNumberInFile;
                                                                if (FindFrame(vfds, MAX_FILES, seekToFrame, out frameInFile, out keyFrameNumberInFile, out endOfGopFrameNumberInFile))
                                                                {
                                                                    gop = null;
                                                                    gopKey = -1;
                                                                    seekToFrame = -1;
                                                                    if (BindTimestampFile(vfds, MAX_FILES, frameInFile, ref mmf, ref va, ref vfd))
                                                                    {
                                                                        fileIndex = frameInFile;
                                                                        frame = forward ? keyFrameNumberInFile : endOfGopFrameNumberInFile;
                                                                        if (BindFramesFile(vfd, ref fsf))
                                                                        {
                                                                            continue;
                                                                        }
                                                                        else
                                                                        {
                                                                            // Huh?
                                                                            throw new Exception("BindFramesFile failed");
                                                                        }
                                                                    }
                                                                    // Huh?
                                                                    throw new Exception("BindTimestampFile failed");
                                                                }
                                                            }
                                                            gop = null;
                                                        }
                                                    }
                                                    if (addFrame)
                                                    {
                                                        gop = new GOP();
                                                        gop.fih = vfd.fih;
                                                        gop.frames.Add(rvf);
                                                        gop.Key = gop.frames.Count - 1;
                                                    }
                                                }
                                                else
                                                {
                                                    if (gop == null)
                                                    {
                                                        gop = new GOP();
                                                        gop.fih = vfd.fih;
                                                    }
                                                    gop.frames.Add(rvf);
                                                }


                                                //Debug.Print("New frame {0} - {1}", frame, timestamp);
                                            }
                                            else
                                            {
                                                // This is fatal
                                                throw new Exception("Error reading from video frames file");
                                            }
                                            //}
                                            #endregion

                                            if (forward)
                                            {
                                                ++frame;
                                                // Have we reached the end of the video segment file?
                                                if (frame > vfd.LastFrame)
                                                {
                                                    // Re-read in case downloader incremented it
                                                    //vfd = vfds[fileIndex];
                                                    //if (frame > vfd.LastFrame)
                                                    //{
                                                        if (vfd.downloadInProgress == 1)
                                                        {
                                                            Debug.Print("*** SPIN WAIT");
                                                            Thread.Sleep(500);
                                                            vfd = vfds[fileIndex];
                                                            --frame;
                                                        }
                                                        else
                                                        {
                                                            // Next file
                                                            break;
                                                        }
                                                    //}
                                                }
                                            }
                                            else
                                            {
                                                --frame;
                                                // Have we reached the beginning of the video segment file?
                                                if (frame < vfd.FirstFrame)
                                                {
                                                    // Move to previous segment file
                                                    break;
                                                }
                                            }

                                        }
                                        else
                                        {
                                            // Waiting for frame to appear
                                            Debug.Print("Spin on frame {0} - {1}", frame, fd.segmentFrameNumber);
                                            Thread.Sleep(100);
                                        }
                                    }
                                }

                                FreeFramesFile(ref fsf);
                                FreeTimestampFile(ref mmf, ref va);
                            }

                            submitGOP(gop);
                            gop = null;

                            #endregion // Timestamp file exists
                        } // BindTimestampFile

                        // Have we received ANY files yet?
                        if (atLeastOneFileReceived)
                        {
                            if (forward)
                            {
                                vfd = vfds[fileIndex];
                                if (vfd.containsLastFrameInVideo == 1)
                                {
                                    // Roll over
                                    // EOF
                                    InBandSignal(queueCompressed, 2, null, ctStream);
                                    fileIndex = 0;
                                }
                                else
                                {
                                    ++fileIndex;
                                    if(fileIndex == MAX_FILES)
                                    {
                                        fileIndex = 0;
                                    }
                                }
                            }
                            else
                            {
                                --fileIndex;
                                if (fileIndex < 0)
                                {
                                    // BOF, now what?
                                    fileIndex = MAX_FILES - 1;
                                }
                            }
                        }
                        else
                        {
                            // No files yet
                            Thread.Sleep(10);
                        }


                        CloseVideoFiles(ref mmf, ref va, ref fsf);
                    }

                    #endregion // File reader loop

                    CloseVideoFiles(ref mmf, ref va, ref fsf);

                    #endregion

                }
                catch (Exception ex)
                {
                    CloseVideoFiles(ref mmf, ref va, ref fsf);

                    if(sTask.Wait(5000))
                    {
                        List<string> files = new List<string>();
                        for (int idx = 0; idx < MAX_FILES; idx++)
                        {
                            VideoFileDescriptor vfd = vfds[idx];
                            if (vfd.FileName != Guid.Empty)
                            {
                                files.Add(Path.Combine(new string[] { Path.GetTempPath(), vfd.FileName.ToString() + ".ts" }));
                                files.Add(Path.Combine(new string[] { Path.GetTempPath(), vfd.FileName.ToString() + ".vd" }));
                            }
                        }
                        foreach (string file in files)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception exDelete)
                            {

                            }

                        }
                    }
                    Debug.Print("Exception in file thread {0}", ex.Message);
                }

                Debug.Print("Reading Task {0} stopped", Thread.CurrentThread.ManagedThreadId);

            }, ctStream, TaskCreationOptions.LongRunning, TaskScheduler.Default);


            #endregion

            #region Decoder task

            decodingTask = new AsyncRequestObject();
            CancellationToken ctDecoding = decodingTask.CancellationToken;
            decodingTask.task = Task.Factory.StartNew(() =>
            {
                Debug.Print("Decoding Task {0} started", Thread.CurrentThread.ManagedThreadId);
                try
                {

                    DecodingSession decodingSession = null;
                    UInt32 frameWidth = 0;
                    UInt32 frameHeight = 0;
                    UInt32 videoFormat = 0;

                    Task<bool> queueingTask = null;

                    List<CompressedFrame> lf = new List<CompressedFrame>();

                    while (!ctDecoding.WaitHandle.WaitOne(0))
                    {
                        StreamAction sa;
                        if (sa_Decoder.TryTake(out sa))
                        {
                            lf.Clear();
                            while (ctDecoding.WaitHandle.WaitOne(0) == false)
                            {
                                CompressedFrame cdump = queueCompressed.Take(ctDecoding);
                                if(cdump.FrameWidth == 3)
                                {
                                    // Signal to flush pipeline
                                    InBandSignal(queueUncompressed, 0, null, ctDecoding);
                                    decodingSession = null;
                                    break;
                                }
                            }
                        }


                        CompressedFrame cf = queueCompressed.Take(ctDecoding);
                        if (ctDecoding.WaitHandle.WaitOne(0))
                        {
                            continue;
                        }
                        if(cf.FrameWidth > 10)
                        {
                            // End of GOP
                            lf.Add(cf);
                            continue;
                        }
                        else
                        {
                            switch(cf.FrameWidth)
                            {
                                case 0:
                                    break;
                                case 2:
                                    InBandSignal(queueUncompressed, 2, null, ctDecoding);
                                    continue;
                                case 4:
                                    InBandSignal(queueUncompressed, 4, cf.Frame, ctDecoding);
                                    continue;
                            }
                        }
                        if(lf[0].key)
                        {
                            cf = lf[0];
                        }
                        else
                        {
                            cf = lf[lf.Count - 1];
                        }

                        if (decodingSession == null || frameWidth != cf.FrameWidth || frameHeight != cf.FrameHeight)
                        {

                            //IPPDecodingSession.CODEC codec = IPPDecodingSession.CODEC.NONE;
                            int  codec = 0;
                            switch (cf.VideoFormat)
                            {
                                case 2:
                                    codec = 3;
                                    break;
                                case 5:
                                case 7:
                                    codec = 4;
                                    break;
                                case 9:
                                    codec = 8;
                                    break;
                                case 17:
                                    codec = 16;
                                    break;
                                default:
                                    throw new Exception("Unsupported codec");
                            }

                            //decodingSession = new IPPDecodingSession(0, (int)cf.FrameWidth, (int)cf.FrameHeight, codec);
                            decodingSession = new MFTDecodingSession((int)cf.FrameWidth, (int)cf.FrameHeight, (int)codec);


                            //if (!((IPPDecodingSession)decodingSession).Init(cf.Frame))
                            if (!((MFTDecodingSession)decodingSession).Init(cf.Frame))
                            {
                                // Stop running
                                Debug.Print("DECODER FAILED TO INITIALIZE");
                                lf.Clear();
                                continue;
                            }
                            frameWidth = cf.FrameWidth;
                            frameHeight = cf.FrameHeight;
                            videoFormat = cf.VideoFormat;
                        }
                        
                        bool reverse = !lf[0].key;

                        int idx = reverse ? lf.Count - 1 : 0;
                        int inc = reverse ? -1 : 1;

                        List<UncompressedFrame> uf = new List<UncompressedFrame>();

                        /*
                        StreamAction sa;
                        if (streamActions.TryTake(out sa))
                        {
                            UncompressedFrame ufdump;
                            int topFrame = -1;
                            while (queueUncompressed.Count() != 0)
                            {
                                ufdump = queueUncompressed.Take();
                                if(topFrame==-1)
                                {
                                    topFrame = (int)ufdump.FrameNumber;
                                }
                                continue;
                            }
                            sa_Internal.Add(new StreamAction(topFrame, 0));
                        }
                        */

                        // Decode the entire GOP
                        while(idx >= 0 && idx < lf.Count)
                        {
                            CompressedFrame cfi = lf[idx];
                            // Fast
                            //if (cfi.key)
                            //{
                                byte[] decodedFrame = decodingSession.Decode(cfi.Frame, (int)cfi.FrameWidth, (int)cfi.FrameHeight, cfi.key ? (byte)1 : (byte)0, (long)cfi.PlayerFrame.frameTime);

                                if (decodedFrame != null)
                                {
                                    if (!reverse)
                                    {
                                        // No delay submit forward
                                        queueUncompressed.Add(new UncompressedFrame(cfi.PlayerFrame.frameNumber, decodedFrame, cfi.FrameWidth, cfi.FrameHeight, new DateTime((long)cfi.PlayerFrame.frameTime), (UInt32)cameraID, cfi.FrameRate), ctDecoding);
                                    }
                                    else
                                    {
                                        // Queue to submit backward
                                        uf.Add(new UncompressedFrame(cfi.PlayerFrame.frameNumber, decodedFrame, cfi.FrameWidth, cfi.FrameHeight, new DateTime((long)cfi.PlayerFrame.frameTime), (UInt32)cameraID, cf.FrameRate));
                                    }
                                }
                            //}
                            idx += inc;
                        }

                        if (reverse)
                        {
                            // Queue to submit backward
                            // Display the GOP as requested, forward or backward
                            idx = reverse ? lf.Count - 1 : 0;
                            inc = reverse ? -1 : 1;
                            while (idx >= 0 && idx < lf.Count)
                            {
                                UncompressedFrame ufi = uf[idx];
                                queueUncompressed.Add(new UncompressedFrame(ufi.FrameNumber, ufi.Frame, ufi.FrameWidth, ufi.FrameHeight, ufi.FrameTime, (UInt32)cameraID, ufi.FrameRate), ctDecoding);
                                idx += inc;
                            }
                        }


                        lf.Clear();    

                    }
                }
                catch (Exception ex)
                {
                    Debug.Print("Exception in decoder {0}", ex.Message);
                }
                Debug.Print("Decoding Task {0} stopped", Thread.CurrentThread.ManagedThreadId);
            }, ctDecoding, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            #endregion


            streamingTask.task = sTask;


            pQueue = queueUncompressed;
        }

        public static void RunPlaybackPipeline(VIServerDescriptor Server, int cameraID, Socket socket, OnNewPlaybackFrame NewPlaybackFrameCallback, out BlockingCollection<UncompressedFrame> pQueue, out AsyncRequestObject streamingTask, out AsyncRequestObject decodingTask)
        {
            BlockingCollection<CompressedFrame> queueCompressed = new BlockingCollection<CompressedFrame>(50);
            BlockingCollection<UncompressedFrame> queueUncompressed;
            if(Environment.Is64BitProcess)
            {
                queueUncompressed = new BlockingCollection<UncompressedFrame>(50);
            }
            else
            {
                queueUncompressed = new BlockingCollection<UncompressedFrame>(20);
            }


            #region Streaming task

            streamingTask = new AsyncRequestObject();
            CancellationToken ctStream = streamingTask.CancellationToken;
            streamingTask.task = Task.Factory.StartNew(() =>
            {
                Debug.Print("Streaming Task {0} started", Thread.CurrentThread.ManagedThreadId);
                try
                {
                    byte[] bytes = null;
                    bool bExit = false;
                    bool bFirstFrameInFile = true;
                    bool bFirstFrame = true;
                    UInt32 dwFrames = 0;
                    UInt64 firstFrameTime;
                    UInt32 currentFileID = 0;
                    FILE_INFO_HEADER fih;
                    fih.AveGOPSize = fih.FrameHeight = fih.FrameRate = fih.FrameWidth = fih.TotalFrames = fih.VideoFormat = 0;

                    while (!ctStream.WaitHandle.WaitOne(0) && !bExit)
                    {
                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, 5000, 4);
                        if (bytes != null && bytes.Length != 0)
                        {
                            UInt32 pktType = BitConverter.ToUInt32(bytes, 0);

                            switch (pktType)
                            {
                                case 0:
                                    // EOF (Video)
                                    Debug.Print("EOF");
                                    bExit = true;
                                    break;

                                case 1:
                                    // Error opening/reading video file - fatal
                                    Debug.Print("Error opening file");
                                    bExit = true;
                                    break;

                                case 2:
                                    // Timestamps for file
                                    {

                                        // Reset for new file
                                        dwFrames = 0;
                                        bFirstFrameInFile = true;

                                        UInt32 timestampsSize;
                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, 5000, 4);
                                        if (bytes != null && bytes.Length == 4)
                                        {
                                            timestampsSize = BitConverter.ToUInt32(bytes, 0);
                                        }
                                        else
                                        {
                                            Debug.Print("Error receiving timestamps size");
                                            bExit = true;
                                            break;
                                        }

                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, 5000, 8);
                                        if (bytes != null && bytes.Length == 8)
                                        {
                                            firstFrameTime = BitConverter.ToUInt64(bytes, 0);
                                        }
                                        else
                                        {
                                            Debug.Print("Error receiving timestamps epoch");
                                            bExit = true;
                                            break;
                                        }

                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, 5000, (int)timestampsSize - 8);
                                        if (bytes != null && bytes.Length == timestampsSize - 8)
                                        {
                                            //firstFrameTime = BitConverter.ToUInt64(bytes, 0);
                                        }
                                        else
                                        {
                                            Debug.Print("Error receiving timestamps");
                                            bExit = true;
                                            break;
                                        }

                                    }
                                    break;

                                default:
                                    // Frame
                                    if (bFirstFrame)
                                    {
                                        // SetInterframeTime
                                        UInt32 cmd = 4;
                                        Int32 val = 1; // 1 ms (really needs to be 0 but requires a server change)
                                        byte[] cmd1 = BitConverter.GetBytes(cmd);
                                        IPServices.TransmitOnSocket(socket, cmd1, ctStream);
                                        byte[] val1 = BitConverter.GetBytes(val);
                                        IPServices.TransmitOnSocket(socket, val1, ctStream);
                                        bFirstFrame = false;
                                    }

                                    if (bFirstFrameInFile)
                                    {
                                        //cfc.Clear();
                                        bFirstFrameInFile = false;
                                    }
                                    if (++dwFrames == 50)
                                    {
                                        //fps = cfc.CalcFreq(dwFrames);
                                        //if (fps > 0.0)
                                        //{
                                        //    OutputTraceLine(NULL, _T("fps=%lf"), fps);
                                        //}
                                        dwFrames = 0;
                                    }

                                    bytes = IPServices.ReceiveOnSocket(socket, ctStream, 5000, Marshal.SizeOf(typeof(PLAYER_FRAME)));
                                    if (bytes != null && bytes.Length == Marshal.SizeOf(typeof(PLAYER_FRAME)))
                                    {
                                        // Full header received
                                        GCHandle pinnedPacket = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                                        PLAYER_FRAME pf = (PLAYER_FRAME)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(PLAYER_FRAME));
                                        pinnedPacket.Free();

                                        //Debug.Print("{0}", pf.frameNumber &= 0xffffff);

                                        // Retrieve file information if necessary   
                                        if (pf.fileID != currentFileID)
                                        {
                                            fih.AveGOPSize = fih.FrameHeight = fih.FrameRate = fih.FrameWidth = fih.TotalFrames = fih.VideoFormat = 0;
                                            byte[] pkt = VIServices.PacketGetFileName(Server, (int)pf.fileID);
                                            Socket s = IPServices.ConnectTCP(Server.ipAddress, (short)Server.port, ctStream);
                                            if (s != null)
                                            {
                                                IPServices.TransmitOnSocket(s, pkt, ctStream);
                                                byte[] filename = IPServices.ReceiveOnSocket(s, ctStream, 5000);
                                                if (filename != null)
                                                {
                                                    string xml = Encoding.ASCII.GetString(filename);
                                                    XmlDocument xmlDoc = new XmlDocument();
                                                    xmlDoc.LoadXml(xml);
                                                    XmlNode node = xmlDoc.SelectSingleNode("//NewDataSet/Table/FileName/text()");
                                                    string file = node.Value;
                                                    pkt = VIServices.PacketGetFileInfoWithName(Server, cameraID, file);
                                                    s = IPServices.ConnectTCP(Server.ipAddress, (short)Server.port, ctStream);
                                                    if (s != null)
                                                    {
                                                        IPServices.TransmitOnSocket(s, pkt, ctStream);
                                                        // Only receive header - also gets timestamps if wanted
                                                        pkt = IPServices.ReceiveOnSocket(s, ctStream, 5000, Marshal.SizeOf(typeof(FILE_INFO_HEADER)));
                                                        if (pkt != null)
                                                        {
                                                            pinnedPacket = GCHandle.Alloc(pkt, GCHandleType.Pinned);
                                                            fih = (FILE_INFO_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(FILE_INFO_HEADER));
                                                            pinnedPacket.Free();
                                                        }
                                                        s.Close();
                                                    }
                                                    else
                                                    {
                                                        Debug.Print("1");
                                                        bExit = true;
                                                        break;
                                                    }

                                                }
                                                else
                                                {
                                                    Debug.Print("2");
                                                    bExit = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                Debug.Print("3");
                                                bExit = true;
                                                break;
                                            }
                                            currentFileID = pf.fileID;
                                        }

                                        UInt32 length = pktType - pf.headerLength;
                                        bytes = IPServices.ReceiveOnSocket(socket, ctStream, 5000, (int)length);
                                        if (bytes != null && bytes.Length == length)
                                        {
                                            bool key = (0x1000000 & pf.frameNumber) > 0;
                                            pf.frameNumber &= 0xffffff;

                                            //fs.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                                            //fs.Write(bytes, 0, bytes.Length);
                                            //Debug.Print("In Time = " + new DateTime((long)pf.frameTime).ToString("dddd MMMM d, yyyy h:mm:ss.fff  tt"));
                                            //if(queueingTask!=null)
                                            //{
                                            //    queueingTask.Wait();
                                            //}
                                            queueCompressed.Add(new CompressedFrame(pf, fih.FrameWidth, fih.FrameHeight, fih.FrameRate, fih.TotalFrames, fih.VideoFormat, fih.AveGOPSize, bytes, key), ctStream);
                                            //queueingTask = queueCompressed.SendAsync(new CompressedFrame(pf, fih.FrameWidth, fih.FrameHeight, fih.FrameRate, fih.TotalFrames, fih.VideoFormat, fih.AveGOPSize, bytes, key));
                                        }
                                        else
                                        {
                                            Debug.Print("4");
                                            bExit = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        Debug.Print("5");
                                        bExit = true;
                                        break;
                                    }
                                    break;
                            }

                        }
                        else
                        {
                            // Server closed connection?
                            Debug.Print("Error receiving frame type from server");
                            break;
                        }
                    }
                }
                catch(Exception ex)
                {
                    Debug.Print("Exception in streamer {0}", ex.Message);
                }
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch (Exception ex)
                {

                }
                Debug.Print("Streaming Task {0} stopped", Thread.CurrentThread.ManagedThreadId);
            }, ctStream, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            #endregion

            #region Decoder task

            decodingTask = new AsyncRequestObject();
            CancellationToken ctDecoding = decodingTask.CancellationToken;
            decodingTask.task = Task.Factory.StartNew(() =>
            {
                Debug.Print("Decoding Task {0} started", Thread.CurrentThread.ManagedThreadId);
                try
                {

                    DecodingSession decodingSession = null;
                    UInt32 frameWidth = 0;
                    UInt32 frameHeight = 0;
                    UInt32 videoFormat = 0;

                    Task<bool> queueingTask = null;

                    while (!ctDecoding.WaitHandle.WaitOne(0))
                    {
                        //CompressedFrame cf = queueCompressed.Receive(aro.CancellationToken);
                        CompressedFrame cf = queueCompressed.Take(ctDecoding);
                        if (ctDecoding.WaitHandle.WaitOne(0))
                        {
                            continue;
                        }
                        if (decodingSession == null || frameWidth != cf.FrameWidth || frameHeight != cf.FrameHeight)
                        {
                            //typedef enum _FILE_FORMAT
                            //{
                            //    MJPEG = 2,
                            //    DIVX = 5,
                            //    MPEG4 = 7,
                            //    H264 = 9,
                            //} FILE_FORMAT;
                            IPPDecodingSession.CODEC codec = IPPDecodingSession.CODEC.NONE;
                            switch (cf.VideoFormat)
                            {
                                case 2:
                                    codec = IPPDecodingSession.CODEC.MJPEG;
                                    break;
                                case 5:
                                case 7:
                                    codec = IPPDecodingSession.CODEC.MPEG4;
                                    break;
                                case 9:
                                    codec = IPPDecodingSession.CODEC.H264;
                                    break;
                                default:
                                    throw new Exception("Unsupported codec");
                            }

                            decodingSession = new IPPDecodingSession(0, (int)cf.FrameWidth, (int)cf.FrameHeight, codec); 
                           if (!((IPPDecodingSession)decodingSession).Init(cf.Frame))
                            {
                                // Stop running
                                continue;
                            }
                            frameWidth = cf.FrameWidth;
                            frameHeight = cf.FrameHeight;
                            videoFormat = cf.VideoFormat;
                            //frameRate = cf.FrameRate;
                        }
                        byte[] decodedFrame = decodingSession.Decode(cf.Frame, (int)cf.FrameWidth, (int)cf.FrameHeight, cf.key ? (byte)1 : (byte)0, (long)cf.PlayerFrame.frameTime);
                        if (decodedFrame != null)
                        {
                            //if(queueingTask!=null)
                            //{
                            //    queueingTask.Wait();
                            //}
                            //queueingTask = queueUncompressed.SendAsync(new UncompressedFrame(decodedFrame, cf.FrameWidth, cf.FrameHeight, new DateTime((long)cf.PlayerFrame.frameTime), (UInt32)cameraID, cf.FrameRate));

                            queueUncompressed.Add(new UncompressedFrame(0, decodedFrame, cf.FrameWidth, cf.FrameHeight, new DateTime((long)cf.PlayerFrame.frameTime), (UInt32)cameraID, cf.FrameRate), ctDecoding);

                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print("Exception in decoder {0}", ex.Message);
                }
                Debug.Print("Decoding Task {0} stopped", Thread.CurrentThread.ManagedThreadId);
            }, ctDecoding, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            #endregion

            #region Display task

            // UI thread
            //GetDisplayFrames(queueUncompressed, aro.CancellationToken, NewPlaybackFrameCallback);

            #endregion

            pQueue = queueUncompressed;

        }

        public static void DisplayFrames(BlockingCollection<UncompressedFrame> buffer, CancellationToken ct, OnNewLiveFrame NewLiveFrameCallback, PixelFormat pf)
        {
            TaskScheduler ts = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() =>
                {
                    while (!ct.WaitHandle.WaitOne(0))
                    {
                        if (buffer.Count > 0)
                        {
                            UncompressedFrame uf = buffer.Take(ct);
                            if (NewLiveFrameCallback != null)
                            {                              
                               
                                CudaTools.DX_GpuCopyImageToSurface(((CudaDecodingSession)uf.DecodingSession).GetCuda(), (int)uf.CameraID, uf.FrameGPU);

                                Task task = Task.Factory.StartNew(() =>
                                    {
                                        NewLiveFrameCallback(uf.Frame, uf.FrameGPU, (int)uf.FrameWidth, (int)uf.FrameHeight, (int)uf.CameraID, pf, null);

                                    }, ct, TaskCreationOptions.None, ts);

                                //NewLiveFrameCallback(uf.Frame, uf.FrameGPU, (int)uf.FrameWidth, (int)uf.FrameHeight, (int)uf.CameraID, pf, null);
                                CudaTools.VideoDecoder_ReleaseFrameGPU64(((CudaDecodingSession)uf.DecodingSession).GetDecoder());
                            }
                        }
                    }
                    buffer.Dispose();
                }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }


        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }



        public static AsyncRequestObject LiveStream(Socket socket, OnNewLiveFrame NewLiveFrameCallback, IntPtr Cuda, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            ts = ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts;

            IntPtr cuda = Cuda;

            BlockingCollection<LiveCompressedFrame> queueCompressed = new BlockingCollection<LiveCompressedFrame>(50);
            BlockingCollection<UncompressedFrame> queueUncompressed = new BlockingCollection<UncompressedFrame>(5);

            #region Streaming task

                    
            aro.task = Task.Factory.StartNew(() =>
            {
                byte[] bytes = null;

                HiResTimer t = new HiResTimer();

                Int64 t1 = t.Value;
                Int64 frames = 0;


                while (!aro.CancellationToken.WaitHandle.WaitOne(0))
                {
                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 30000, Marshal.SizeOf(typeof(DATA_FRAME_HEADER)));
                    if (bytes != null && bytes.Length != 0)
                    {
                        // Full header received
                        GCHandle pinnedPacket = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        DATA_FRAME_HEADER dfh = (DATA_FRAME_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(DATA_FRAME_HEADER));
                        pinnedPacket.Free();

                        // Get frame
                        bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, dfh.TotalLength - Marshal.SizeOf(typeof(DATA_FRAME_HEADER)));
                        if (bytes != null && bytes.Length != 0)
                        {
                            // SUBTRACT THE VZ AT THE END?

                            //frameCompressedIn.Post(bytes);
                            //frameCompressedIn2.Post(bytes);
                            //frameCompressedIn3.Post(bytes);
                            queueCompressed.Add(new LiveCompressedFrame(dfh, bytes), aro.CancellationToken);
                        }
                        else
                            break;
                    }
                    else
                    {
                        // Server closed connection?
                        break;
                    }

                    ++frames;
                    if (frames % 100 == 0)
                    {
                        Int64 timeTicks = t.Value - t1;
                        Int64 timeElapseInSeconds =
                         timeTicks / t.Frequency;
                        Debug.Print("Frame rate = {0}", (float)frames / (float)timeElapseInSeconds);

                    }
                }

                //fs.Close();

            }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            
            #endregion

            #region Decoding task

            // cuda?
            bool useCuda = true;
            bool leaveFramesOnGpu = true;
            bool useMFT = false;

            PixelFormat pf;
            if (useCuda || useMFT)
            {
                pf = PixelFormat.Format32bppRgb;
            }
            else
            {
                pf = PixelFormat.Format24bppRgb;
            }

            Task.Factory.StartNew(() =>
            {
               //System.Diagnostics.PerformanceCounter cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");


                UInt64 timestamp = 0;
                //VISearchPlugIn vis = new VISearchPlugIn("VISearch64d.dll");
                //vis.Load();
                //vis.ResolveAll();


                Dictionary<int, DecodingSession> decodeCollection = null;

                try
                {


                    decodeCollection = new Dictionary<int, DecodingSession>();


                    //CudaTranscoder ct = new CudaTranscoder(0, 0);
                    //ct.InitSink();
                    //UInt32 frames = 0;

                    //FileStream fs = new FileStream("TestVideo.H264", System.IO.FileMode.Create);
                    while (!aro.CancellationToken.WaitHandle.WaitOne(0))
                    {
                        LiveCompressedFrame lcf = queueCompressed.Take(aro.CancellationToken);
                        DecodingSession decodingSession = null;
                        if (!decodeCollection.ContainsKey(lcf.DataFrameHeader.CameraID))
                        {

                            //vis.Create(1, lcf.DataFrameHeader.FrameWidth, lcf.DataFrameHeader.FrameHeight, 500, 500, 500, 500);

                            //IntPtr p_hInputQueueSemaphore = new IntPtr();
                            //IntPtr p_hOutputQueueSemaphore = new IntPtr();
                            //IntPtr p_hSearchStoppedEvent = new IntPtr();
                            //vis.GetWindowsHandles(out p_hInputQueueSemaphore, out p_hOutputQueueSemaphore, out p_hSearchStoppedEvent);
                            //Semaphore inputQueueSemaphore = new Semaphore(0, int.MaxValue);
                            //inputQueueSemaphore.SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(p_hInputQueueSemaphore, false);
                            //Semaphore outputQueueSemaphore = new Semaphore(0, int.MaxValue);
                            //outputQueueSemaphore.SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(p_hOutputQueueSemaphore, false);
                            //ManualResetEvent searchStoppedEvent = new ManualResetEvent(false);
                            //searchStoppedEvent.SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(p_hSearchStoppedEvent, false);

                            //// create exit event
                            //ManualResetEvent exitEvent = new ManualResetEvent(false);

                            //bool success = vis.StartSearch(0.20);

                            bool initialized = false;
                            if(useMFT)
                            {
                                decodingSession = new MFTDecodingSession(lcf.DataFrameHeader.FrameWidth, lcf.DataFrameHeader.FrameHeight, lcf.DataFrameHeader.StreamingMode);
                                initialized = ((MFTDecodingSession)decodingSession).Init(null);
                            }
                            else if (useCuda == false)
                            {
                                //MessageBox.Show("NEW IPP DECODER");
                                IPPDecodingSession.CODEC codec = IPPDecodingSession.CODEC.NONE;
                                switch (lcf.DataFrameHeader.StreamingMode)
                                {
                                    case 3:
                                        codec = IPPDecodingSession.CODEC.MJPEG;
                                        break;
                                    case 4:
                                    case 6:
                                        codec = IPPDecodingSession.CODEC.MPEG4;
                                        break;
                                    case 8:
                                        codec = IPPDecodingSession.CODEC.H264;
                                        break;
                                    default:
                                        throw new Exception("Unsupported codec");
                                }
                                decodingSession = new IPPDecodingSession(lcf.DataFrameHeader.CameraID, lcf.DataFrameHeader.FrameWidth, lcf.DataFrameHeader.FrameHeight, codec);
                                initialized = ((IPPDecodingSession)decodingSession).Init(lcf.Frame);
                            }
                            else
                            {
                                decodingSession = new CudaDecodingSession(lcf.DataFrameHeader.CameraID, lcf.DataFrameHeader.FrameWidth, lcf.DataFrameHeader.FrameHeight, lcf.DataFrameHeader.StreamingMode, leaveFramesOnGpu);
                                initialized = ((CudaDecodingSession)decodingSession).Init();
                            }
                            decodeCollection.Add(lcf.DataFrameHeader.CameraID, decodingSession);
                        }
                        else
                        {
                            decodingSession = decodeCollection[lcf.DataFrameHeader.CameraID];
                        }
                        //vis.NewInputFrame(lcf.Frame, ++timestamp);
                        //if(timestamp % 100 == 0)
                        //{
                        //    Debug.Print("timestamp {0}", timestamp);
                        //}
                        //ulong start, stop, imgLen = 0;
                        //int rows = 0, cols = 0;
                        //byte[] image = new byte[0];
                        //if(vis.GetRange(out start, out stop, ref imgLen, image, ref rows, ref cols))
                        //{
                        //    Array.Resize(ref image, (int)imgLen);
                        //    int tmpRows = 0, tmpCols = 0;
                        //    vis.GetRange(out start, out stop, ref imgLen, image, ref tmpRows, ref tmpCols);
                        //    Debug.Print("Start {0}, Stop {1}, Rows {2}, Cols {3}", start, stop, rows, cols);

                        //    Bitmap b = new Bitmap(rows, cols, PixelFormat.Format24bppRgb);
                        //    BitmapData bm = b.LockBits(new Rectangle(0, 0, rows, cols), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                        //    Marshal.Copy(image, 0, bm.Scan0, rows * cols * 3);
                        //    b.UnlockBits(bm);
                        //    //ImageCodecInfo myImageCodecInfo = GetEncoderInfo("image/jpeg");

                        //    b.Save("frame_" + start.ToString() + ".jpg", ImageFormat.Jpeg);
                        //}

                        //VIClient.MFTSoftwareDecoder.Decode64(d, lcf.Frame, (uint)lcf.Frame.Length);

                        short fWidth = lcf.DataFrameHeader.FrameWidth;
                        short fHeight = lcf.DataFrameHeader.FrameHeight;
                        //short fWidth = 640;
                        //short fHeight = 480;

                        if (decodingSession != null)
                        {
                            IntPtr ptrFrameGPU = decodingSession.DecodeGPU(cuda, lcf.Frame, fWidth, fHeight, lcf.DataFrameHeader.KeyFlag); 
                            //byte[] decodedFrame = decodingSession.Decode(lcf.Frame, fWidth, fHeight, lcf.DataFrameHeader.KeyFlag);
                            //if (decodedFrame != null)
                            if(ptrFrameGPU != IntPtr.Zero)
                            {

                                //Debug.Print("FRAME");

                                queueUncompressed.Add(new UncompressedFrame(0, ptrFrameGPU, decodingSession, (uint)fWidth, (uint)fHeight, DateTime.Now, (UInt32)lcf.DataFrameHeader.CameraID), aro.CancellationToken);




                                //System.Diagnostics.PerformanceCounter cpuCounter;
                                //float cpuUsage = cpuCounter.NextValue();
                                //cpuCounter.CategoryName = "Processor";
                                //cpuCounter.CounterName = "% Processor Time";
                                //cpuCounter.InstanceName = "_Total";
                                //int s;
                                //if (cpuUsage > 60.0)
                                //    s = (int)cpuUsage * 5; //3;
                                //else if (cpuUsage > 30)
                                //    s = (int)cpuUsage * 2;  //2;
                                //else
                                //    s = 0;
                                //if (s != 0)
                                //{
                                //    Thread.Sleep(s);
                                //}
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    if (decodeCollection != null)
                    {
                        foreach (KeyValuePair<int, DecodingSession> ds in decodeCollection)
                        {
                            if (ds.Value != null)
                            {
                                ds.Value.CloseDecoder();
                            }
                        }
                    }
                    //queueCompressed.Dispose();
                    //queueUncompressed.Dispose();
                    GC.Collect();
                    throw ex;
                }
                if (decodeCollection != null)
                {
                    foreach (KeyValuePair<int, DecodingSession> ds in decodeCollection)
                    {
                        if (ds.Value != null)
                        {
                            ds.Value.CloseDecoder();
                        }
                    }
                }
            }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            #endregion

            DisplayFrames(queueUncompressed, aro.CancellationToken, NewLiveFrameCallback, pf);

            return aro;
        }


        public static AsyncRequestObject RunPlaybackStream(VIServerDescriptor Server, int cameraID, Socket socket, OnNewPlaybackFrame NewPlaybackFrameCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            VIServices.__PrintCurrentThreadID();
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            ts = ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts;
            aro.task = Task.Factory.StartNew(() =>
            {
                VIServices.__PrintCurrentThreadID();
                byte[] bytes = null;
                Dictionary<int, VIDecodingSession> decodeCollection = new Dictionary<int, VIDecodingSession>();

                bool bExit = false;
                bool bFirstFrameInFile = true;
                bool bFirstFrame = true;
                UInt32 dwFrames = 0;
                UInt64 firstFrameTime;
                UInt32 currentFileID = 0;
                FILE_INFO_HEADER fih;
                fih.AveGOPSize = fih.FrameHeight = fih.FrameRate = fih.FrameWidth = fih.TotalFrames = fih.VideoFormat = 0;

                BlockingCollection<CompressedFrame> compressedFrames = new BlockingCollection<CompressedFrame>(new ConcurrentQueue<CompressedFrame>(), 50);
                BlockingCollection<UncompressedFrame> uncompressedFrames = new BlockingCollection<UncompressedFrame>(new ConcurrentQueue<UncompressedFrame>(), 50);


                /*************************************************************************************************/
                /* Decoder Task                                                                                  */
                /*************************************************************************************************/
                #region Decoder Task


                Task.Factory.StartNew(() =>
                    {
                        VIServices.__PrintCurrentThreadID();
                        VIDecodingSession decodingSession = null;
                        UInt32 frameWidth = 0;
                        UInt32 frameHeight = 0;
                        UInt32 videoFormat = 0;
                        Int32 sleepTime = -1;
                        float divisor = 1.0F;
                        UInt32 frameRate = 0;
                        bool sleep = false;



                        /*************************************************************************************************/
                        /* Display Task                                                                                  */
                        /*************************************************************************************************/
                        #region Display thread

                        Task.Factory.StartNew(() =>
                        {
                            UncompressedFrame uf;
                            while(uncompressedFrames.TryTake(out uf, Timeout.Infinite, aro.CancellationToken))
                            {
                                Task.Factory.StartNew(() =>
                                {
                                    if (NewPlaybackFrameCallback != null)
                                    {
                                        NewPlaybackFrameCallback(uf.Frame, (int)uf.FrameWidth, (int)uf.FrameHeight, uf.FrameTime, null);
                                    }
                                }, CancellationToken.None, TaskCreationOptions.None, ts).Wait(aro.CancellationToken);
                                if (!sleep)
                                {
                                    sleep = true;
                                    sleepTime = (int)CalcInterframeTime((Int32)frameRate, divisor);
                                }
                                Thread.Sleep(sleepTime);
                            }
                        }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                        #endregion

                        while (!aro.CancellationToken.WaitHandle.WaitOne(0))
                        {
                            CompressedFrame cf;
                            if (!compressedFrames.TryTake(out cf, -1, aro.CancellationToken))
                            {
                                // Cancel
                                continue;
                            }
                            if (decodingSession == null || frameWidth != cf.FrameWidth || frameHeight != cf.FrameHeight)
                            {
                                //typedef enum _FILE_FORMAT
                                //{
                                //    MJPEG = 2,
                                //    DIVX = 5,
                                //    MPEG4 = 7,
                                //    H264 = 9,
                                //} FILE_FORMAT;
                                Videoinsight.LIB.VITypes.StreamingMode sm = Videoinsight.LIB.VITypes.StreamingMode.H264;
                                switch(cf.VideoFormat)
                                {
                                    case 2:
                                        sm = Videoinsight.LIB.VITypes.StreamingMode.MJPG;
                                        break;
                                    case 5:
                                    case 7:
                                        sm = Videoinsight.LIB.VITypes.StreamingMode.DIVX;
                                        break;
                                    case 9:
                                        sm = Videoinsight.LIB.VITypes.StreamingMode.H264;
                                        break;
                                    default:
                                        throw new Exception("Unsupported codec");
                                }

                                decodingSession = new VIDecodingSession(0, (int)cf.FrameWidth, (int)cf.FrameHeight, (byte)sm);
                                if (!decodingSession.Init())
                                {
                                    bExit = true;
                                    break;
                                }
                                frameWidth = cf.FrameWidth;
                                frameHeight = cf.FrameHeight;
                                videoFormat = cf.VideoFormat;
                                frameRate = cf.FrameRate;
                            }
                            try
                            {
                                byte[] decodedFrame = decodingSession.Decode(cf.Frame, (int)cf.FrameWidth, (int)cf.FrameHeight, cf.key ? (byte)1 : (byte)0);
                                if (decodedFrame != null)
                                {
                                    uncompressedFrames.TryAdd(new UncompressedFrame(0, decodedFrame, cf.FrameWidth, cf.FrameHeight, new DateTime((long)cf.PlayerFrame.frameTime), (UInt32)cameraID), Timeout.Infinite, aro.CancellationToken);
                                }   
                            }
                            catch (Exception e)
                            {
                                Task.Factory.StartNew(() =>
                                {
                                    if (NewPlaybackFrameCallback != null)
                                    {
                                        NewPlaybackFrameCallback(null, 0, 0, DateTime.Now, e);
                                    }
                                }, CancellationToken.None, TaskCreationOptions.None, ts);
                            }
                        }
                        
           
                    }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                #endregion // Decoder Task

                //FileStream fs = new FileStream("Basketball.H264", System.IO.FileMode.Create);

                while (!aro.CancellationToken.WaitHandle.WaitOne(0) && !bExit)
                {
                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, 4);
                    if (bytes != null && bytes.Length != 0)
                    {
                        UInt32 pktType = BitConverter.ToUInt32(bytes, 0);

                        switch(pktType)
                        {
                            case 0:
                                // EOF (Video)
                                bExit = true;
                                break;

                            case 1:
                                // Error opening/reading video file - fatal
                                bExit = true;
                                break;

                            case 2:
                                // Timestamps for file
                                {

                                    // Reset for new file
                                    dwFrames = 0;
                                    bFirstFrameInFile = true;

                                    UInt32 timestampsSize;
                                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, 4);
                                    if (bytes != null && bytes.Length==4)
                                    {
                                        timestampsSize = BitConverter.ToUInt32(bytes, 0);
                                    }
                                    else
                                    {
                                        bExit = true;
                                        break;
                                    }

                                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, 8);
                                    if (bytes != null && bytes.Length == 8)
                                    {
                                        firstFrameTime = BitConverter.ToUInt64(bytes, 0);
                                    }
                                    else
                                    {
                                        bExit = true;
                                        break;
                                    }

                                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, (int)timestampsSize-8);
                                    if (bytes != null && bytes.Length == timestampsSize-8)
                                    {
                                        //firstFrameTime = BitConverter.ToUInt64(bytes, 0);
                                    }
                                    else
                                    {
                                        bExit = true;
                                        break;
                                    }

                                }
                                break;

                            default:
                                // Frame
                                if (bFirstFrame)
                                {
                                    // SetInterframeTime
                                    UInt32 cmd = 4;
                                    UInt32 val = 1; // 1 ms (really needs to be 0 but requires a server change)
                                    byte[] cmd1 = BitConverter.GetBytes(cmd);
                                    IPServices.TransmitOnSocket(socket, cmd1, aro.CancellationToken);
                                    byte[] val1 = BitConverter.GetBytes(val);
                                    IPServices.TransmitOnSocket(socket, val1, aro.CancellationToken);
                                    bFirstFrame = false;
                                }

                  				if (bFirstFrameInFile)
					    		{
						    		//cfc.Clear();
							    	bFirstFrameInFile = false;
							    }
							    if (++dwFrames == 50)
							    {
								    //fps = cfc.CalcFreq(dwFrames);
								    //if (fps > 0.0)
								    //{
									//    OutputTraceLine(NULL, _T("fps=%lf"), fps);
								    //}
								    dwFrames = 0;
							    }

                                bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, Marshal.SizeOf(typeof(PLAYER_FRAME)));
                                if(bytes!=null && bytes.Length==Marshal.SizeOf(typeof(PLAYER_FRAME)))
                                {
                                    // Full header received
                                    GCHandle pinnedPacket = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                                    PLAYER_FRAME pf = (PLAYER_FRAME)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(PLAYER_FRAME));
                                    pinnedPacket.Free();

                                    // Retrieve file information if necessary   
                                    if (pf.fileID != currentFileID)
                                    {
                                        fih.AveGOPSize = fih.FrameHeight = fih.FrameRate = fih.FrameWidth = fih.TotalFrames = fih.VideoFormat = 0;
                                        byte[] pkt = VIServices.PacketGetFileName(Server, (int)pf.fileID);
                                        Socket s = IPServices.ConnectTCP(Server.ipAddress, (short)Server.port, aro.CancellationToken);
                                        if(s!=null)
                                        {
                                            IPServices.TransmitOnSocket(s, pkt, aro.CancellationToken);
                                            byte[] filename = IPServices.ReceiveOnSocket(s, aro.CancellationToken, 5000);
                                            if (filename != null)
                                            {
                                                string xml = Encoding.ASCII.GetString(filename);
                                                XmlDocument xmlDoc = new XmlDocument();
                                                xmlDoc.LoadXml(xml);
                                                XmlNode node = xmlDoc.SelectSingleNode("//NewDataSet/Table/fileName/text()");
                                                string file = node.Value;
                                                pkt = VIServices.PacketGetFileInfoWithName(Server, cameraID, file);
                                                s = IPServices.ConnectTCP(Server.ipAddress, (short)Server.port, aro.CancellationToken);
                                                if(s!=null)
                                                {
                                                    IPServices.TransmitOnSocket(s, pkt, aro.CancellationToken);
                                                    // Only receive header - also gets timestamps if wanted
                                                    pkt = IPServices.ReceiveOnSocket(s, aro.CancellationToken, 5000, Marshal.SizeOf(typeof(FILE_INFO_HEADER)));
                                                    if (pkt != null)
                                                    {
                                                        pinnedPacket = GCHandle.Alloc(pkt, GCHandleType.Pinned);
                                                        fih = (FILE_INFO_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(FILE_INFO_HEADER));
                                                        pinnedPacket.Free();
                                                    }
                                                    s.Close();
                                                }
                                                else
                                                {
                                                    bExit = true;
                                                    break;
                                                }

                                            }
                                            else
                                            {
                                                bExit = true;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            bExit = true;
                                            break;
                                        }
                                        currentFileID = pf.fileID;
                                    }

                                    UInt32 length = pktType - pf.headerLength;
                                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, (int)length);
                                    if (bytes != null && bytes.Length == length)
                                    {                                        
                                        bool key = (0x1000000 & pf.frameNumber) > 0;
                                        pf.frameNumber &= 0xffffff;

                                        //fs.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                                        //fs.Write(bytes, 0, bytes.Length);


                                        bExit = !compressedFrames.TryAdd(new CompressedFrame(pf, fih.FrameWidth, fih.FrameHeight, fih.FrameRate, fih.TotalFrames, fih.VideoFormat, fih.AveGOPSize, bytes, key), Timeout.Infinite, aro.CancellationToken);
                                    }
                                    else
                                    {
                                        bExit = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    bExit = true;
                                    break;
                                }
                            break;
                        }

                    }
                    else
                    {
                        // Server closed connection?
                        break;
                    }
                }
                //fs.Close();
            }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return aro;
        }


    }


    class FrameScrubber
    {
        class VideoFile
        {
            public string name;
            public FILE_INFO_HEADER fih;
        }

        private Dictionary<int, VideoFile> m_fileCache = new Dictionary<int, VideoFile>();

        private int m_cameraID;
        private VIServerDescriptor m_server;
        private VIDecodingSession m_decoder;
        private OnNewPlaybackFrame m_onNewPlaybackFrame;

        public FrameScrubber(VIServerDescriptor server, int cameraID, OnNewPlaybackFrame NewPlaybackFrameCallback)
        {
            m_decoder = null;
            m_server = server;
            m_cameraID = cameraID;
            m_onNewPlaybackFrame = NewPlaybackFrameCallback;
        }

        ~FrameScrubber()
        {
        }

        void GetGOP(VideoFile vf, int frame)
        {
            bool keyOnly = true;
            IPServices.TCPTransactionTask(m_server, VIServices.PacketGetGOPWithNameAndIndexRange(m_server, vf.name, frame, frame, keyOnly),
                 (Object resultGOP, AsyncRequestObject aroGOP) =>
                 {
                     if (resultGOP is Exception)
                     {
                         // FAILURE
                     }
                     else
                     {
                         if (resultGOP is byte[])
                         {
                             byte[] pkt = (byte[])resultGOP;
                             int idx = 0;
                             // Total length of frame received - should equal length of pkt
                             UInt32 bytes = BitConverter.ToUInt32(pkt, idx);
                             if (bytes == pkt.Length)
                             {
                                 idx += 4;
                                 UInt16 gopFrameCount = BitConverter.ToUInt16(pkt, idx);
                                 idx += 2;
                                 UInt16 frameCount = gopFrameCount;
                                 UInt32 frameNumber;
                                 UInt32 keyFrame = 0;
                                 while (frameCount != 0)
                                 {
                                     UInt32 frameLength = BitConverter.ToUInt32(pkt, idx) - 8;
                                     idx += 4;
                                     frameNumber = BitConverter.ToUInt32(pkt, idx);
                                     idx += 4;
                                     bool bIsKeyFrame = false;
                                     if (frameNumber >= 0x1000000)
                                     {
                                         //0x01 00 00 00, the highest byte of framenum is used as the key frame flag
                                         bIsKeyFrame = true;
                                         frameNumber -= 0x1000000;
                                         keyFrame = frameNumber;
                                     }
                                     // DECODE HERE
                                     // frameLength and pkt[idx]
                                    //typedef enum _FILE_FORMAT
                                    //{
                                    //    MJPEG = 2,
                                    //    DIVX = 5,
                                    //    MPEG4 = 7,
                                    //    H264 = 9,
                                    //} FILE_FORMAT;
                                     if(m_decoder == null)
                                     {
                                        Videoinsight.LIB.VITypes.StreamingMode sm = Videoinsight.LIB.VITypes.StreamingMode.H264;
                                        switch(vf.fih.VideoFormat)
                                        {
                                            case 2:
                                                sm = Videoinsight.LIB.VITypes.StreamingMode.MJPG;
                                                break;
                                            case 5:
                                            case 7:
                                                sm = Videoinsight.LIB.VITypes.StreamingMode.DIVX;
                                                break;
                                            case 9:
                                                sm = Videoinsight.LIB.VITypes.StreamingMode.H264;
                                                break;
                                            default:
                                                throw new Exception("Unsupported codec");
                                        }

                                        m_decoder = new VIDecodingSession(0, (int)vf.fih.FrameWidth, (int)vf.fih.FrameHeight, (byte)sm);
                                        if (!m_decoder.Init())
                                        {
                                        }
                                     }
                                     byte[] a = new byte[frameLength];
                                     Array.Copy(pkt, idx, a, 0, frameLength);
                                     byte[] decodedFrame = m_decoder.Decode(a, (int)vf.fih.FrameWidth, (int)vf.fih.FrameHeight, bIsKeyFrame ? (byte)1 : (byte)0);
                                     if (decodedFrame != null)
                                     {
                                        // FRAME
                                         if(m_onNewPlaybackFrame!=null)
                                         {
                                             m_onNewPlaybackFrame(decodedFrame, (int)vf.fih.FrameWidth, (int)vf.fih.FrameHeight, DateTime.MinValue, null);
                                         }
                                     }
                                     idx += (int)frameLength;
                                     --frameCount;
                                 }
                             }
                         }
                     }
                 });
        }

        public void Scrub(DateTime start, CancellationToken ct)
        {
            int fileID;
            int frame;
            IPServices.TCPTransactionTask(m_server, VIServices.PacketFileAndFrameFromTime(m_server, m_cameraID, start),
               (Object resultFFFT, AsyncRequestObject aroFFFT) =>
               {
                   if (resultFFFT is Exception)
                   {
                       // FAILURE
                   }
                   else
                   {
                       if (resultFFFT is byte[])
                       {
                           byte[] resultBytes = (byte[])resultFFFT;
                           if (resultBytes.Length == 8)
                           {
                               fileID = BitConverter.ToInt32(resultBytes, 0);
                               frame = BitConverter.ToInt32(resultBytes, 4);

                               if (m_fileCache.ContainsKey(fileID))
                               {
                                   GetGOP(m_fileCache[fileID], frame);
                               }
                               else
                               {
                                   IPServices.TCPTransactionTask(m_server, VIServices.PacketGetFileName(m_server, fileID),
                                   (Object resultGFN, AsyncRequestObject aroGFN) =>
                                   {
                                       if (resultGFN is Exception)
                                       {
                                           // FAILURE
                                       }
                                       else
                                       {
                                           if (resultGFN is byte[])
                                           {
                                               string xml = Encoding.ASCII.GetString((byte[])resultGFN);
                                               XmlDocument xmlDoc = new XmlDocument();
                                               xmlDoc.LoadXml(xml);
                                               XmlNode node = xmlDoc.SelectSingleNode("//NewDataSet/Table/fileName/text()");
                                               string fileName = node.Value;
                                               byte[] pkt = VIServices.PacketGetFileInfoWithName(m_server, m_cameraID, fileName);
                                               Socket s = IPServices.ConnectTCP(m_server.ipAddress, (short)m_server.port, ct);
                                               if (s != null)
                                               {
                                                   IPServices.TransmitOnSocket(s, pkt, ct);
                                                   // Only receive header - also gets timestamps if wanted
                                                   pkt = IPServices.ReceiveOnSocket(s, ct, 5000, Marshal.SizeOf(typeof(FILE_INFO_HEADER)));
                                                   if (pkt != null)
                                                   {
                                                       GCHandle pinnedPacket = GCHandle.Alloc(pkt, GCHandleType.Pinned);
                                                       FILE_INFO_HEADER fih = (FILE_INFO_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(FILE_INFO_HEADER));
                                                       pinnedPacket.Free();
                                                       VideoFile vf = new VideoFile();
                                                       vf.name = fileName;
                                                       vf.fih = fih;
                                                       m_fileCache.Add(fileID, vf);
                                                       GetGOP(m_fileCache[fileID], frame);
                                                   }
                                                   s.Close();
                                               }
                                           }
                                       }
                                   });
                               }
                           }
                       }
                   }
               });
        }

    }


}
