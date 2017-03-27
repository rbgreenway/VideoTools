#include "VideoEncoder.h"
#include <cuda.h>

#define BITSTREAM_BUFFER_SIZE 2*1024*1024

VideoEncoder::VideoEncoder(VideoDecoder *pVideoDecoder)
{    
    mp_decoder = pVideoDecoder;
    mp_DecodeSession = pVideoDecoder->GetDecodeSession();

    cuCtxPushCurrent(pVideoDecoder->GetDecodeSession()->cuContext);

    m_hExitEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
    m_hEncoderStoppedEvent = CreateEvent(NULL, TRUE, TRUE, NULL);
    m_hOutputItems  = mp_decoder->m_hOutputItems;
    m_hFrameQueueSemaphore = pVideoDecoder->GetFrameQueueSemaphore();
    m_hDecoderStoppedEvent = pVideoDecoder->GetDecoderStoppedEvent();

    ResetEvent(m_hExitEvent);  // make sure exit event is clear
    SetEvent(m_hEncoderStoppedEvent); // make sure encoder stopped event is set

    m_pNvHWEncoder = new CNvHWEncoder();

    m_ctxLock = mp_DecodeSession->cuCtxLock;

    m_uEncodeBufferCount = 0;
    m_iEncodedFrames = 0;
    memset(&m_stEncoderInput, 0, sizeof(m_stEncoderInput));
    memset(&m_stEOSOutputBfr, 0, sizeof(m_stEOSOutputBfr));
    memset(&m_stEncodeBuffer, 0, sizeof(m_stEncodeBuffer));

    m_encoderConfigured = false;
//    EncodingPrepCallback = nullptr;
//    EncodingCompleteCallback = nullptr;
//    EncodedFrameReadyCallback = nullptr;
    EncodingFinished = nullptr;

    SetDefaults();  // set up default values in m_encodeConfig

    // set default values
    m_inputWidth = m_encodeConfig.width;
    m_inputHeight = m_encodeConfig.height;
    m_outputBitrate = m_encodeConfig.bitrate;

    m_encoderMode = ENCODER_TO_H264;

    m_bEncoderRunning.store(false);

    m_threadPtr = nullptr;

    mp_videoSink = nullptr;

    m_jpegQuality = 75;

}

VideoEncoder::~VideoEncoder(void)
{
    if(m_threadPtr != nullptr)
    {
        SetEvent(m_hExitEvent);

        m_threadPtr->join();
        delete m_threadPtr;
    }


    // clean up encode API resources here
    if (m_pNvHWEncoder)
    {
        delete m_pNvHWEncoder;
        m_pNvHWEncoder = NULL;
    }

    //  clean up VideoSink
    if(mp_videoSink)
    {
        delete mp_videoSink;
        mp_videoSink = nullptr;
    }

    CloseHandle(m_hExitEvent);
    CloseHandle(m_hEncoderStoppedEvent);
}

void VideoEncoder::Start()
{
    if(!m_encoderConfigured)
    {
        Diagnostics::DebugMessage("VideoEncoder::Start Failed.  Encoder Not Configured!");
        return;
    }

    switch(m_encoderMode)
    {
    case ENCODER_TO_H264:
    case ENCODER_TO_HEVC:
        // start separate thread that runs the StartDecoder() method
        m_threadPtr = new std::thread(&VideoEncoder::StartEncoder,this);
        break;
    case ENCODER_TO_JPEG:
        // start separate thread that runs the StartDecoder() method
        m_threadPtr = new std::thread(&VideoEncoder::StartJpegEncoder,this);
        break;
    }


}

void VideoEncoder::Stop()
{
    SetEvent(m_hExitEvent);
}


void VideoEncoder::SetDefaults()
{
    memset(&m_encodeConfig,0,sizeof(EncodeConfig));

    m_encodeConfig.endFrameIdx = INT_MAX;
    m_encodeConfig.bitrate = 1024000;
    m_encodeConfig.gopLength = 24; //NVENC_INFINITE_GOPLENGTH;
    m_encodeConfig.codec = NV_ENC_H264;  // use care setting to anything but this, some cards don't handle H265

    m_encodeConfig.fps = 24;
    m_encodeConfig.qp = 28;
    m_encodeConfig.pictureStruct = NV_ENC_PIC_STRUCT_FRAME;


    m_encodeConfig.rcMode = NV_ENC_PARAMS_RC_VBR;//NV_ENC_PARAMS_RC_CONSTQP;
    m_encodeConfig.deviceType = NV_ENC_DEVICE_TYPE_CUDA; //NV_ENC_DEVICE_TYPE_DIRECTX
    m_encodeConfig.presetGUID = NV_ENC_PRESET_LOW_LATENCY_HQ_GUID;//NV_ENC_PRESET_DEFAULT_GUID;
    m_encodeConfig.isYuv444 = 0;


    m_encodeConfig.width = 640;
    m_encodeConfig.height = 480;


    std::string defaultOutputFilename = "outencoder.mp4";
    std::string defaultInputFilename  = "in.mp4";

    // convert to lower case
    std::transform(defaultOutputFilename.begin(), defaultOutputFilename.end(),
                   defaultOutputFilename.begin(), ::tolower);

    std::transform(defaultInputFilename.begin(), defaultInputFilename.end(),
                   defaultInputFilename.begin(), ::tolower);

    m_encodeConfig.outputFileName = (char*)malloc(defaultOutputFilename.length()+1);
    strcpy(m_encodeConfig.outputFileName,defaultOutputFilename.c_str());


    m_encodeConfig.inputFileName = (char*)malloc(defaultInputFilename.length()+1);
    strcpy(m_encodeConfig.inputFileName,defaultInputFilename.c_str());

}



void VideoEncoder::ConfigureEncoder(unsigned int bitRate, int frameRate, ENCODER_MODE mode, OUTPUT_MODE outMode,
                                    int gopLength,                      // set = frameRate, so that we get one i-frame per second
                                    int invalidateRefFramesEnableFlag,  // 0
                                    int intraRefreshEnableFlag,         // 1
                                    int intraRefreshPeriod,             // 5
                                    int intraRefreshDuration)           // 5
{
    m_encoderConfigured = true;

    m_inputWidth = mp_DecodeSession->dci.ulTargetWidth;
    m_inputHeight = mp_DecodeSession->dci.ulTargetHeight;
    m_outputBitrate = bitRate;
    m_frameRate = frameRate;

    m_encodeConfig.fps = frameRate;
    m_encodeConfig.width = m_inputWidth;
    m_encodeConfig.height = m_inputHeight;
    m_encodeConfig.bitrate = bitRate;
    m_encodeConfig.gopLength = m_frameRate;

    switch(mode)
    {
    case ENCODER_TO_H264:
        m_encodeConfig.codec = NV_ENC_H264;
        break;
    case ENCODER_TO_HEVC:
        m_encodeConfig.codec = NV_ENC_HEVC;  // H265
        break;
    case ENCODER_TO_JPEG:
        // since we don't use the hardware encoder for JPEG encoding, doesn't matter what this is set to
        break;
    }


    switch(outMode)
    {
    case OUTPUT_MP4_FILE:
        mp_videoSink = new VideoSink_MP4();
        break;
    case OUTPUT_AVI_FILE:
        mp_videoSink = new VideoSink_AVI();
        break;
    case OUTPUT_QUEUE:
        mp_videoSink = new VideoSink_OutputQueue();
        break;
    }


    mp_videoSink->SetFrameRate(frameRate);
    mp_videoSink->SetFrameSize(m_inputWidth, m_inputHeight);

    // bitRate = Encoding bitrate

    int fps = frameRate;
    uint32_t maxFrameSize = bitRate / fps; // bandwidth / frame rate

    m_encodeConfig.vbvSize = maxFrameSize;

    m_encodeConfig.endFrameIdx = INT_MAX;
    m_encodeConfig.bitrate = m_encodeConfig.vbvSize * fps;
    m_encodeConfig.vbvMaxBitrate = m_encodeConfig.vbvSize * fps;

    m_encoderMode = mode;

    m_encodeConfig.gopLength = gopLength;
    m_encodeConfig.invalidateRefFramesEnableFlag = invalidateRefFramesEnableFlag;
    m_encodeConfig.intraRefreshEnableFlag = intraRefreshEnableFlag;
    m_encodeConfig.intraRefreshPeriod = intraRefreshPeriod;
    m_encodeConfig.intraRefreshDuration = intraRefreshDuration;
}

void VideoEncoder::SetOutputFilename(std::string filename)
{
    if(mp_videoSink)
    {
        mp_videoSink->SetOutputFileName(filename);
    }
}



void VideoEncoder::StartEncoder()
{
    cuCtxPushCurrent(mp_DecodeSession->cuContext);

    FrameQueue* pFrameQueue = mp_DecodeSession->pDecodedFrameQueue;

    ResetEvent(m_hEncoderStoppedEvent);

    // build and init encoder


        // make sure that the encoder is configured to accept the same size that the decoder is outputing
        m_inputWidth = mp_DecodeSession->dci.ulTargetWidth;
        m_inputHeight = mp_DecodeSession->dci.ulTargetHeight;

        if(m_inputWidth == 0 || m_inputHeight == 0) // this is bad, but no way to handle this right now
        {
            m_inputWidth = 640;
            m_inputHeight = 480;
        }

        m_encodeConfig.width  = m_inputWidth;
        m_encodeConfig.height = m_inputHeight;
        m_encodeConfig.bitrate = m_outputBitrate;
        m_encodeConfig.fps = m_frameRate;

        // move this call to decoder code
        //pFrameQueue->init(m_encodeConfig.width, m_encodeConfig.height);

        NVENCSTATUS nvStatus = m_pNvHWEncoder->Initialize(mp_DecodeSession->cuContext,NV_ENC_DEVICE_TYPE_CUDA);
        if (nvStatus != NV_ENC_SUCCESS)
        {
            // ERROR - Need to add error handling here           
            Diagnostics::DebugMessage("Failed to Initialize Hardware Encoder.");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));
            return;
        }

        m_encodeConfig.presetGUID = m_pNvHWEncoder->GetPresetGUID(m_encodeConfig.encoderPreset, m_encodeConfig.codec);
        nvStatus = m_pNvHWEncoder->CreateEncoder(&m_encodeConfig);
        if (nvStatus != NV_ENC_SUCCESS)
        {
            // ERROR - Need to add error handling here
            Diagnostics::DebugMessage("Failed to Create Hardware Incoder.");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));
            return;
        }

        nvStatus = AllocateIOBuffers(&m_encodeConfig);
        if (nvStatus != NV_ENC_SUCCESS)
        {
            // ERROR - Need to add error handling here
            Diagnostics::DebugMessage("Failed to Allocate IO Buffers for Encoder.");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));
            return;
        }


        // get reference to the last frame that the encoder processed
        ENCODED_FRAME_DATA * pLastFrame = GetPtrToLastEncodedFrameBuffer();


//        if(EncodingPrepCallback != nullptr)
//            EncodingPrepCallback();
        // Prep the VideoSink
        mp_videoSink->Prep();

    //start encoding loop
        int count = 0;
        int timeoutCount = 0;
        CUresult result;

        HANDLE handles[] = { m_hExitEvent,            // stop event
                             m_hFrameQueueSemaphore}; // semaphore tied to pFrameQueue

        m_bEncoderRunning.store(true);

        while( !(pFrameQueue->isEndOfDecode() && pFrameQueue->isEmpty()) && m_bEncoderRunning.load())
        {
            switch (WaitForMultipleObjects(2, handles, FALSE, 100))
            {
               case WAIT_OBJECT_0:
                    m_bEncoderRunning.store(false);
                    break;

               case WAIT_OBJECT_0 + 1:
                    CUVIDPARSERDISPINFO pInfo;
                    if(pFrameQueue->dequeue(&pInfo)) {
                        CUdeviceptr dMappedFrame = 0;
                        unsigned int pitch;
                        CUVIDPROCPARAMS oVPP = { 0 };
                        oVPP.unpaired_field = 1;
                        oVPP.progressive_frame = 1;

                        result = cuvidMapVideoFrame(mp_DecodeSession->cuDecoder, pInfo.picture_index, &dMappedFrame, &pitch, &oVPP);

                        if(result!=CUDA_SUCCESS)
                        {
                            Diagnostics::DebugMessage("cuvidMapVideoFrame Failed.");
                            Diagnostics::DebugMessage(GetCudaErrorMessage(result));
                        }

                        EncodeFrameConfig stEncodeConfig = { 0 };
                        stEncodeConfig.dptr = dMappedFrame;
                        stEncodeConfig.pitch = pitch;
                        stEncodeConfig.width = m_encodeConfig.width;
                        stEncodeConfig.height = m_encodeConfig.height;
                        EncodeFrame(&stEncodeConfig);

                        result = cuvidUnmapVideoFrame(mp_DecodeSession->cuDecoder, dMappedFrame);

                        if(result!=CUDA_SUCCESS)
                        {
                            Diagnostics::DebugMessage("cuvidUnmapVideoFrame Failed.");
                            Diagnostics::DebugMessage(GetCudaErrorMessage(result));
                        }

                        pFrameQueue->releaseFrame(&pInfo);


                        if(pLastFrame->Ready)
                        {
                            pLastFrame->Ready = false;
                            count++;
                            mp_videoSink->AddFrame(pLastFrame->pData,pLastFrame->NumBytes,pLastFrame->frameNumber,pLastFrame->Ready,pLastFrame->AviDwFlags);
                        }
                   }
                    timeoutCount = 0;
                   break;

               case WAIT_TIMEOUT:
                    // check maximum wait time for a new frame
                    timeoutCount++;
//                    if(timeoutCount > 50) m_bEncoderRunning.store(false);
                   break;
            }
        }

        m_bEncoderRunning.store(false);

        nvStatus = FlushEncoder();
        if (nvStatus != NV_ENC_SUCCESS)
        {
            // ERROR - Need to add error handling here
            Diagnostics::DebugMessage("Failed to Flush Encoder.");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));
            return;
        }

        SetEvent(m_hEncoderStoppedEvent);

        mp_videoSink->Complete();

        if(EncodingFinished != nullptr)
            EncodingFinished(count);

       nvStatus = Deinitialize();
       if (nvStatus != NV_ENC_SUCCESS)
       {
           // ERROR - Need to add error handling here
           Diagnostics::DebugMessage("Failed to Deinitialize Hardware Encoder.");
           Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));
           return;
       }

}





NVENCSTATUS VideoEncoder::AllocateIOBuffers(EncodeConfig* pEncodeConfig)
{
    NVENCSTATUS nvStatus = NV_ENC_SUCCESS;
    CUresult result = CUDA_SUCCESS;

    m_uEncodeBufferCount = pEncodeConfig->numB + 4;

    uint32_t uInputWidth  = pEncodeConfig->width;
    uint32_t uInputHeight = pEncodeConfig->height;
    bool success = m_EncodeBufferQueue.Initialize(m_stEncodeBuffer, m_uEncodeBufferCount);

    if(!success)
    {
        Diagnostics::DebugMessage("Encoder Buffer Queue Initialize Failed.");
    }

    //Allocate input buffer
    for (uint32_t i = 0; i < m_uEncodeBufferCount; i++) {

        { // use brackets to provide local scope to CAutoCtxLock below
//            CAutoCtxLock lck(m_ctxLock);
            cuvidCtxLock(m_ctxLock,0);
            result = cuMemAllocPitch(&m_stEncodeBuffer[i].stInputBfr.pNV12devPtr,
                    (size_t*)&m_stEncodeBuffer[i].stInputBfr.uNV12Stride,
                    uInputWidth, uInputHeight * 3 / 2, 16);
            cuvidCtxUnlock(m_ctxLock,0);

            if(result != CUDA_SUCCESS)
            {
                Diagnostics::DebugMessage("Error in function cuMemAllocPitch");
                Diagnostics::DebugMessage(GetCudaErrorMessage(result));

                return NV_ENC_ERR_GENERIC;
            }
        }

        nvStatus = m_pNvHWEncoder->NvEncRegisterResource(NV_ENC_INPUT_RESOURCE_TYPE_CUDADEVICEPTR,
            (void*)m_stEncodeBuffer[i].stInputBfr.pNV12devPtr,
            uInputWidth, uInputHeight,
            m_stEncodeBuffer[i].stInputBfr.uNV12Stride,
            &m_stEncodeBuffer[i].stInputBfr.nvRegisteredResource);

        if (nvStatus != NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in NvEncRegisterResource");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

            return nvStatus;
        }

        m_stEncodeBuffer[i].stInputBfr.bufferFmt = NV_ENC_BUFFER_FORMAT_NV12_PL;
        m_stEncodeBuffer[i].stInputBfr.dwWidth = uInputWidth;
        m_stEncodeBuffer[i].stInputBfr.dwHeight = uInputHeight;

        nvStatus = m_pNvHWEncoder->NvEncCreateBitstreamBuffer(BITSTREAM_BUFFER_SIZE, &m_stEncodeBuffer[i].stOutputBfr.hBitstreamBuffer);
        if (nvStatus != NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in NvEncCreateBitstreamBuffer");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

            return nvStatus;
        }

        m_stEncodeBuffer[i].stOutputBfr.dwBitstreamBufferSize = BITSTREAM_BUFFER_SIZE;

#if defined(NV_WINDOWS)
        nvStatus = m_pNvHWEncoder->NvEncRegisterAsyncEvent(&m_stEncodeBuffer[i].stOutputBfr.hOutputEvent);
        if (nvStatus != NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in NvEncRegisterAsyncEvent");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

            return nvStatus;
        }
        m_stEncodeBuffer[i].stOutputBfr.bWaitOnEvent = true;
#else
        m_stEncodeBuffer[i].stOutputBfr.hOutputEvent = NULL;
#endif
    }

    m_stEOSOutputBfr.bEOSFlag = TRUE;
#if defined(NV_WINDOWS)
    nvStatus = m_pNvHWEncoder->NvEncRegisterAsyncEvent(&m_stEOSOutputBfr.hOutputEvent);
    if (nvStatus != NV_ENC_SUCCESS)
    {
        Diagnostics::DebugMessage("Error in NvEncRegisterAsyncEvent");
        Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

        return nvStatus;
    }
#else
    m_stEOSOutputBfr.hOutputEvent = NULL;
#endif

    return NV_ENC_SUCCESS;
}

ENCODED_FRAME_DATA *VideoEncoder::GetPtrToLastEncodedFrameBuffer()
{
    return m_pNvHWEncoder->GetLastFramePtr();
}



std::string VideoEncoder::GetCudaErrorMessage(CUresult result)
{
    std::string errMsg;

    char msg[2048];
    const char* pmsg = &msg[0];
    const char** ppmsg = &pmsg;
    result = cuGetErrorString(result, ppmsg);

    if(result != CUDA_SUCCESS)
    {
        errMsg = "Failed to retrieve error message for CUresult = " + std::to_string((int)result);
    }
    else
    {
        std::string eMsg(pmsg);
        errMsg = eMsg;
    }

    return errMsg;
}



std::string VideoEncoder::GetEncoderErrorMessage(NVENCSTATUS code)
{

        // These error descriptions are taken from: nvEncodeAPI.h file provided by Nvidia

        std::string errMsg;

        switch (code)
        {

        case NV_ENC_SUCCESS:
            errMsg = "No Error";
            break;

        case NV_ENC_ERR_NO_ENCODE_DEVICE:
            errMsg = "No encode capable devices were detected.";
            break;

        case NV_ENC_ERR_UNSUPPORTED_DEVICE:
            errMsg = "Device(s) passed by the client is not supported.";
            break;

        case NV_ENC_ERR_INVALID_ENCODERDEVICE:
            errMsg = "Encoder device supplied by the client is not valid.";
            break;

        case NV_ENC_ERR_INVALID_DEVICE:
            errMsg = "Device passed to the API call is invalid.";
            break;

        case NV_ENC_ERR_DEVICE_NOT_EXIST:
            errMsg = "Device passed to the API call is no longer available and\nneeds to be reinitialized. The clients need to destroy the current encoder\nsession by freeing the allocated input output buffers and destroying the device\nand create a new encoding session.";
            break;

        case NV_ENC_ERR_INVALID_PTR:
            errMsg = "One or more of the pointers passed to the API call is invalid.";
            break;

        case NV_ENC_ERR_INVALID_EVENT:
            errMsg = "Completion event passed in ::NvEncEncodePicture() call is invalid.";
            break;

        case NV_ENC_ERR_INVALID_PARAM:
            errMsg = "One or more of the parameter passed to the API call is invalid.";
            break;

        case NV_ENC_ERR_INVALID_CALL:
            errMsg = "An API call was made in wrong sequence/order.";
            break;

        case NV_ENC_ERR_OUT_OF_MEMORY:
            errMsg = "The API call failed because it was unable to allocate enough memory to perform the requested operation.";
            break;

        case NV_ENC_ERR_ENCODER_NOT_INITIALIZED:
            errMsg = "The encoder has not been initialized with ::NvEncInitializeEncoder()\nor that initialization has failed.\nThe client cannot allocate input or output buffers or do any encoding\nrelated operation before successfully initializing the encoder.";
            break;

        case NV_ENC_ERR_UNSUPPORTED_PARAM:
            errMsg = "An unsupported parameter was passed by the client.";
            break;

        case NV_ENC_ERR_LOCK_BUSY:
            errMsg = "The ::NvEncLockBitstream() failed to lock the output\nbuffer. This happens when the client makes a non blocking lock call to\naccess the output bitstream by passing NV_ENC_LOCK_BITSTREAM::doNotWait flag.\nThis is not a fatal error and client should retry the same operation after\nfew milliseconds.";
            break;

        case NV_ENC_ERR_NOT_ENOUGH_BUFFER:
            errMsg = "The size of the user buffer passed by the client is insufficient for the requested operation.";
            break;

        case NV_ENC_ERR_INVALID_VERSION:
            errMsg = "An invalid struct version was used by the client.";
            break;

        case NV_ENC_ERR_MAP_FAILED:
            errMsg = "::NvEncMapInputResource() API failed to map the client provided input resource.";
            break;

        case NV_ENC_ERR_NEED_MORE_INPUT:
            errMsg = "Encode driver requires more input buffers to produce an output bitstream.\nIf this error is returned from ::NvEncEncodePicture() API, this\nis not a fatal error. If the client is encoding with B frames then,\n::NvEncEncodePicture() API might be buffering the input frame for re-ordering.";
            break;

        case NV_ENC_ERR_ENCODER_BUSY:
            errMsg = "The HW encoder is busy encoding and is unable to encode\nthe input. The client should call ::NvEncEncodePicture() again after few\nmilliseconds.";
            break;

        case NV_ENC_ERR_EVENT_NOT_REGISTERD:
            errMsg = "The completion event passed in ::NvEncEncodePicture()\nAPI has not been registered with encoder driver using ::NvEncRegisterAsyncEvent().";
            break;

        case NV_ENC_ERR_GENERIC:
            errMsg = "An unknown internal error has occurred.";
            break;

        case NV_ENC_ERR_INCOMPATIBLE_CLIENT_KEY:
            errMsg = "The client is attempting to use a feature that is not available for the license type for the current system.";
            break;

        case NV_ENC_ERR_UNIMPLEMENTED:
            errMsg = "The client is attempting to use a feature that is not implemented for the current version.";
            break;

        case NV_ENC_ERR_RESOURCE_REGISTER_FAILED:
            errMsg = "The ::NvEncRegisterResource API failed to register the resource.";
            break;

        case NV_ENC_ERR_RESOURCE_NOT_REGISTERED:
            errMsg = "The client is attempting to unregister a resource that has not been successfuly registered.";
            break;

        case NV_ENC_ERR_RESOURCE_NOT_MAPPED:
            errMsg = "The client is attempting to unmap a resource that has not been successfuly mapped.";
            break;

        default:
            errMsg = "Unknown Encoder error.";
            break;
        }


        return errMsg;
}




void VideoEncoder::ConfigureEncoderFinishedCallback(std::function<void (int)> finishedCallback)
{
    EncodingFinished = finishedCallback;
}






NVENCSTATUS VideoEncoder::ReleaseIOBuffers()
{
    CUresult result;
    NVENCSTATUS status;

    for (uint32_t i = 0; i < m_uEncodeBufferCount; i++)
    {

        { // use brackets to provide local scope to CAutoCtxLock below
//            CAutoCtxLock lck(m_ctxLock);
            cuvidCtxLock(m_ctxLock,0);
            result = cuMemFree(m_stEncodeBuffer[i].stInputBfr.pNV12devPtr);
            cuvidCtxUnlock(m_ctxLock,0);
            if(result != CUDA_SUCCESS)
            {
                Diagnostics::DebugMessage("Error in cuMemFree");
                Diagnostics::DebugMessage(GetCudaErrorMessage(result));

                return NV_ENC_ERR_GENERIC;
            }
        }

        status = m_pNvHWEncoder->NvEncDestroyBitstreamBuffer(m_stEncodeBuffer[i].stOutputBfr.hBitstreamBuffer);
        if(status!=NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in NvEncDestroyBitstreamBuffer");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(status));

            return status;
        }

        m_stEncodeBuffer[i].stOutputBfr.hBitstreamBuffer = NULL;

#if defined(NV_WINDOWS)
        status = m_pNvHWEncoder->NvEncUnregisterAsyncEvent(m_stEncodeBuffer[i].stOutputBfr.hOutputEvent);
        if(status!=NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in NvEncUnregisterAsyncEvent");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(status));

            return status;
        }

        nvCloseFile(m_stEncodeBuffer[i].stOutputBfr.hOutputEvent);
        m_stEncodeBuffer[i].stOutputBfr.hOutputEvent = NULL;
#endif
    }

    if (m_stEOSOutputBfr.hOutputEvent)
    {
#if defined(NV_WINDOWS)
        status = m_pNvHWEncoder->NvEncUnregisterAsyncEvent(m_stEOSOutputBfr.hOutputEvent);
        if(status!=NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in NvEncUnregisterAsyncEvent");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(status));

            return status;
        }

        nvCloseFile(m_stEOSOutputBfr.hOutputEvent);
        m_stEOSOutputBfr.hOutputEvent = NULL;
#endif
    }

    return NV_ENC_SUCCESS;
}

NVENCSTATUS VideoEncoder::FlushEncoder()
{
    NVENCSTATUS nvStatus = m_pNvHWEncoder->NvEncFlushEncoderQueue(m_stEOSOutputBfr.hOutputEvent);
    if (nvStatus != NV_ENC_SUCCESS)
    {
        Diagnostics::DebugMessage("Error in NvEncFlushEncoderQueue");
        Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

        return nvStatus;
    }

    // get reference to the last frame that the encoder processed
    ENCODED_FRAME_DATA * pLastFrame = GetPtrToLastEncodedFrameBuffer();

    EncodeBuffer *pEncodeBuffer = m_EncodeBufferQueue.GetPending();
    while (pEncodeBuffer)
    {
        nvStatus = m_pNvHWEncoder->ProcessOutput(pEncodeBuffer);
        if (nvStatus != NV_ENC_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in Encoder ProcessOutput");
            Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

            return nvStatus;
        }


        if(pLastFrame->Ready)
        {
            pLastFrame->Ready = false;

            mp_videoSink->AddFrame(pLastFrame->pData,pLastFrame->NumBytes,pLastFrame->frameNumber,pLastFrame->Ready,pLastFrame->AviDwFlags);
        }

        pEncodeBuffer = m_EncodeBufferQueue.GetPending();
        // UnMap the input buffer after frame is done
        if (pEncodeBuffer && pEncodeBuffer->stInputBfr.hInputSurface)
        {
            nvStatus = m_pNvHWEncoder->NvEncUnmapInputResource(pEncodeBuffer->stInputBfr.hInputSurface);
            if (nvStatus != NV_ENC_SUCCESS)
            {
                Diagnostics::DebugMessage("Error in Encoder NvEncUnmapInputResource");
                Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

                return nvStatus;
            }

            pEncodeBuffer->stInputBfr.hInputSurface = NULL;
        }
    }
#if defined(NV_WINDOWS)
    if (WaitForSingleObject(m_stEOSOutputBfr.hOutputEvent, 500) != WAIT_OBJECT_0)
    {
        Diagnostics::DebugMessage("Error in WaitForSingleObject");
        Diagnostics::DebugMessage("Output Event Timeout");

        nvStatus = NV_ENC_ERR_GENERIC;
    }
#endif
    return nvStatus;
}



NVENCSTATUS VideoEncoder::Deinitialize()
{
    NVENCSTATUS nvStatus = NV_ENC_SUCCESS;

    ReleaseIOBuffers();

    nvStatus = m_pNvHWEncoder->NvEncDestroyEncoder();
    if (nvStatus != NV_ENC_SUCCESS)
    {
        Diagnostics::DebugMessage("Error in Encoder NvEncDestroyEncoder");
        Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

        return nvStatus;
    }

    return NV_ENC_SUCCESS;
}

NVENCSTATUS VideoEncoder::EncodeFrame(EncodeFrameConfig *pEncodeFrame, bool bFlush)
{
    NVENCSTATUS nvStatus = NV_ENC_SUCCESS;
    CUresult result = CUDA_SUCCESS;

    if (bFlush)
    {
        FlushEncoder();
        return NV_ENC_SUCCESS;
    }


    if(pEncodeFrame == NULL)
    {
        Diagnostics::DebugMessage("pEncodeFrame is NULL");
        return NV_ENC_ERR_GENERIC;
    }


    EncodeBuffer *pEncodeBuffer = m_EncodeBufferQueue.GetAvailable();
    if (!pEncodeBuffer)
    {
        pEncodeBuffer = m_EncodeBufferQueue.GetPending();
        m_pNvHWEncoder->ProcessOutput(pEncodeBuffer);
        // UnMap the input buffer after frame done
        if (pEncodeBuffer->stInputBfr.hInputSurface)
        {
            nvStatus = m_pNvHWEncoder->NvEncUnmapInputResource(pEncodeBuffer->stInputBfr.hInputSurface);
            if (nvStatus != NV_ENC_SUCCESS)
            {
                Diagnostics::DebugMessage("Error in Encoder NvEncUnmapInputResource");
                Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

                return nvStatus;
            }


            pEncodeBuffer->stInputBfr.hInputSurface = NULL;
        }
        pEncodeBuffer = m_EncodeBufferQueue.GetAvailable();
    }

    // encode width and height
    unsigned int dwWidth  = pEncodeBuffer->stInputBfr.dwWidth;
    unsigned int dwHeight = pEncodeBuffer->stInputBfr.dwHeight;

    // Here we copy from Host to Device Memory (CUDA)

    { // use brackets to provide local scope to CAutoCtxLock below
//        CAutoCtxLock lck(m_ctxLock);


        if(pEncodeFrame->width != dwWidth || pEncodeFrame->height != dwHeight)
        {
            Diagnostics::DebugMessage("Error in EncodeFrame");
            Diagnostics::DebugMessage("Frame dimensions don't match Encoder configuration");

            return NV_ENC_ERR_GENERIC;
        }

        CUDA_MEMCPY2D memcpy2D  = {0};
        memcpy2D.srcMemoryType  = CU_MEMORYTYPE_DEVICE;
        memcpy2D.srcDevice      = pEncodeFrame->dptr;
        memcpy2D.srcPitch       = pEncodeFrame->pitch;
        memcpy2D.dstMemoryType  = CU_MEMORYTYPE_DEVICE;
        memcpy2D.dstDevice      = (CUdeviceptr)pEncodeBuffer->stInputBfr.pNV12devPtr;
        memcpy2D.dstPitch       = pEncodeBuffer->stInputBfr.uNV12Stride;
        memcpy2D.WidthInBytes   = dwWidth;
        memcpy2D.Height         = dwHeight*3/2;

        cuvidCtxLock(m_ctxLock,0);

        result = cuMemcpy2D(&memcpy2D);

        cuvidCtxUnlock(m_ctxLock,0);

        if(result != CUDA_SUCCESS)
        {
            Diagnostics::DebugMessage("Error in cuMemcpy2D");
            Diagnostics::DebugMessage(GetCudaErrorMessage(result));

            return NV_ENC_ERR_GENERIC;
        }
    }



    nvStatus = m_pNvHWEncoder->NvEncMapInputResource(pEncodeBuffer->stInputBfr.nvRegisteredResource, &pEncodeBuffer->stInputBfr.hInputSurface);
    if (nvStatus != NV_ENC_SUCCESS)
    {
        Diagnostics::DebugMessage("Error in NvEncMapInputResource");
        Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

        return nvStatus;
    }

    nvStatus = m_pNvHWEncoder->NvEncEncodeFrame(pEncodeBuffer, NULL, pEncodeFrame->width, pEncodeFrame->height);
    if (nvStatus != NV_ENC_SUCCESS)
    {
        Diagnostics::DebugMessage("Error in NvEncEncodeFrame");
        Diagnostics::DebugMessage(GetEncoderErrorMessage(nvStatus));

        return nvStatus;
    }

    m_iEncodedFrames++;

    return NV_ENC_SUCCESS;
}

int VideoEncoder::GetFrameRate()
{
    return m_encodeConfig.fps;
}

void VideoEncoder::GetWidthHeight(int *w, int *h)
{
    *w = m_encodeConfig.width;
    *h = m_encodeConfig.height;
}

void VideoEncoder::GetWindowsHandles(HANDLE *pEncoderStoppedEvent)
{
    *pEncoderStoppedEvent = m_hEncoderStoppedEvent;
}

void VideoEncoder::GetEncoderOutputQueueSemaphore(HANDLE *pSinkOutputQueueSemaphore)
{
    if(mp_videoSink)
        mp_videoSink->GetOutputItemsSemaphore(pSinkOutputQueueSemaphore);
    else
        *pSinkOutputQueueSemaphore = NULL;
}


bool VideoEncoder::GetNextFrameFromOutputQueue(char** pData, uint32_t* numBytes)
{
    bool success = false;
    if(mp_videoSink)
    {
        success = mp_videoSink->OutputQueue_GetNextFrame(pData,numBytes);
    }
    return success;
}

void VideoEncoder::ReleaseFrameFromOutputQueue()
{
    if(mp_videoSink)
    {
        mp_videoSink->OutputQueue_ReleaseFrame();
    }
}

uint32_t VideoEncoder::GetFramesOut()
{
    uint32_t count = 0;
    if(mp_videoSink)
        count = mp_videoSink->OutputQueue_GetPopCount();
    return count;
}

uint32_t VideoEncoder::GetFramesIn()
{
    uint32_t count = 0;
    if(mp_videoSink)
        count = mp_videoSink->OutputQueue_GetPushCount();
    return count;
}



/////////////////////////////////////////////////////////////////////////////////
///  JPEG functions
///



void VideoEncoder::StartJpegEncoder()
{
    cuCtxPushCurrent(mp_DecodeSession->cuContext);

    ResetEvent(m_hEncoderStoppedEvent);

    CircularFifoGpu* pGpuDecodedFrameQueue = mp_DecodeSession->pGpuDecodedFrameQueue;

    m_inputWidth = mp_DecodeSession->dci.ulTargetWidth;
    m_inputHeight = mp_DecodeSession->dci.ulTargetHeight;

    // build and init encoder
    Jpeg_InitEncoder(m_inputWidth,m_inputHeight);


        // Prep the VideoSink
        mp_videoSink->Prep();


        int count = 0;


        //  wait for decoder to start
            m_bDecoderRunning.store(mp_decoder->GetDecoderRunning());

            int waitCycles = 0; // wait a maximum of this many cycles through the following while loop for the decoder to start
            bool timeout = false;

            while(!m_bDecoderRunning.load() && !timeout)
            {
                switch(WaitForSingleObject(m_hExitEvent,10))
                {
                case WAIT_OBJECT_0:
                    break;
                case WAIT_TIMEOUT:
                    bool r = mp_decoder->GetDecoderRunning();
                    m_bDecoderRunning.store(r);

                    waitCycles++;
                    if(waitCycles>250) timeout = true;
                    break;
                }
            }

            if(timeout)
            {
                Diagnostics::DebugMessage("Encoder Start Failed: Decoder running not detected");
                return;
            }


        // start encoder loop
        int timeoutCount = 0;

		int outerCount = 0;

        m_bEncoderRunning.store(true);

        HANDLE handles[] = { m_hExitEvent,    // stop event
                             m_hOutputItems,  // semaphore tied to output queue
                             m_hDecoderStoppedEvent }; // event indicating that the associated decoder has stopped

        while( m_bEncoderRunning.load() && (m_bDecoderRunning.load() || pGpuDecodedFrameQueue->itemAvailable()) )
        {
            switch (WaitForMultipleObjects(3, handles, FALSE, INFINITE))
            {
               case WAIT_OBJECT_0:  // m_hExitEvent
                    m_bEncoderRunning.store(false);
                    break;

               case WAIT_OBJECT_0 + 1: // m_hOutputItems
				    outerCount++;
					if(pGpuDecodedFrameQueue->itemAvailable())
					{
						// get pointer to next frame in queue
						DecodedFrameOnGpu* pFrame = pGpuDecodedFrameQueue->peekHead();

						if(pFrame->numBytes > 0)
						{
							cudaEvent_t start, stop;
							cudaEventCreate(&start);
							cudaEventCreate(&stop);


							cudaEventRecord(start);
							bool encodeSuccessful = Jpeg_EncodeImage((uint8_t*)pFrame->pData);
							cudaEventRecord(stop);

							cudaEventSynchronize(stop);
							float milliseconds = 0;
							cudaEventElapsedTime(&milliseconds, start, stop);
							if(outerCount % 10 == 0) 
							{
								Diagnostics::DebugMessage("JPEG Encode time (ms): %.3f",milliseconds); 
							}

							if(encodeSuccessful)
							{
								count++;

	//                            ENCODED_FRAME_DATA * pfd = new ENCODED_FRAME_DATA();
	//                            pfd->frameNumber = count;
	//                            pfd->NumBytes = m_jpegEncoderSession.image_compressed_size;
	//                            pfd->Ready = true;
	//                            pfd->pData = (char*)m_jpegEncoderSession.image_compressed;
	//                                EncodedFrameReadyCallback(pfd);

								// send the frame data to the VideoSink
								mp_videoSink->AddFrame((char*)m_jpegEncoderSession.image_compressed,
													   m_jpegEncoderSession.image_compressed_size,count,true,0);
							}
							else
							{
								Diagnostics::DebugMessage("JPEG Encoder Failed.");
							}


						}
						else
						{
							m_bEncoderRunning.store(false);
						}

						// free up this spot in the queue
						pGpuDecodedFrameQueue->pop();
					}			

                    timeoutCount = 0;
                   break;

               case WAIT_OBJECT_0 + 2: // m_hDecoderStoppedEvent
                   m_bDecoderRunning.store(false);
                   break;

               case WAIT_TIMEOUT:
                    // check maximum wait time for a new frame
                    timeoutCount++;
//                    if(timeoutCount > 50) m_bEncoderRunning.store(false);
                   break;
            }
        }

        m_bEncoderRunning.store(false);


        Jpeg_ShutdownEncoder();

        mp_videoSink->Complete();

        if(EncodingFinished != nullptr)
            EncodingFinished(count);


		SetEvent(m_hEncoderStoppedEvent);
}





bool VideoEncoder::Jpeg_InitEncoder(int width, int height)
{
    bool success = true;

    gpujpeg_set_default_parameters(&m_jpegEncoderSession.param);
    m_jpegEncoderSession.param.quality = m_jpegQuality;
    // (default value is 75)
    m_jpegEncoderSession.param.restart_interval = 16;
    // (default value is 8)
    m_jpegEncoderSession.param.interleaved = 1;
    // (default value is 0)


    gpujpeg_image_set_default_parameters(&m_jpegEncoderSession.param_image);
    m_jpegEncoderSession.param_image.width = width;
    m_jpegEncoderSession.param_image.height = height;
    m_jpegEncoderSession.param_image.comp_count = 3;
    // (for now, it must be 3)
    m_jpegEncoderSession.param_image.color_space = GPUJPEG_RGB;
    // or GPUJPEG_YCBCR_ITU_R or GPUJPEG_YCBCR_JPEG
    // (default value is GPUJPEG_RGB)
    m_jpegEncoderSession.param_image.sampling_factor = GPUJPEG_4_4_4;
    // or GPUJPEG_4_2_2
    // (default value is GPUJPEG_4_4_4)

    // Use default sampling factors
    // If you want to use subsampling in JPEG format call following function,
    // that will set default sampling factors (2x2 for Y, 1x1 for Cb and Cr).
    //      User custom sampling factors
    //          param.sampling_factor[0].horizontal = 4;
    //          param.sampling_factor[0].vertical = 4;
    //          param.sampling_factor[1].horizontal = 1;
    //          param.sampling_factor[1].vertical = 2;
    //          param.sampling_factor[2].horizontal = 2;
    //          param.sampling_factor[2].vertical = 1;
    gpujpeg_parameters_chroma_subsampling(&m_jpegEncoderSession.param);


    m_jpegEncoderSession.p_encoder = gpujpeg_encoder_create(&m_jpegEncoderSession.param, &m_jpegEncoderSession.param_image);

    if ( m_jpegEncoderSession.p_encoder == NULL ) success = false;

    return success;
}

bool VideoEncoder::Jpeg_EncodeImage(uint8_t *p_imageData)
{
    bool success = true;

	// TEST
//	int size = m_jpegEncoderSession.param_image.width*m_jpegEncoderSession.param_image.width*3;
//	char* pImageOnCpu = (char*)malloc();
//	cudaMemcpy(pImageOnCpu, p_imageData, size, cudaMemcpyDeviceToHost) );
//	gpujpeg_encoder_input_set_image(&m_jpegEncoderSession.encoder_input, pImageOnCpu);

	// END TEST


    gpujpeg_encoder_input_set_image_on_gpu(&m_jpegEncoderSession.encoder_input, p_imageData);

    m_jpegEncoderSession.image_compressed = NULL;

    if ( gpujpeg_encoder_encode(m_jpegEncoderSession.p_encoder,
                                &m_jpegEncoderSession.encoder_input,
                                &m_jpegEncoderSession.image_compressed,
                                &m_jpegEncoderSession.image_compressed_size) != 0 )
        success = false;

    return success;
}

void VideoEncoder::Jpeg_ShutdownEncoder()
{
    gpujpeg_encoder_destroy(m_jpegEncoderSession.p_encoder);
}

bool VideoEncoder::Jpeg_SaveToFile(std::string filename)
{
    bool success = true;
    if ( gpujpeg_image_save_to_file("output_image.jpg", m_jpegEncoderSession.image_compressed,
             m_jpegEncoderSession.image_compressed_size) != 0 )
        success = false;

    return success;
}

void VideoEncoder::Jpeg_SetOutputQuality(uint32_t quality)
{
    // value must be between 0-100 (inclusive)
    if(quality<0) quality = 0;
    if(quality>100) quality = 100;
    m_jpegQuality = quality;
}




