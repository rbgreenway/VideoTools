#include "vglib.h"

#include "cudautil.h"
#include "videodecoder.h"
#include "videoencoder.h"
#include "videosink.h"
#include "gpumatfifo.h"




//////////////////////////////////////////////////////////////
///     CUDA interface
///


VGLIB_EXPORT void Cuda_DeviceReset()
{
    cudaDeviceReset();    
}

VGLIB_EXPORT void* Cuda_Create()
{
    return reinterpret_cast<void*>(new CudaUtil());
}

VGLIB_EXPORT void Cuda_Free(void* pCuda)
{
    CudaUtil * pcu = reinterpret_cast<CudaUtil*>(pCuda);

    delete pcu;
}


VGLIB_EXPORT int Cuda_GetDeviceCount(void* pCuda)
{
    CudaUtil* pCudaUtil = reinterpret_cast<CudaUtil*>(pCuda);

    int count = 0;

    if(pCudaUtil)
    {
        bool success = pCudaUtil->GetCudaDeviceCount(count);
        if(!success) count = 0;
    }

    return count;
}


VGLIB_EXPORT void Cuda_GetComputeCapability(void* pCuda, int *major, int *minor)
{
    // cudaUtil = pointer to instance of CudaUtil
    // major    = pointer to int,
    // minor    = pointer to int

    CudaUtil* pCudaUtil = reinterpret_cast<CudaUtil*>(pCuda);

    int count = 0;

    if(pCudaUtil)
    {
        int* maj = reinterpret_cast<int*>(major);
        int* min = reinterpret_cast<int*>(minor);

        bool success = pCudaUtil->GetComputeCapability(*maj, *min);
        if(!success)
        {
            *maj = 0;
            *min = 0;
        }
    }
}


VGLIB_EXPORT void Cuda_GetDeviceName(void* pCuda, char* deviceName)
{
    // cudaUtil   = pointer to instance of CudaUtil
    // deviceName = pointer to char array.
    //              assumes deviceName is a pre-allocated buffer, 80 bytes should be plenty big.
    //              the devicename is put into this array and is null terminated.

    CudaUtil* pCudaUtil = reinterpret_cast<CudaUtil*>(pCuda);

    if(pCudaUtil)
    {
        char* name = reinterpret_cast<char*>(deviceName);
        std::string str;

        bool success = pCudaUtil->GetDeviceName(str);
        if(success)
        {
           str.copy(name,str.length());
           name[str.length()] = 0; // add null terminator
        }
        else
           name[0] = 0;
    }
}


VGLIB_EXPORT void Cuda_GetDeviceMemory(void* pCuda, unsigned long *totalMem, unsigned long *freeMem)
{
    // cudaUtil = pointer to instance of CudaUtil
    // totalMem = pointer to size_t
    // freeMem  = pointer to size_t

    CudaUtil* pCudaUtil = reinterpret_cast<CudaUtil*>(pCuda);

    if(pCudaUtil)
    {
        size_t _total;// = reinterpret_cast<size_t*>(totalMem);
        size_t _free; // = reinterpret_cast<size_t*>(freeMem);

        bool success = pCudaUtil->GetDeviceMemory(_total, _free);
        if(!success)
        {
           _total = 0;
           _free = 0;
        }

        *totalMem = (unsigned long)_total;
        *freeMem = (unsigned long)_free;
    }
}



VGLIB_EXPORT bool Cuda_GetContext(void* pCuda, void **pCudaCtx)
{
    // cudaUtil = pointer to instance of CudaUtil
    // pContext  = pointer to CUcontext
    // pContextLock  = pointer to CUvideoctxlock

    CudaUtil*        l_pCuda    = reinterpret_cast<CudaUtil*>(pCuda);
    CUcontext**      l_pCudaCtx = reinterpret_cast<CUcontext**>(pCudaCtx);

    bool success;

    if(l_pCuda)
    {
        success = l_pCuda->GetContext(l_pCudaCtx);
        if(success)
        {
            pCudaCtx = reinterpret_cast<void**>(l_pCudaCtx);
        }
        else
        {
            pCudaCtx = 0;
        }
    }

    return success;
}


VGLIB_EXPORT bool Cuda_IsReady(void* pCuda)
{
    // cudaUtil = pointer to instance of CudaUtil

    CudaUtil*       pCudaUtil    = reinterpret_cast<CudaUtil*>(pCuda);

    bool ready = true;

    if(pCudaUtil)
    {
        bool success = pCudaUtil->IsCudaReady();
        if(!success)
        {
           ready = false;
        }
    }

    return ready;
}



VGLIB_EXPORT void Cuda_SetContext(void* pCuda, void* pCudaCtx)
{
	CudaUtil*       pCudaUtil = reinterpret_cast<CudaUtil*>(pCuda);
	CUcontext*  l_pCudaCtx = reinterpret_cast<CUcontext*>(pCudaCtx);

	pCudaUtil->SetContext(l_pCudaCtx);
}


VGLIB_EXPORT bool  Cuda_CopyDataFromGpu(void* pCuda, void* cpuDest, void* gpuSource, int numBytes)
{
	CudaUtil*       pCudaUtil = reinterpret_cast<CudaUtil*>(pCuda);

	return pCudaUtil->CopyDataFromGpu(cpuDest, gpuSource, numBytes);
}




//////////////////////////////////////////////////////////////
///     VideoDecoder interface
///


VGLIB_EXPORT void* VideoDecoder_Create(void* pCudaContext, uint32_t codec)
{
    CUcontext* pCuCtx = reinterpret_cast<CUcontext*>(pCudaContext);
    VideoDecoder* pDecoder = new VideoDecoder(*pCuCtx, codec);
    return reinterpret_cast<void*>(pDecoder);
}



VGLIB_EXPORT bool VideoDecoder_Init(void *pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    bool success = vd->Init();

    return success;
}


VGLIB_EXPORT void  VideoDecoder_GetWindowsHandles(void *pDecoder,
                                                  HANDLE *pInputQueueSpaceAvailableSemaphore,
                                                  HANDLE *pOutputQueueSemaphore,
                                                  HANDLE *pDecoderStoppedEvent)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->GetWindowsHandles(pInputQueueSpaceAvailableSemaphore,pOutputQueueSemaphore,pDecoderStoppedEvent);
}


VGLIB_EXPORT void VideoDecoder_Start(void *pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->Start();
}

VGLIB_EXPORT void VideoDecoder_Stop(void *pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->Stop();
}

VGLIB_EXPORT void VideoDecoder_GetLastErrorMsg(void *pDecoder, char *errMsg)
{
    // deviceName = pointer to char array.
    //              assumes deviceName is a pre-allocated buffer, 80 bytes should be plenty big.
    //              the devicename is put into this array and is null terminated.

    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    std::string errStr = vd->GetLastErrorMessage();

    errStr.copy(errMsg,errStr.length());
    errMsg[errStr.length()] = 0; // add null terminator
}


VGLIB_EXPORT void VideoDecoder_ConfigureDecoder(void *pDecoder, int outputWidth, int outputHeight, int mode, int codec)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->ConfigureDecoder(outputWidth, outputHeight, (DECODER_MODE)mode, (cudaVideoCodec)codec);
}



VGLIB_EXPORT void VideoDecoder_NewInputFrame(void* pDecoder, char *data, int length, uint64_t timestamp = 0)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->EnqueueFrame((unsigned char*)data, length, timestamp, false);
}

VGLIB_EXPORT void VideoDecoder_Flush(void* pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->EnqueueFrame((unsigned char*)NULL, 0, 0, true);
}


VGLIB_EXPORT bool VideoDecoder_DecodedFrameReady(void* pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    bool ready = vd->FrameReadyCpu();

    return ready;
}

VGLIB_EXPORT bool VideoDecoder_GetNextDecodedFrame(void* pDecoder, char** ppData, uint32_t* pNumBytes, uint32_t* pWidth, uint32_t* pHeight, uint32_t *pFormat, uint64_t* pTimestamp)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    return vd->GetNextDecodedFrameCpu(ppData,pNumBytes,pWidth,pHeight,(DECODED_IMAGE_FORMAT*)pFormat,pTimestamp);
}


VGLIB_EXPORT void  VideoDecoder_ReleaseFrame(void* pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->ReleaseFrame();
}


VGLIB_EXPORT bool VideoDecoder_GetNextDecodedFrameGpu(void* pDecoder, void* ppData, uint32_t* pNumBytes,  // returns data from the oldest frame in queue
                                                        uint32_t* pWidth, uint32_t* pHeight,
                                                        uint32_t* pFormat, uint64_t* pTimestamp)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    return vd->GetNextDecodedFrameGpu((CUdeviceptr*)ppData,pNumBytes,pWidth,pHeight,(DECODED_IMAGE_FORMAT*)pFormat,pTimestamp);
}

VGLIB_EXPORT void  VideoDecoder_ReleaseFrameGpu(void* pDecoder)  // release a frame position in queue for re-use
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->ReleaseFrameGpu();
}



VGLIB_EXPORT void* VideoDecoder_PeekNextGpuMat(void* pDecoder, uint64_t* timestamp)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

	void* pGpuMat = NULL;

    GPUMAT_FIFO_OBJECT* pGpuMatObj = vd->PeekNextGpuMat();
	if(pGpuMatObj != NULL)
	{
		pGpuMat = (void*)pGpuMatObj->pGpuMat;
		*timestamp = pGpuMatObj->timestamp;
	}
	else *timestamp = 0;

    return pGpuMat;
}

VGLIB_EXPORT bool  VideoDecoder_PopNextGpuMat(void* pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    return vd->PopNextGpuMat();
}

VGLIB_EXPORT void  VideoDecoder_GetDecodedImageSize(void* pDecoder, uint32_t *pWidth, uint32_t *pHeight)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

    vd->GetDecodedImageSize(pWidth,pHeight);
}


VGLIB_EXPORT void VideoDecoder_Free(void* pDecoder)
{
    delete reinterpret_cast<VideoDecoder*>(pDecoder);
}

VGLIB_EXPORT uint32_t  VideoDecoder_InputQueue_GetFramesIn(void* pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);
    return vd->InputQueue_GetPushCount();
}

VGLIB_EXPORT uint32_t  VideoDecoder_InputQueue_GetFramesOut(void* pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);
    return vd->InputQueue_GetPopCount();
}

VGLIB_EXPORT void VideoDecoder_SetSkipCount(void *pDecoder, int count)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);
    vd->SetSkipCount(count);
}


VGLIB_EXPORT void VideoDecoder_InitD3D9(void *pDecoder, void *vpWnd, int width, int height)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);
	vd->D3D9_Interop_Init(vpWnd,width,height);
}


VGLIB_EXPORT void VideoDecoder_CleanUpD3D9(void *pDecoder)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);
	vd->D3D9_CleanUp();
}

VGLIB_EXPORT void VideoDecoder_SetD3D9Window(void *pDecoder, void* vpWnd, int width, int height)
{
    VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);
	vd->D3D9_SetWindow(vpWnd, width, height);
}



VGLIB_EXPORT bool  VideoDecoder_CopyGpuToCpu(void *pDecoder, char* pDestCpu, void* pSourceGpu, uint32_t numBytes)
{
	VideoDecoder* vd = reinterpret_cast<VideoDecoder*>(pDecoder);

	return vd->CopyGpuFrameToCpuFrame(pDestCpu, (CUdeviceptr)pSourceGpu, numBytes);
}



//////////////////////////////////////////////////////////////
///     VideoEncoder interface
///

VGLIB_EXPORT void* VideoEncoder_Create(void* pVideoDecoder)
{
    VideoDecoder* pVD = reinterpret_cast<VideoDecoder*>(pVideoDecoder);
    VideoEncoder* pEN = new VideoEncoder(pVD);
    return reinterpret_cast<void*>(pEN);
}

VGLIB_EXPORT void VideoEncoder_Free(void* pVideoEncoder)
{
     VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);
     delete pEN;
}


VGLIB_EXPORT void VideoEncoder_Start(void *pVideoEncoder)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);

    pEN->Start();
}


VGLIB_EXPORT void  VideoEncoder_Stop(void* pVideoEncoder)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);

    pEN->Stop();
}

VGLIB_EXPORT void VideoEncoder_ConfigureEncoder(void *pVideoEncoder, unsigned int bitRate, int frameRate, int mode, int outputMode,
                                                int gopLength,
                                                int invalidateRefFramesEnableFlag,
                                                int intraRefreshEnableFlag,
                                                int intraRefreshPeriod,
                                                int intraRefreshDuration)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);

    pEN->ConfigureEncoder(bitRate,frameRate,(ENCODER_MODE)mode, (OUTPUT_MODE)outputMode,
                          gopLength,
                          invalidateRefFramesEnableFlag,
                          intraRefreshEnableFlag,
                          intraRefreshPeriod,
                          intraRefreshDuration);
}


VGLIB_EXPORT void  VideoEncoder_GetWindowsHandles(void* pVideoEncoder, HANDLE *pEncoderStoppedEvent, HANDLE *pEncoderOutputQueueSemaphore)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);

    pEN->GetWindowsHandles(pEncoderStoppedEvent);

    pEN->GetEncoderOutputQueueSemaphore(pEncoderOutputQueueSemaphore);
}


VGLIB_EXPORT void VideoEncoder_SetOutputFilename(void* pVideoEncoder, char* filename)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);
    std::string filenameStr(filename);

    pEN->SetOutputFilename(filenameStr);
}


VGLIB_EXPORT bool VideoEncoder_GetNextEncodedFrame(void* pVideoEncoder, char** ppBuffer, uint32_t* pNumBytes)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);

    return pEN->GetNextFrameFromOutputQueue(ppBuffer,pNumBytes);
}


VGLIB_EXPORT void  VideoEncoder_ReleaseFrame(void *pVideoEncoder)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);

    pEN->ReleaseFrameFromOutputQueue();
}


VGLIB_EXPORT uint32_t  VideoEncoder_OutputQueue_GetFramesIn(void* pVideoEncoder)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);
    return pEN->GetFramesIn();
}

VGLIB_EXPORT uint32_t  VideoEncoder_OutputQueue_GetFramesOut(void* pVideoEncoder)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);
    return pEN->GetFramesOut();
}


VGLIB_EXPORT uint32_t  VideoEncoder_Flush(void* pVideoEncoder)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);
    NVENCSTATUS status = pEN->FlushEncoder();

    return (uint32_t)status;
}


VGLIB_EXPORT void VideoEncoder_SetJpegQuality(void* pVideoEncoder, uint32_t quality)
{
    VideoEncoder* pEN = reinterpret_cast<VideoEncoder*>(pVideoEncoder);
    pEN->Jpeg_SetOutputQuality(quality);
}





//////////////////////////////////////////////////////////////
///     DirectX interface
///
//////////////////////////////////////////////////////////////


VGLIB_EXPORT bool DX_CopyImageToSurface(void* pCuda, int SurfaceIndex, void* ImageData)
{
	CudaUtil * pcu = reinterpret_cast<CudaUtil*>(pCuda);
	return pcu->CopyImageToSurface(SurfaceIndex, (CUdeviceptr)ImageData);
}


VGLIB_EXPORT bool DX_RemoveD3DSurface(void* pCuda, int SurfaceIndex)
{
	CudaUtil * pcu = reinterpret_cast<CudaUtil*>(pCuda);
	return pcu->RemoveD3DSurface(SurfaceIndex);
}

VGLIB_EXPORT bool DX_AddD3DSurface(void* pCuda, int SurfaceIndex, void* pSurface, int width, int height)
{
	CudaUtil * pcu = reinterpret_cast<CudaUtil*>(pCuda);
	return pcu->AddD3DSurface(SurfaceIndex, reinterpret_cast<IDirect3DSurface9*>(pSurface), width, height);
}