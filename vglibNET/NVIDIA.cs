using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NVIDIA
{
    public class CudaTools
    {
        const string VGLIBDLL_NAME = "vglib.dll";

        public enum NVIDIAChipset
        {
            Unsupported,
            MaxwellGen1,
            MaxwellGen2,
            AtLeastMaxwellGen2
        };

        public enum CODEC
        {
            MPEG1,
            MPEG2,
            MPEG4,
            VC1,
            H264,
            JPEG,
            H264_SVC,
            H264_MVC,
            HEVC,
            VP8,
            VP9
        }
        public enum DECODER_MODE
        {
            CPU_ARGB,    // ARGB decoded frames are pushed onto CPU decoded frame queue
            CPU_RGB,     // RGB decoded frames are pushed onto CPU decoded frame queue
            GPU_ARGB,    // ARGB decoded frames are pushed onto GPU decoded frame queue
            GPU_RGB,     // RGB decoded frames are pushed onto GPU decoded frame queue
            GPU_GPUMAT,  // GpuMat objects are pushed onto GpuMatFIFO
            TO_ENCODER,  // this mode for fast h264 transcoding (Decode -> NV12 -> Encode), uses FrameQueue
            CPU_NV12     // NV12 decoded frames are pushed onto CPU decoded frame queue
        };

        public enum ENCODER_MODE
        {
            ENCODER_TO_H264,
            ENCODER_TO_HEVC,  // H265
            ENCODER_TO_JPEG
        };

        public enum VIDEOSINK_MODE
        {
            MP4,
            AVI,
            OUTPUT_QUEUE
        }

        //////////////////////////////////////////////////////////////
        ///     CUDA interface
        //////////////////////////////////////////////////////////////

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_Create")]
        // VGLIB_EXPORT void* Cuda_Create();
        public static extern IntPtr Cuda_Create64();

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_Free")]
        // VGLIB_EXPORT void  Cuda_Free(void* pCuda);
        public static extern void Cuda_Free64(IntPtr pCuda);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_GetDeviceCount")]
        // VGLIB_EXPORT int   Cuda_GetDeviceCount(void* pCuda);
        public static extern int Cuda_GetDeviceCount64(IntPtr pCuda);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_GetComputeCapability")]
        // VGLIB_EXPORT void  Cuda_GetComputeCapability(void* pCuda, int* major, int* minor);
        public static extern void Cuda_GetComputeCapability64(IntPtr pCuda, out int major, out int minor);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_GetDeviceName")]
        // VGLIB_EXPORT void  Cuda_GetDeviceName(void* pCuda, char* deviceName);
        public static extern void Cuda_GetDeviceName64(IntPtr pCuda, [MarshalAs(UnmanagedType.LPArray)]  byte[] deviceName);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_GetDeviceMemory")]
        // VGLIB_EXPORT void  Cuda_GetDeviceMemory(void* pCuda, unsigned long* totalMem, unsigned long* freeMem);
        public static extern void Cuda_GetDeviceMemory64(IntPtr pCuda, out UInt64 totalMem, out UInt64 freeMem);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_GetContext")]
        // VGLIB_EXPORT bool  Cuda_GetContext(void* pCuda, void **pCudaCtx);
        public static extern bool Cuda_GetContext64(IntPtr pCuda, out IntPtr pCudaCtx);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_IsReady")]
        // VGLIB_EXPORT bool  Cuda_IsReady(void* pCuda);
        public static extern bool Cuda_IsReady64(IntPtr pCuda);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_SetContext")]
        //VGLIB_EXPORT void Cuda_SetContext(void* pCuda, void* pCudaCtx)
        public static extern void Cuda_SetContext64(IntPtr pCuda, IntPtr pCudaCtx);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cuda_CopyDataFromGpu")]        
        // VGLIB_EXPORT bool  Cuda_CopyDataFromGpu(void* pCuda, void* cpuDest, void* gpuSource, int numBytes);
        private static extern bool Cuda_CopyDataFromGpu64(IntPtr pCuda, IntPtr CpuDest, IntPtr GpuSource, int numBytes);


        public static bool Cuda_CopyDataFromGpu(IntPtr pCuda, out byte[] dest, IntPtr source, int numBytes)
        {
            // pCuda  = pointer to the CudaUtil instance
            // source = the gpu memory pointer to the location of data to be copied to the cpu
            // dest   = the managed cpu array to which the data is copied
            // numBytes = the number of bytes to copy

            bool success = true;

            IntPtr destPtr = Marshal.AllocHGlobal(numBytes);

            if (Cuda_CopyDataFromGpu64(pCuda, destPtr, source, numBytes))
            {
                // copy from unmanaged to managed
                dest = new byte[numBytes];
                Marshal.Copy(destPtr, dest, 0, numBytes);
            }
            else
            {
                dest = null;
                success = false;
            }

            Marshal.FreeHGlobal(destPtr);

            return success;
        }


        //////////////////////////////////////////////////////////////
        /// VideoDecoder interface
        //////////////////////////////////////////////////////////////

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Create")]
        // VGLIB_EXPORT void* VideoDecoder_Create(void *pCudaContext);
        public static extern IntPtr VideoDecoder_Create64(IntPtr pCudaContext);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Init")]
        // VGLIB_EXPORT bool  VideoDecoder_Init(void *pDecoder);
        public static extern bool VideoDecoder_Init64(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_GetWindowsHandles")]
        // VGLIB_EXPORT void  VideoDecoder_GetWindowsHandles(void *pDecoder, HANDLE *pInputQueueSemaphore, HANDLE *pOutputQueueSemaphore, HANDLE *pDecoderStoppedEvent);
        public static extern IntPtr VideoDecoder_GetWindowsHandles64(IntPtr pDecoder, out IntPtr pInputQueueSemaphore, out IntPtr pOutputQueueSemaphore, out IntPtr pDecoderStoppedEvent);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Start")]
        // VGLIB_EXPORT void  VideoDecoder_Start(void *pDecoder);
        public static extern void VideoDecoder_Start64(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Stop")]
        // VGLIB_EXPORT void  VideoDecoder_Stop(void *pDecoder);
        public static extern void VideoDecoder_Stop64(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_GetLastErrorMsg", CharSet = CharSet.Unicode)]
        // VGLIB_EXPORT void  VideoDecoder_GetLastErrorMsg(void *pDecoder, char* errMsg);
        public static extern void VideoDecoder_GetLastErrorMsg64(IntPtr pDecoder, [MarshalAs(UnmanagedType.LPArray)] byte[] errMsg);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_ConfigureDecoder")]
        // VGLIB_EXPORT void  VideoDecoder_ConfigureDecoder(void* pDecoder, int outputWidth, int outputHeight, int mode);
        public static extern void VideoDecoder_ConfigureDecoder64(IntPtr pDecoder, int outputWidth, int outputHeight, int mode, int codec);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_NewInputFrame")]
        // VGLIB_EXPORT void  VideoDecoder_NewInputFrame(void* pDecoder, char* data, int length);
        public static extern void VideoDecoder_NewInputFrame64(IntPtr pDecoder, byte[] data, Int32 length, UInt64 timestamp = 0);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_DecodedFrameReady")]
        // VGLIB_EXPORT bool  VideoDecoder_DecodedFrameReady(void* pDecoder);
        public static extern void VideoDecoder_DecodedFrameReady64(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_GetNextDecodedFrame")]
        // VGLIB_EXPORT bool VideoDecoder_GetNextDecodedFrame(void* pDecoder, char** ppData, int_t* pNumBytes,  // returns data from the oldest frame in queue
        //                                                    int_t* pWidth, int_t* pHeight,
        //                                                    int_t* pFormat, uint64_t* pTimestamp);
        public static extern int VideoDecoder_GetNextDecodedFrame64(IntPtr pDecoder, out IntPtr pData, out int numBytes, out int width, 
                                                                       out int height, out int format, out UInt64 timeStamp );


        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_ReleaseFrame")]    
        // VGLIB_EXPORT void  VideoDecoder_ReleaseFrame(void* pDecoder);  // release a frame position in queue for re-use
        public static extern void VideoDecoder_ReleaseFrame(IntPtr pDecoder);

        
        // HUH? void* ppData not void** ppData
        // VGLIB_EXPORT bool  VideoDecoder_GetNextDecodedFrameGpu(void* pDecoder, void* ppData, int_t* pNumBytes,  // returns data from the oldest frame in queue
        //                                                        int_t* pWidth, int_t* pHeight,
        //                                                        int_t* pFormat, uint64_t* pTimestamp);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_GetNextDecodedFrameGpu")]
        //VGLIB_EXPORT bool  VideoDecoder_GetNextDecodedFrameGpu(void* pDecoder, void* ppData, uint32_t* pNumBytes,  // returns data from the oldest frame in queue
        //                                                uint32_t* pWidth, uint32_t* pHeight,
        //                                                uint32_t* pFormat, uint64_t* pTimestamp);
        public static extern int VideoDecoder_GetNextDecodedFrameGPU64(IntPtr pDecoder, out IntPtr pData, out int numBytes, out int width,
                                                                        out int height, out int format, out UInt64 timeStamp);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_ReleaseFrameGpu")]
        //VGLIB_EXPORT void  VideoDecoder_ReleaseFrameGpu(void* pDecoder);  // release a frame position in queue for re-use
        public static extern void VideoDecoder_ReleaseFrameGPU64(IntPtr pDecoder);


        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_PeekNextGpuMat")]
        // VGLIB_EXPORT void* VideoDecoder_PeekNextGpuMat(void* pDecoder, uint64_t *timestamp);
        public static extern IntPtr VideoDecoder_PeekNextGpuMat(IntPtr pDecoder, out UInt64 timeStamp);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_PopNextGpuMat")]
        // VGLIB_EXPORT bool  VideoDecoder_PopNextGpuMat(void* pDecoder);
        public static extern int VideoDecoder_PopNextGpuMat(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_GetDecodedImageSize")]
        // VGLIB_EXPORT void  VideoDecoder_GetDecodedImageSize(void* pDecoder, int_t *pWidth, int_t *pHeight);
        public static extern void VideoDecoder_GetDecodedImageSize64(IntPtr pDecoder, out int width, out int height);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Free")]
        // VGLIB_EXPORT void  VideoDecoder_Free(void* pDecoder);
        public static extern void VideoDecoder_Free64(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Input_GetFramesIn")]
        // VGLIB_EXPORT int_t VideoDecoder_InputQueue_GetFramesIn(void* pDecoder);
        public static extern int  VideoDecoder_InputQueue_GetFramesIn(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Input_GetFramesOut")]
        // VGLIB_EXPORT int_t  VideoDecoder_InputQueue_GetFramesOut(void* pDecoder);
        public static extern int  VideoDecoder_InputQueue_GetFramesOut(IntPtr pDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_SetSkipCount")]
        // VGLIB_EXPORT void VideoDecoder_SetSkipCount(void* pDecoder, int count);
        public static extern void VideoDecoder_SetSkipCount(IntPtr pDecoder, int count);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_ConvertToEdges")]
        // VGLIB_EXPORT void VideoDecoder_ConvertToEdges(void* pDecoder, void* pGpuMat);
        public static extern void VideoDecoder_ConvertToEdges(IntPtr pDecoder, IntPtr pGpuMat);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_Flush")]
        // VGLIB_EXPORT void VideoDecoder_Flush(void* pVideoDecoder);
        public static extern void VideoDecoder_Flush64(IntPtr pVideoDecoder);


        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoDecoder_CopyGpuToCpu")]
        //VGLIB_EXPORT bool  VideoDecoder_CopyGpuToCpu(void *pDecoder, char* pDestCpu, void* pSourceGpu, uint32_t numBytes);
        public static extern void VideoDecoder_CopyGpuToCpu(IntPtr pVideoDecoder, IntPtr pDestCpu, IntPtr pSourceGpu, UInt32 numBytes);


         //////////////////////////////////////////////////////////////
        // DirectX interface
        //////////////////////////////////////////////////////////////

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "DX_CopyImageToSurface")]
        //VGLIB_EXPORT bool DX_CopyImageToSurface(void* pCuda, int SurfaceIndex, void* ImageData);
        public static extern bool DX_GpuCopyImageToSurface(IntPtr pCuda, int SurfaceIndex, IntPtr ImageData);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "DX_RemoveD3DSurface")]
        //VGLIB_EXPORT bool DX_RemoveD3DSurface(void* pCuda, int SurfaceIndex);
        public static extern bool DX_GpuRemoveD3DSurface(IntPtr pCuda, int SurfaceIndex);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "DX_AddD3DSurface")]
        //VGLIB_EXPORT bool DX_AddD3DSurface(void* pCuda, int SurfaceIndex, void* pSurface, int width, int height);
        public static extern bool DX_GpuAddD3DSurface(IntPtr pCuda, int SurfaceIndex, IntPtr pSurface, int width, int height);



        //////////////////////////////////////////////////////////////
        // VideoEncoder interface
        //////////////////////////////////////////////////////////////

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_Create")]
        // VGLIB_EXPORT void* VideoEncoder_Create(void* pVideoDecoder);
        public static extern IntPtr VideoEncoder_Create64(IntPtr pVideoDecoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_Free")]
        // VGLIB_EXPORT void  VideoEncoder_Free(void* pVideoEncoder);
        public static extern void VideoEncoder_Free64(IntPtr pEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_Start")]
        // VGLIB_EXPORT void  VideoEncoder_Start(void* pVideoEncoder);
        public static extern void VideoEncoder_Start64(IntPtr pVideoEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_Stop")]
        // VGLIB_EXPORT void  VideoEncoder_Stop(void* pVideoEncoder);
        public static extern void VideoEncoder_Stop64(IntPtr pVideoEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_ConfigureEncoder")]
        // VGLIB_EXPORT void  VideoEncoder_ConfigureEncoder(void* pVideoEncoder, unsigned int bitRate, int frameRate, int mode,
        //                                         int gopLength,
        //                                         int invalidateRefFramesEnableFlag,
        //                                         int intraRefreshEnableFlag,
        //                                         int intraRefreshPeriod,
        //                                         int intraRefreshDuration);
        public static extern void VideoEncoder_ConfigureEncoder64(IntPtr pVideoEncoder, int bitRate, Int32 frameRate, Int32 mode, Int32 outputMode,
            Int32 gopLength, Int32 invalidateRefFramesEnableFlag, Int32 intraRefreshEnableFlag, Int32 intraRefreshPeriod, Int32 intraRefreshDuration);


        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_GetWindowsHandles")]
        // VGLIB_EXPORT void  VideoEncoder_GetWindowsHandles(void* pVideoEncoder, HANDLE *pEncoderStoppedEvent);
        public static extern void VideoEncoder_GetWindowsHandles64(IntPtr pVideoEncoder, out IntPtr pEncoderStoppedEvent, out IntPtr pEncoderOutputQueueSemaphore);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_SetOutputFilename", CharSet = CharSet.Unicode)]
        // VGLIB_EXPORT void  VideoEncoder_SetOutputFilename(void* pVideoSink, char* filename);
        public static extern void VideoEncoder_SetOutputFilename64(IntPtr pVideoEncoder, [MarshalAs(UnmanagedType.LPStr)] string filename);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_SetOutputQueueMaxSize")]
        // VGLIB_EXPORT void  VideoEncoder_SetOutputQueueMaxSize(void* pVideoSink, int count);
        public static extern void VideoEncoder_SetOutputQueueMaxSize64(IntPtr pVideoEncoder, Int32 count);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_GetNextEncodedFrame")]
        // VGLIB_EXPORT int VideoEncoder_GetNextEncodedFrame(void* pVideoSink, char* pBuffer, int_t maxBytes);
        public static extern int VideoEncoder_GetNextEncodedFrame64(IntPtr pVideoEncoder, out IntPtr frame, out int size);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_ReleaseFrame")]
        // VGLIB_EXPORT void  VideoEncoder_ReleaseFrame(void* pVideoEncoder);
        public static extern void VideoEncoder_ReleaseFrame64(IntPtr pVideoEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_OutputQueue_GetFramesIn")]
        // VGLIB_EXPORT int  VideoEncoder_OutputQueue_GetFramesIn(void* pVideoEncoder);
        public static extern int VideoEncoder_OutputQueue_FrameFramesIn64(IntPtr pVideoEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_OutputQueue_GetFramesIn")]
        // VGLIB_EXPORT int  VideoEncoder_OutputQueue_GetFramesOut(void* pVideoEncoder);
        public static extern int VideoEncoder_OutputQueue_FrameFramesOut64(IntPtr pVideoEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_Flush")]
        // VGLIB_EXPORT int VideoEncoder_Flush(void* pVideoEncoder);
        public static extern int VideoEncoder_Flush64(IntPtr pVideoEncoder);

        [DllImport(VGLIBDLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "VideoEncoder_SetJpegQuality")]
        // VGLIB_EXPORT void VideoEncoder_SetJpegQuality(void* pVideoEncoder, uint32_t quality)
        public static extern void VideoEncoder_SetJpegQuality(IntPtr pVideoEncoder, int quality);

        public static UInt64 GetFreeMemory()
        {
            // Gets Device 0 memory - need to code for additional cards
            UInt64 totalMem;
            UInt64 freeMem = 0;
            IntPtr cuda;
            if ((cuda = Cuda_Create64()) != IntPtr.Zero)
            {
                Cuda_GetDeviceMemory64(cuda, out totalMem, out freeMem);
                Cuda_Free64(cuda);
            }
            return freeMem;
        }

        public static NVIDIAChipset Chipset
        {
            get 
            {
                NVIDIAChipset cs = NVIDIAChipset.Unsupported;
                if (CudaAvailable)
                {
                    IntPtr cuda;
                    if ((cuda = Cuda_Create64()) != IntPtr.Zero)
                    {
                        int major, minor;
                        Cuda_GetComputeCapability64(cuda, out major, out minor);
                        if (major > 4)
                        {
                            // At least 1st generation maxwell chipset
                            if (major == 5)
                            {
                                if (minor == 0)
                                {
                                    // 1st generation maxwell chipset
                                    cs = NVIDIAChipset.MaxwellGen1;
                                }
                                else if (minor == 2)
                                {
                                    // 2nd generation maxwell chipset (H.265 support)
                                    cs = NVIDIAChipset.MaxwellGen2;
                                }
                                else
                                {
                                    // > than 2nd generation maxwell chipset
                                    cs = NVIDIAChipset.AtLeastMaxwellGen2;
                                }
                            }
                            else
                            {
                                // > than 2nd generation maxwell chipset
                                cs = NVIDIAChipset.AtLeastMaxwellGen2;
                            }
                        }
                        Cuda_Free64(cuda);
                    }
                }
                return cs;
            }
        }

        public static int GetDeviceCount()
        {
            int deviceCount = 0;
            IntPtr cuda;
            if ((cuda = Cuda_Create64()) != IntPtr.Zero)
            {
                deviceCount = Cuda_GetDeviceCount64(cuda);
                Cuda_Free64(cuda);
            }
            return deviceCount;
        }

        public static bool CudaAvailable
        {
            get
            {
                bool Success = false;
                IntPtr cuda;
                if ((cuda = Cuda_Create64()) != IntPtr.Zero)
                {
                    Success = true;
                    Cuda_Free64(cuda);
                }
                return Success;
            }
        }

        public static string GetDeviceName()
        {
            string deviceName = null;
            IntPtr cuda;
            if ((cuda = Cuda_Create64()) != IntPtr.Zero)
            {
                byte[] devName = new byte[128];
                Cuda_GetDeviceName64(cuda, devName);
                deviceName = Encoding.ASCII.GetString(devName).Trim(new char[] { '\0' });
                Cuda_Free64(cuda);
            }
            return deviceName;
        }
        

    }


    public class CudaDecodingSession
    {
        private IntPtr m_cuda;
        private IntPtr m_cudaContext;
        private IntPtr m_videoDecoder;

        private int m_width;
        private int m_height;
 
        private byte[] decodedFrame;

        private Semaphore m_InputQueueSemaphore;
        private Semaphore m_OutputQueueSemaphore;

        public CudaDecodingSession(int width, int height)
        {
            m_cuda = IntPtr.Zero;
            m_cudaContext = IntPtr.Zero;

            m_width = width;
            m_height = height;

        }

        ~CudaDecodingSession()
        {
            CloseDecoder();
        }

        public void CloseDecoder()
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

        }

        public Semaphore OutputQueueSemaphore
        {
            get { return m_OutputQueueSemaphore; }
        }

        public void AddCompressedFrame(byte[] frame, int width, int height, byte keyFlag)
        {
            CudaTools.VideoDecoder_NewInputFrame64(m_videoDecoder, frame, frame.Length);
        }

        public void SetSkipCount(int skipCount)
        {
            CudaTools.VideoDecoder_SetSkipCount(m_videoDecoder, skipCount);
        }

        public void Stop()
        {
            CudaTools.VideoDecoder_Stop64(m_videoDecoder);
        }

        public void EndDecoding()
        {
            InputNewCompressedFrame(new byte[0]);
        }

        public byte[] GetUncompressedFrame()
        {
            byte[] result = null;
            // Get the new frame
            if (decodedFrame == null)
            {
                decodedFrame = new byte[m_width * m_height * 4];
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


        public IntPtr GetUncompressedFrameGPU()
        {
            int width;
            int height;
            int format;
            UInt64 timeStamp;
            IntPtr pFrame;
            int numBytes;
            CudaTools.VideoDecoder_GetNextDecodedFrameGPU64(m_videoDecoder, out pFrame, out numBytes, out width, out height, out format, out timeStamp);

            return IntPtr.Zero;
        }


        public bool Init(CudaTools.CODEC codec = CudaTools.CODEC.H264, CudaTools.DECODER_MODE decoderMode = CudaTools.DECODER_MODE.CPU_ARGB, int skipCount = 1)
        {
            bool retVal = false;
            CloseDecoder();
            if ((m_cuda = CudaTools.Cuda_Create64()) != IntPtr.Zero)
            {
                ulong t;
                ulong f;
                CudaTools.Cuda_GetDeviceMemory64(m_cuda, out t, out f);
                if (CudaTools.Cuda_GetContext64(m_cuda, out m_cudaContext) == true)
                {
                    if ((m_videoDecoder = CudaTools.VideoDecoder_Create64(m_cudaContext)) != IntPtr.Zero)
                    {
                        IntPtr semaphoreInput;
                        IntPtr semaphoreOutput;
                        IntPtr eventStopped;
                        CudaTools.VideoDecoder_GetWindowsHandles64(m_videoDecoder, out semaphoreInput, out semaphoreOutput, out eventStopped);

                        m_InputQueueSemaphore = new Semaphore(0, int.MaxValue);
                        m_InputQueueSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreInput, false);

                        m_OutputQueueSemaphore = new Semaphore(0, int.MaxValue);
                        m_OutputQueueSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreOutput, false);


                        if (CudaTools.VideoDecoder_Init64(m_videoDecoder))
                        {
                            CudaTools.VideoDecoder_ConfigureDecoder64(m_videoDecoder, (int)m_width, (int)m_height, (int)decoderMode, (int)codec);
                            CudaTools.VideoDecoder_SetSkipCount(m_videoDecoder, skipCount);
                            CudaTools.VideoDecoder_Start64(m_videoDecoder);
                            retVal = true;
                        }
                    }
                }
            }
            return retVal;
        }

        public bool InputNewCompressedFrame(byte[] frame)
        {
            bool success = false;
            if (m_InputQueueSemaphore.WaitOne(5000))
            {
                CudaTools.VideoDecoder_NewInputFrame64(m_videoDecoder, frame, frame.Length);
                success = true;
            }
            return success;
        }

        public byte[] Decode(byte[] frame, int width, int height, byte keyFlag)
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
                    InputNewCompressedFrame(frame);
                    if (m_OutputQueueSemaphore.WaitOne(0))
                    {
                        result = GetUncompressedFrame();
                    }

                }
            }
            return result;
        }
    }




    public class CudaTranscoder
    {
        private IntPtr m_cuda;
        private IntPtr m_cudaContext;
        private IntPtr m_videoDecoder;
        private IntPtr m_videoEncoder;
     


        private int m_outputWidth;
        private int m_outputHeight;

        private Semaphore m_InputQueueSemaphore;
        private Semaphore m_OutputQueueSemaphore;
        private Semaphore m_OutputQueueSinkSemaphore;
        private Semaphore m_OutputQueueEncoderSemaphore;

        private int count = 0;
        private int dropped = 0;

        private byte[] m_compressedFrame = new byte[0];
        private byte[] m_frameStore = null;

        public byte[] CompressedFrame
        {
            get { return m_frameStore; }
        }

        public CudaTranscoder()
        {
            m_cuda = IntPtr.Zero;
            m_cudaContext = IntPtr.Zero;
            m_videoDecoder = IntPtr.Zero;
            m_videoEncoder = IntPtr.Zero;
       
            m_outputWidth = 0;
            m_outputHeight = 0;

        }

        ~CudaTranscoder()
        {
            FreeEncoder();
            FreeDecoder();
            FreeCuda();
        }

        public bool NewInputFrame(byte[] frame, int length, int msWait = 10000)
        {
            bool retVal = false;
            if (m_InputQueueSemaphore.WaitOne(msWait))
            {
                CudaTools.VideoDecoder_NewInputFrame64(m_videoDecoder, frame, length);
                ++count;
                retVal = true;
            }
            else
            {
                ++dropped;
            }
            return retVal;
        }

        public bool GetOutputFrame(WaitHandle hExit, int msWait = 0)
        {
            bool success = false;
            switch(WaitHandle.WaitAny(new WaitHandle[] { m_OutputQueueEncoderSemaphore, hExit }, msWait))
            {
                case 0:
                    {
                       IntPtr frame;
                       int numBytes;
                        
                       int haveFrame = CudaTools.VideoEncoder_GetNextEncodedFrame64(m_videoEncoder, out frame, out numBytes);
                        //Debug.Print(size.ToString());
                       if (haveFrame == 1)
                       {
                           m_frameStore = new byte[numBytes];
                           Marshal.Copy(frame, m_frameStore, 0, (int)numBytes);
                           CudaTools.VideoEncoder_ReleaseFrame64(m_videoEncoder);
                           success = true;
                       }
                    }
                    break;
                
                case 1:
                    break;

                case WaitHandle.WaitTimeout:
                    break;
            }
            return success;
        }

        public void Stop()
        {
            if (m_videoDecoder != IntPtr.Zero)
            {
                CudaTools.VideoDecoder_Stop64(m_videoDecoder);
            }
            if (m_videoEncoder != IntPtr.Zero)
            {
                CudaTools.VideoEncoder_Stop64(m_videoEncoder);
            }
        }

        public void FreeCuda()
        {
            if (m_cuda != IntPtr.Zero)
            {
                CudaTools.Cuda_Free64(m_cuda);
                m_cuda = IntPtr.Zero;
            }
        }

        public bool InitCuda()
        {
            bool Success = m_cuda != IntPtr.Zero;
            if (!Success)
            {
                if ((m_cuda = CudaTools.Cuda_Create64()) != IntPtr.Zero)
                {
                    if (CudaTools.Cuda_GetContext64(m_cuda, out m_cudaContext) == true)
                    {
                        Success = true;
                    }
                }
            }
            return Success;
        }


        public void FreeDecoder()
        {
            if (m_videoDecoder != IntPtr.Zero)
            {
                CudaTools.VideoDecoder_Free64(m_videoDecoder);
                m_videoDecoder = IntPtr.Zero;
            }
        }

        public bool InitDecoder(CudaTools.DECODER_MODE modeDecoder, int outputWidth, int outputHeight, CudaTools.CODEC codec = CudaTools.CODEC.H264)
        {
            bool Success = InitCuda();
            if (Success)
            {
                FreeDecoder();
                if ((m_videoDecoder = CudaTools.VideoDecoder_Create64(m_cudaContext)) != IntPtr.Zero)
                {
                    IntPtr semaphoreInput;
                    IntPtr semaphoreOutput;
                    IntPtr eventStopped;
                    CudaTools.VideoDecoder_GetWindowsHandles64(m_videoDecoder, out semaphoreInput, out semaphoreOutput, out eventStopped);

                    m_InputQueueSemaphore = new Semaphore(0, int.MaxValue);
                    m_InputQueueSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreInput, false);

                    m_OutputQueueSemaphore = new Semaphore(0, int.MaxValue);
                    m_OutputQueueSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreOutput, false);

                    if (Success = CudaTools.VideoDecoder_Init64(m_videoDecoder))
                    {
                        CudaTools.VideoDecoder_ConfigureDecoder64(m_videoDecoder, outputWidth, outputHeight, (int)modeDecoder, (int)codec);
                        Success = true;
                    }
                }
            }
            return Success;
        }

        public void FreeEncoder()
        {
            if (m_videoEncoder != IntPtr.Zero)
            {
                CudaTools.VideoEncoder_Free64(m_videoEncoder);
                m_videoEncoder = IntPtr.Zero;
            }

        }

        public bool InitEncoder(CudaTools.ENCODER_MODE modeEncoder, CudaTools.VIDEOSINK_MODE modeSink, int bitRate, int frameRate, int gopLength,   
                                int invalidateRefFramesEnableFlag,
                                int intraRefreshEnableFlag,
                                int intraRefreshPeriod,
                                int intraRefreshDuration)
        {
            bool Success = false;
            FreeEncoder();
            if ((m_videoEncoder = CudaTools.VideoEncoder_Create64(m_videoDecoder)) != IntPtr.Zero)
            {
                CudaTools.VideoEncoder_ConfigureEncoder64(m_videoEncoder, bitRate, (int)frameRate, (int)modeEncoder, (int)modeSink, (int)gopLength, (int)invalidateRefFramesEnableFlag,
                                                          (int)intraRefreshEnableFlag, (int)intraRefreshPeriod, (int)intraRefreshDuration);
                Success = true;
            }
            return Success;
        }

        public void SetOutputFile(string fileName)
        {
            CudaTools.VideoEncoder_SetOutputFilename64(m_videoEncoder, fileName);
        }

        public void Start()
        {
            CudaTools.VideoDecoder_Start64(m_videoDecoder);
            CudaTools.VideoEncoder_Start64(m_videoEncoder);
        }

        public bool InitTranscodeToJPEG(int outputWidth, int outputHeight)
        {
            return Init(CudaTools.DECODER_MODE.GPU_RGB,
                        CudaTools.ENCODER_MODE.ENCODER_TO_JPEG,
                        CudaTools.VIDEOSINK_MODE.OUTPUT_QUEUE,
                        outputWidth, outputHeight, 0, 1, 0);
        }

        public bool InitTranscodeToH264_MP4(string filename, 
                         int outputWidth, int outputHeight, 
                         int bitRate, int frameRate, int gopLength,   
                         int invalidateRefFramesEnableFlag = 0,
                         int intraRefreshEnableFlag = 1,
                         int intraRefreshPeriod = 5,
                         int intraRefreshDuration = 5)
        {
            bool retVal =  Init(CudaTools.DECODER_MODE.TO_ENCODER,
                        CudaTools.ENCODER_MODE.ENCODER_TO_H264,
                        CudaTools.VIDEOSINK_MODE.MP4,
                        outputWidth, outputHeight, bitRate, frameRate, gopLength,
                        invalidateRefFramesEnableFlag, 
                        intraRefreshDuration, intraRefreshPeriod, intraRefreshDuration);
            if (retVal)
            {
                SetOutputFile(filename);
            }
            return retVal;
        }

        public bool InitTranscodeToH264_Queue(int outputWidth, int outputHeight,
                         int bitRate, int frameRate, int gopLength,
                         int invalidateRefFramesEnableFlag = 0,
                         int intraRefreshEnableFlag = 1,
                         int intraRefreshPeriod = 5,
                         int intraRefreshDuration = 5)
        {
            bool retVal = Init(CudaTools.DECODER_MODE.TO_ENCODER,
                        CudaTools.ENCODER_MODE.ENCODER_TO_H264,
                        CudaTools.VIDEOSINK_MODE.OUTPUT_QUEUE,
                        outputWidth, outputHeight, bitRate, frameRate, gopLength,
                        invalidateRefFramesEnableFlag,
                        intraRefreshDuration, intraRefreshPeriod, intraRefreshDuration);
            return retVal;
        }

        public bool InitTranscodeToH264_AVI(string filename,
                 int outputWidth, int outputHeight,
                 int bitRate, int frameRate, int gopLength,
                 int invalidateRefFramesEnableFlag = 0,
                 int intraRefreshEnableFlag = 1,
                 int intraRefreshPeriod = 5,
                 int intraRefreshDuration = 5)
        {
            bool retVal = Init(CudaTools.DECODER_MODE.TO_ENCODER,
                        CudaTools.ENCODER_MODE.ENCODER_TO_H264,
                        CudaTools.VIDEOSINK_MODE.AVI,
                        outputWidth, outputHeight, bitRate, frameRate, gopLength,
                        invalidateRefFramesEnableFlag,
                        intraRefreshDuration, intraRefreshPeriod, intraRefreshDuration);
            if (retVal)
            {
                SetOutputFile(filename);
            }
            return retVal;
        }

        public bool Init(CudaTools.DECODER_MODE modeDecoder,
                         CudaTools.ENCODER_MODE modeEncoder,
                         CudaTools.VIDEOSINK_MODE modeSink, 
                         int outputWidth, int outputHeight, 
                         int bitRate, int frameRate, int gopLength,   
                         int invalidateRefFramesEnableFlag = 0,
                         int intraRefreshEnableFlag = 1,
                         int intraRefreshPeriod = 5,
                         int intraRefreshDuration = 5)
        {
            bool success = false;
            if(success = InitDecoder(modeDecoder, outputWidth, outputHeight))
            {
                if(success = InitEncoder(modeEncoder, modeSink, bitRate, frameRate, gopLength, invalidateRefFramesEnableFlag, 
                                         intraRefreshEnableFlag, intraRefreshPeriod, intraRefreshDuration))
                {
                    if(success)
                    {
 
                        IntPtr semaphoreOutput;
                        IntPtr eventStopped;
                        CudaTools.VideoEncoder_GetWindowsHandles64(m_videoEncoder, out eventStopped, out semaphoreOutput);

                        m_OutputQueueEncoderSemaphore = new Semaphore(0, int.MaxValue);
                        m_OutputQueueEncoderSemaphore.SafeWaitHandle = new SafeWaitHandle(semaphoreOutput, false);

                    }
                }
            }
            return success;
        }

        public void Flush()
        {
            CudaTools.VideoDecoder_Flush64(m_videoDecoder);
        }

        public void SetJpegQuality(int jpegQuality)
        {
            CudaTools.VideoEncoder_SetJpegQuality(m_videoEncoder, jpegQuality);
        }

        public void EndTranscoding()
        {
            NewInputFrame(new byte[0], 0);
        }

    }

}
