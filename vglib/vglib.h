#ifndef DLLEXPORTS_H
#define DLLEXPORTS_H


#include <stdint.h>
#include <functional>


#ifndef HANDLE
typedef void * HANDLE;
#endif



#ifdef __cplusplus
extern "C" {
#endif

//#ifdef COMPILE_VGLIB
//#define VGLIB_EXPORT __declspec(dllexport)
//#else
//#define VGLIB_EXPORT __declspec(dllimport)
//#endif

#define VGLIB_EXPORT __declspec(dllexport)


//////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////
///
///                     DLL Exports
///
//////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////



//////////////////////////////////////////////////////////////
///     CUDA interface

VGLIB_EXPORT void  Cuda_DeviceReset();

VGLIB_EXPORT void* Cuda_Create();
// returns pointer to Cuda_Util object needed for all other Cuda functions (pCuda)

VGLIB_EXPORT void  Cuda_Free(void* pCuda);

VGLIB_EXPORT int   Cuda_GetDeviceCount(void* pCuda);

VGLIB_EXPORT void  Cuda_GetComputeCapability(void* pCuda, int* major, int* minor);

VGLIB_EXPORT void  Cuda_GetDeviceName(void* pCuda, char* deviceName);

VGLIB_EXPORT void  Cuda_GetDeviceMemory(void* pCuda, unsigned long* totalMem, unsigned long* freeMem);

VGLIB_EXPORT bool  Cuda_GetContext(void* pCuda, void **pCudaCtx);

VGLIB_EXPORT bool  Cuda_IsReady(void* pCuda);

VGLIB_EXPORT void  Cuda_SetContext(void* pCuda, void* pCudaCtx);

VGLIB_EXPORT bool  Cuda_CopyDataFromGpu(void* pCuda, void* cpuDest, void* gpuSource, int numBytes);

//////////////////////////////////////////////////////////////
//// VideoDecoder interface

VGLIB_EXPORT void* VideoDecoder_Create(void *pCudaContext, uint32_t codec);
// the value returned by this function is used for pDecoder in all of the following functions for VideoDecoder

VGLIB_EXPORT bool  VideoDecoder_Init(void *pDecoder);

VGLIB_EXPORT void  VideoDecoder_GetWindowsHandles(void *pDecoder, HANDLE *pInputQueueSpaceAvailableSemaphore,
                                                                  HANDLE *pOutputQueueSemaphore,
                                                                  HANDLE *pDecoderStoppedEvent);

VGLIB_EXPORT void  VideoDecoder_Start(void *pDecoder);

VGLIB_EXPORT void  VideoDecoder_Stop(void *pDecoder);

VGLIB_EXPORT void  VideoDecoder_GetLastErrorMsg(void *pDecoder, char* errMsg);
// errMsg = pointer to char array.
//          assumes errMsg is a pre-allocated buffer, 120 bytes should be plenty big.
//          the error message is put into this array and is null terminated.

typedef void (__stdcall * DecodedFrameReadyCallback)(char* frame, int size, int width, int height,
                                                     int colorspace, void* receiver);
typedef void (__stdcall * DecoderFinishedCallback)();

VGLIB_EXPORT void  VideoDecoder_ConfigureDecoder(void* pDecoder, int outputWidth, int outputHeight, int mode, int codec);
// codec values:
//cudaVideoCodec_MPEG1=0,
//cudaVideoCodec_MPEG2,
//cudaVideoCodec_MPEG4,
//cudaVideoCodec_VC1,
//cudaVideoCodec_H264,
//cudaVideoCodec_JPEG,
//cudaVideoCodec_H264_SVC,
//cudaVideoCodec_H264_MVC,
//cudaVideoCodec_HEVC,
//cudaVideoCodec_VP8,
//cudaVideoCodec_VP9,
//cudaVideoCodec_NumCodecs,
//// Uncompressed YUV:
//cudaVideoCodec_YUV420 = (('I'<<24)|('Y'<<16)|('U'<<8)|('V')),   // Y,U,V (4:2:0)
//cudaVideoCodec_YV12   = (('Y'<<24)|('V'<<16)|('1'<<8)|('2')),   // Y,V,U (4:2:0)
//cudaVideoCodec_NV12   = (('N'<<24)|('V'<<16)|('1'<<8)|('2')),   // Y,UV  (4:2:0)
//cudaVideoCodec_YUYV   = (('Y'<<24)|('U'<<16)|('Y'<<8)|('V')),   // YUYV/YUY2 (4:2:2)
//cudaVideoCodec_UYVY   = (('U'<<24)|('Y'<<16)|('V'<<8)|('Y'))    // UYVY (4:2:2)


// after the call to VideoDecoder_NewInputFrame(...), a copy is made, so you can release the data (i.e. call delete data)
VGLIB_EXPORT void  VideoDecoder_NewInputFrame(void* pDecoder, char* data, int length, uint64_t timestamp);

VGLIB_EXPORT void VideoDecoder_Flush(void* pDecoder);

VGLIB_EXPORT bool  VideoDecoder_DecodedFrameReady(void* pDecoder);

VGLIB_EXPORT bool VideoDecoder_GetNextDecodedFrame(void* pDecoder, char** ppData, uint32_t* pNumBytes,  // returns data from the oldest frame in queue
                                                        uint32_t* pWidth, uint32_t* pHeight,
                                                        uint32_t* pFormat, uint64_t* pTimestamp);

VGLIB_EXPORT void  VideoDecoder_ReleaseFrame(void* pDecoder);  // release a frame position in queue for re-use

VGLIB_EXPORT bool  VideoDecoder_GetNextDecodedFrameGpu(void* pDecoder, void* ppData, uint32_t* pNumBytes,  // returns data from the oldest frame in queue
                                                        uint32_t* pWidth, uint32_t* pHeight,
                                                        uint32_t* pFormat, uint64_t* pTimestamp);

VGLIB_EXPORT void  VideoDecoder_ReleaseFrameGpu(void* pDecoder);  // release a frame position in queue for re-use

VGLIB_EXPORT void* VideoDecoder_PeekNextGpuMat(void* pDecoder, uint64_t *timestamp);

VGLIB_EXPORT bool  VideoDecoder_PopNextGpuMat(void* pDecoder);

VGLIB_EXPORT void  VideoDecoder_GetDecodedImageSize(void* pDecoder, uint32_t *pWidth, uint32_t *pHeight);

VGLIB_EXPORT void  VideoDecoder_Free(void* pDecoder);

VGLIB_EXPORT uint32_t  VideoDecoder_InputQueue_GetFramesIn(void* pDecoder);

VGLIB_EXPORT uint32_t  VideoDecoder_InputQueue_GetFramesOut(void* pDecoder);

VGLIB_EXPORT void  VideoDecoder_SetSkipCount(void* pDecoder, int count);

VGLIB_EXPORT void VideoDecoder_InitD3D9(void *pDecoder, void* vpWnd, int width, int height);

VGLIB_EXPORT void VideoDecoder_CleanUpD3D9(void *pDecoder);

VGLIB_EXPORT void VideoDecoder_SetD3D9Window(void *pDecoder, void* vpWnd, int width, int height);

VGLIB_EXPORT bool  VideoDecoder_CopyGpuToCpu(void *pDecoder, char* pDestCpu, void* pSourceGpu, uint32_t numBytes);

//////////////////////////////////////////////////////////////
// VideoEncoder interface

VGLIB_EXPORT void* VideoEncoder_Create(void* pVideoDecoder);

VGLIB_EXPORT void  VideoEncoder_Free(void* pVideoEncoder);

VGLIB_EXPORT void  VideoEncoder_Start(void* pVideoEncoder);

VGLIB_EXPORT void  VideoEncoder_Stop(void* pVideoEncoder);

VGLIB_EXPORT void  VideoEncoder_ConfigureEncoder(void* pVideoEncoder, unsigned int bitRate, int frameRate, int mode, int outputMode,
                                                 int gopLength,
                                                 int invalidateRefFramesEnableFlag,
                                                 int intraRefreshEnableFlag,
                                                 int intraRefreshPeriod,      
                                                 int intraRefreshDuration);
// mode values:
//enum ENCODER_MODE {
//    ENCODER_TO_H264 = 0,
//    ENCODER_TO_HEVC,  // H265
//    ENCODER_TO_JPEG
//};

// outputMode values:
//enum OUTPUT_MODE {
//    OUTPUT_MP4_FILE = 0,
//    OUTPUT_AVI_FILE,
//    OUTPUT_QUEUE
//};

VGLIB_EXPORT void  VideoEncoder_GetWindowsHandles(void* pVideoEncoder, HANDLE *pEncoderStoppedEvent, HANDLE *pEncoderOutputQueueSemaphore);

VGLIB_EXPORT void  VideoEncoder_SetOutputFilename(void* pVideoEncoder, char* filename);

VGLIB_EXPORT bool VideoEncoder_GetNextEncodedFrame(void* pVideoEncoder, char **ppBuffer, uint32_t *pNumBytes);

VGLIB_EXPORT void  VideoEncoder_ReleaseFrame(void* pVideoEncoder);


VGLIB_EXPORT uint32_t  VideoEncoder_OutputQueue_GetFramesIn(void* pVideoEncoder);

VGLIB_EXPORT uint32_t  VideoEncoder_OutputQueue_GetFramesOut(void* pVideoEncoder);

VGLIB_EXPORT uint32_t  VideoEncoder_Flush(void* pVideoEncoder);

VGLIB_EXPORT void VideoEncoder_SetJpegQuality(void* pVideoEncoder, uint32_t quality);


//////////////////////////////////////////////////////////////



VGLIB_EXPORT bool DX_CopyImageToSurface(void* pCuda, int SurfaceIndex, void* ImageData);
VGLIB_EXPORT bool DX_RemoveD3DSurface(void* pCuda, int SurfaceIndex);
VGLIB_EXPORT bool DX_AddD3DSurface(void* pCuda, int SurfaceIndex, void* pSurface, int width, int height);




#ifdef __cplusplus
}
#endif

#endif // DLLEXPORTS_H
