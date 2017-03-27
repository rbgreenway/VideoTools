#include "videodecoder.h"



VideoDecoder::VideoDecoder(CUcontext ctx, uint32_t codec)
{
//    m_state.inputQueueSemaphore  = CreateSemaphore(NULL,0,CIRCULARFIFO_INPUT_SIZE,NULL);
//    m_state.inputQueueSpacesSemaphore= CreateSemaphore(NULL,CIRCULARFIFO_INPUT_SIZE,CIRCULARFIFO_INPUT_SIZE,NULL);
//    m_state.outputQueueSemaphore = CreateSemaphore(NULL,0,CIRCULARFIFO_CPU_SIZE,NULL);
    m_hExitEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
    m_hDecoderStoppedEvent = CreateEvent(NULL, TRUE, TRUE, NULL);

    memset(&m_state.dci, 0, sizeof(CUVIDDECODECREATEINFO));

    m_state.cuContext = ctx;
    CUresult result = cuCtxPushCurrent(m_state.cuContext); // set the current cuda context

    // bind the context lock to the CUDA context
	result = cuvidCtxLockCreate(&m_state.cuCtxLock, m_state.cuContext); 

    if(result != CUDA_SUCCESS)
    {
        m_cudaResult = result;
        m_errMsg = "Failed creating Cuda Context Lock";

        Diagnostics::DebugMessage("Error in cuvidCtxLockCreate");
        Diagnostics::DebugMessage(GetCudaErrorMessage(result));
    }
    else
    {
        m_state.pDecodedFrameQueue = new CUVIDFrameQueue(m_state.cuCtxLock);
    }



    m_bDecoderRunning.store(false);

    m_decodedFrames = 0;
    m_state.cuParser = NULL;
    m_state.cuDecoder = NULL;
    m_state.count = 0;

    m_state.pCpuDecodedFrameQueue = &m_outputQueueCpu;
    m_state.pGpuMatQueue = &m_outputQueueGpuMat;
    m_state.pGpuDecodedFrameQueue = &m_outputQueueGpu;

    m_state.argbFrame = 0;
    m_state.decodedFormat = FORMAT_ARGB;

    // set default decoder mode
    m_state.decoderMode = DECODER_TO_CPU_ARGB;
    m_state.skipCount = 1;
    m_state.skipCounter = 0;

	m_state.pD3D9 = nullptr;  // this gets set when D3D9_Interop_Init() gets called

	m_state.pVD = this;

	m_state.pD3D9 = &m_D3D9;
	

    m_threadPtr = nullptr;

    m_codec = (cudaVideoCodec)codec; // cudaVideoCodec_H264;  // default codec is H264

    m_hInputItems = m_inputQueue.GetSemaphore_ItemsInQueue();
    m_hInputSpaces = m_inputQueue.GetSemaphore_SpacesInQueue();

    m_hOutputItems = m_outputQueueCpu.GetSemaphore_ItemsInQueue();  // default value - should be set in Configure Decoder function
    m_hOutputSpaces = m_outputQueueCpu.GetSemaphore_SpacesInQueue(); // default value


	SystemState::SS_set_outputQueue_countPush(0);

}

VideoDecoder::~VideoDecoder()
{
    if(m_threadPtr != nullptr)
    {
        m_bDecoderRunning.store(true);
        SetEvent(m_hExitEvent);

        m_threadPtr->join();
        delete m_threadPtr;
    }

	if (m_state.cuStream)
	{
		cuStreamDestroy(m_state.cuStream);
	}

    if (m_state.cuDecoder)
    {
        cuvidDestroyDecoder(m_state.cuDecoder);
        m_state.cuDecoder = NULL;
    }

	if (m_state.pD3D9->cudaLinearMemory != 0)
	{
		cudaFree(m_state.pD3D9->cudaLinearMemory);
	}
	if (m_state.pD3D9->cudaResource != 0)
	{
		cudaFree(m_state.pD3D9->cudaResource);
	}
	

//    CloseHandle(m_state.inputQueueSemaphore);
//    CloseHandle(m_state.inputQueueSpacesSemaphore);
//    CloseHandle(m_state.outputQueueSemaphore);
    CloseHandle(m_hExitEvent);
    CloseHandle(m_hDecoderStoppedEvent);

}




bool VideoDecoder::Init()
{
    bool success = true;
    CUresult result;

	if(success)
    {
        cuvidCtxLock(m_state.cuCtxLock,0);
        result = cuStreamCreate(&m_state.cuStream,CU_STREAM_NON_BLOCKING);
        cuvidCtxUnlock(m_state.cuCtxLock,0);
        if (result != CUDA_SUCCESS)
        {
            m_cudaResult = result;
            m_errMsg = "Failed to creating Cuda Stream";
            success = false;
            Diagnostics::DebugMessage("Error in cuStreamCreate");
            Diagnostics::DebugMessage(GetCudaErrorMessage(result));
        }
    }

    return success;
}



void VideoDecoder::Start()
{

    // make sure any previous decoding sessions has completed.    
//    m_bDecoderRunning.setFalse();
    m_bDecoderRunning.store(false);


    if(m_threadPtr != nullptr)
    {
        m_threadPtr->join();
        m_threadPtr = nullptr;
    }

    // make sure a parser has been created
    if(m_state.cuParser == NULL)
    {
        CreateVideoParser(m_codec,MAX_FRM_CNT);
    }


    // start separate thread that runs the StartDecoder() method
    m_threadPtr = new std::thread(&VideoDecoder::StartDecoder,this);
}





void VideoDecoder::StartDecoder()
{  
    cuCtxPushCurrent(m_state.cuContext);

	if(m_state.decoderMode == DECODER_TO_DIRECTX && m_state.pD3D9->hWnd != 0)
		{
			D3D9_Interop_Init((void*)m_state.pD3D9->hWnd,m_state.pD3D9->width,m_state.pD3D9->height);
		}


    ResetEvent(m_hDecoderStoppedEvent); // indicate that decoder is running

    m_bDecoderRunning.store(true);


    HANDLE handles1[] = {m_hExitEvent, m_hInputItems};
    HANDLE handles2[] = {m_hExitEvent, m_hOutputSpaces};


	int signalCount = 0;

    if(m_state.decoderMode == DECODER_TO_ENCODER)
    {
        ///////////////////////////
        // TRANSCODING LOOP
        while(m_bDecoderRunning.load())
        {
            switch(WaitForMultipleObjects(2, handles1, FALSE, 5000)) // wait for item in input queue
            {
            case WAIT_OBJECT_0:
                m_bDecoderRunning.store(false);
                break;
            case WAIT_OBJECT_0 + 1:
                    {
                        // get next encoded frame from input queue
                        EncodedInputFrame* pFrame = m_inputQueue.peekHead();

                        if(pFrame->flushDecoder)
                        {
                            FlushDecoder();                            
                        }
                        else
                        {
                            HandleVideoData((unsigned char *)pFrame->pData, (uint32_t)pFrame->numBytes, pFrame->timestamp);

                            if((uint32_t)pFrame->numBytes == 0)
                            {
                                // if an empty frame is received, stop decoding
                                m_state.pDecodedFrameQueue->endDecode();
                                m_bDecoderRunning.store(false);
                            }

                            m_inputQueue.pop();
                        }

                    }
                break;
            case WAIT_TIMEOUT:
                break;
            }
        }
    }
    else
    {
        ///////////////////////////
        // DECODING ONLY LOOP
        while(m_bDecoderRunning.load())
        {
            switch(WaitForMultipleObjects(2, handles1, FALSE, 5000)) // wait for item in input queue
            {
            case WAIT_OBJECT_0:
                m_bDecoderRunning.store(false);
                break;
            case WAIT_OBJECT_0 + 1:

				signalCount++;
                { // need this bracket so that we can declare variables inside this case block
                    // get next encoded frame from input queue
                    EncodedInputFrame* pFrame = m_inputQueue.peekHead();
					                    
                    HandleVideoData((unsigned char *)pFrame->pData, (uint32_t)pFrame->numBytes, pFrame->timestamp);

                    if((uint32_t)pFrame->numBytes == 0)
                    {
                        // if an empty frame is received, stop decoding
                        m_state.pDecodedFrameQueue->endDecode();
                        m_bDecoderRunning.store(false);
                    }

                    m_inputQueue.pop();

					if(pFrame->flushDecoder)
					{
						FlushDecoder();
					}

                }
                break;
            case WAIT_TIMEOUT:
                break;
            }
        }
    }

    m_bDecoderRunning.store(false);

	uint32_t c = SystemState::SS_get_outputQueue_countPush();


    SetEvent(m_hDecoderStoppedEvent); // indicate that decoder is stopped

    switch(m_state.decoderMode)
    {
    case DECODER_TO_CPU_ARGB:
    case DECODER_TO_CPU_RGB:
    case DECODER_TO_CPU_NV12:
		{
			// build empty frame to mark end of decoding on output queue
			DecodedFrameOnCpu* pFrame = m_state.pCpuDecodedFrameQueue->peekTail();
			pFrame->numBytes = 0;
			m_state.pCpuDecodedFrameQueue->push();
		}
        break;
    case DECODER_TO_GPU_ARGB:
    case DECODER_TO_GPU_RGB:
	case DECODER_TO_DIRECTX:
		{
			// build empty frame to mark end of decoding on output queue
			DecodedFrameOnGpu* pFrame = m_state.pGpuDecodedFrameQueue->peekTail();
			pFrame->numBytes = 0;
			m_state.pGpuDecodedFrameQueue->push();
		}
        break;
    case DECODER_TO_GPU_GPUMAT:
        break;
    case DECODER_TO_ENCODER:
        m_state.pDecodedFrameQueue->endDecode();  // probably redundant
        break;
    }

	
}



void VideoDecoder::Stop()
{
    //m_bFinished.setTrue();
    m_state.pDecodedFrameQueue->endDecode();
    SetEvent(m_hExitEvent);
}

std::string VideoDecoder::GetLastErrorMessage()
{
    return m_errMsg;
}



bool VideoDecoder::EnqueueFrame(unsigned char *pData, uint32_t numBytes, uint64_t timestamp = 0, bool flushDecoder = false)
{
    // this function attempts to enqueue a new frame in the input queue.
    // if there is room it returns true, if not, it returns false.
    //
    // if the frame is added to the queue, the m_state.inputQueueSemaphore is decremented.  The count
    // in the semaphore is meant to indicate if there is room in the queue.  The semaphore is set if
    // there is room in the queue (i.e. semaphore count > 0), and is clear if there is no room in the
    // queue (i.e. semaphore count = 0).

    if(m_inputQueue.spaceAvailable())
    {
        // get pointer to next available queue item (which is a std::vector)
        EncodedInputFrame* pFrame = m_inputQueue.peekTail();

        // copy data into frame, resizing buffer if necessary
        if(pFrame->bufferSize < numBytes)
        {
            if(pFrame->pData!=nullptr) delete pFrame->pData;
            pFrame->pData = (char*)malloc(numBytes);
            pFrame->bufferSize = numBytes;            
        }
        memcpy(pFrame->pData,pData,numBytes);
        pFrame->numBytes = numBytes;
        pFrame->timestamp = timestamp;
        pFrame->flushDecoder = flushDecoder;

        // mark this spot in the queue as having new data
        m_inputQueue.push();

        SystemState::SS_increment_inputQueue_countPush();

        return true;
    }
    else
    {
        // no room in queue, enqueue failed
        return false;
    }
}



void VideoDecoder::EndDecoding()
{    
    // get pointer to next available queue item (which is a std::vector)
    EncodedInputFrame* pFrame = m_inputQueue.peekTail();

    // create a NULL frame that signals end of the input stream
    if(pFrame->pData!=nullptr) { delete pFrame->pData; pFrame->pData = nullptr; }
    pFrame->numBytes = 0;
    pFrame->bufferSize = 0;

    // mark this spot in the queue as having new data
    m_inputQueue.push();
}


bool VideoDecoder::CreateVideoDecoder(uint32_t imageWidth, uint32_t imageHeight,
                                      cudaVideoCodec codec, CUvideoctxlock ctxLock)
{
    bool success = true;

    // Fill the decoder-create-info struct from the given video-format struct.
       memset(&m_state.dci, 0, sizeof(CUVIDDECODECREATEINFO));

    // populate CUVIDDECODECREATEINFO

       m_state.dci.CodecType           = codec;
       m_state.dci.ulWidth             = imageWidth;
       m_state.dci.ulHeight            = imageHeight;
       m_state.dci.ulNumDecodeSurfaces = FrameQueue::cnMaximumSize;
	   
       // Limit decode memory to 24MB (16M pixels at 4:2:0 = 24M bytes)
       while (m_state.dci.ulNumDecodeSurfaces * imageWidth * imageHeight > 16*1024*1024)
       {
           m_state.dci.ulNumDecodeSurfaces--;
       }

       m_state.dci.ChromaFormat        = cudaVideoChromaFormat_420;
       m_state.dci.OutputFormat        = cudaVideoSurfaceFormat_NV12;
       m_state.dci.DeinterlaceMode     = cudaVideoDeinterlaceMode_Weave;

       // No scaling
       m_state.dci.ulTargetWidth       = imageWidth;
       m_state.dci.ulTargetHeight      = imageHeight;
       m_state.dci.ulNumOutputSurfaces = 8;  // Why isn't this the same as ulNumDecodeSurfaces?
       m_state.dci.ulCreationFlags     = cudaVideoCreate_PreferCUVID;
       m_state.dci.vidLock             = ctxLock;


//       CAutoCtxLock lck(ctxLock);

       // create the decoder
       cuvidCtxLock(ctxLock,0);
       CUresult oResult = cuvidCreateDecoder(&m_state.cuDecoder, &m_state.dci);
       cuvidCtxUnlock(ctxLock,0);

       if(CUDA_SUCCESS != oResult)
       {
           // Failed to create video decoder
           success = false;
           m_errMsg = "Failed to create video decoder";

           Diagnostics::DebugMessage("Failed to Create Decoder");
           Diagnostics::DebugMessage(GetCudaErrorMessage(oResult));
       }

       return success;
}



bool VideoDecoder::CreateVideoParser(cudaVideoCodec codec, unsigned int maxDecodedSurfaces)
{
//    CAutoCtxLock lck(m_state.cuCtxLock);

    bool success = true;

    CUVIDPARSERPARAMS oVideoParserParameters;
    memset(&oVideoParserParameters, 0, sizeof(CUVIDPARSERPARAMS));
    oVideoParserParameters.CodecType              = codec;
    oVideoParserParameters.ulMaxNumDecodeSurfaces = maxDecodedSurfaces;
    oVideoParserParameters.ulMaxDisplayDelay      = 1;  // this flag is needed so the parser will push frames out to the decoder as quickly as it can
    oVideoParserParameters.pUserData              = &m_state;
    oVideoParserParameters.pfnSequenceCallback    = HandleVideoSequence;    // Called before decoding frames and/or whenever there is a format change
    oVideoParserParameters.pfnDecodePicture       = HandlePictureDecode;    // Called when a picture is ready to be decoded (decode order)
    oVideoParserParameters.pfnDisplayPicture      = HandlePictureDisplay;   // Called whenever a picture is ready to be displayed (display order)
	
    cuvidCtxLock(m_state.cuCtxLock,0);
    CUresult oResult = cuvidCreateVideoParser(&m_state.cuParser, &oVideoParserParameters);
    cuvidCtxUnlock(m_state.cuCtxLock,0);
    if(CUDA_SUCCESS != oResult)
    {
        Diagnostics::DebugMessage("Failed to Create Parser");
        Diagnostics::DebugMessage(GetCudaErrorMessage(oResult));
        success = false;
    }

    return success;
}





void VideoDecoder::ConfigureDecoder(int outputWidth, int outputHeight, DECODER_MODE mode, cudaVideoCodec codec)
{
    if(m_bDecoderRunning.load())
    {
        Diagnostics::DebugMessage("Error: cannot call ConfigureDecoder while decoder is running. Attempt to configure ignored.");
        return;
    }

    m_state.dci.ulTargetWidth = outputWidth;
    m_state.dci.ulTargetHeight = outputHeight;
    m_state.decoderMode = mode;
    m_state.hExitEvent = m_hExitEvent;

    m_codec = codec;

    switch(mode)
    {
    case DECODER_TO_CPU_ARGB:
        m_state.decodedFormat = FORMAT_ARGB;
        m_hOutputItems = m_outputQueueCpu.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueCpu.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;
        break;
    case DECODER_TO_CPU_RGB:
        m_state.decodedFormat = FORMAT_RGB;
        m_hOutputItems = m_outputQueueCpu.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueCpu.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;
        break;
    case DECODER_TO_GPU_ARGB:	
        m_state.decodedFormat = FORMAT_ARGB;
        m_hOutputItems = m_outputQueueGpu.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueGpu.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;
        break;
    case DECODER_TO_GPU_RGB:
        m_state.decodedFormat = FORMAT_RGB;
        m_hOutputItems = m_outputQueueGpu.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueGpu.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;
        break;
    case DECODER_TO_GPU_GPUMAT:
        m_state.decodedFormat = FORMAT_GPUMAT;
        m_hOutputItems = m_outputQueueGpuMat.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueGpuMat.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;
        break;    
    case DECODER_TO_ENCODER:
        m_state.decodedFormat = FORMAT_NV12;
        break;
    case DECODER_TO_CPU_NV12:
        m_state.decodedFormat = FORMAT_NV12;
        m_hOutputItems = m_outputQueueCpu.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueCpu.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;
        break;
	case DECODER_TO_DIRECTX:
		m_state.decodedFormat = FORMAT_ARGB;
		m_hOutputItems = m_outputQueueGpu.GetSemaphore_ItemsInQueue();
        m_hOutputSpaces = m_outputQueueGpu.GetSemaphore_SpacesInQueue();
        m_state.hOutputSpaces = m_hOutputSpaces;		
		break;
    }
}




void VideoDecoder::HandleVideoData(unsigned char *packet, uint32_t numBytes, uint64_t timestamp)
{
    // called when a new frame of data has been received

    // packet is the raw packet of image data (not yet decoded)
    // numBytes is the number of bytes in packet

    CUresult result;
    CUVIDSOURCEDATAPACKET pkt;
	
    if (numBytes > 0)
    {
        if(timestamp == 0)
            pkt.flags = 0;
        else
            pkt.flags = CUVID_PKT_TIMESTAMP;
        pkt.payload_size = numBytes;
        pkt.payload = packet;
        pkt.timestamp = timestamp;
        result = cuvidParseVideoData(m_state.cuParser, &pkt);
        if(CUDA_SUCCESS != result)
        {
            Diagnostics::DebugMessage("Failed to Parse Video Data");
            Diagnostics::DebugMessage(GetCudaErrorMessage(result));
        }
    }
    else
    {
        // Flush the decoder
        pkt.flags = CUVID_PKT_ENDOFSTREAM;
        pkt.payload_size = 0;
        pkt.payload = NULL;
        pkt.timestamp = 0;
        result = cuvidParseVideoData(m_state.cuParser, &pkt);
    }
}



int CUDAAPI VideoDecoder::HandleVideoSequence(void *pUserData, CUVIDEOFORMAT *pFormat)
{
    // Callback function
    // Called by the video parser before decoding frames and/or whenever there is a format change

    DecodeSession *state = (DecodeSession *)pUserData;


    if ((pFormat->codec != state->dci.CodecType)
        || (pFormat->coded_width != state->dci.ulWidth)
        || (pFormat->coded_height != state->dci.ulHeight)
        || (pFormat->chroma_format != state->dci.ChromaFormat))
    {
//        CAutoCtxLock lck(state->cuCtxLock);
        cuvidCtxLock(state->cuCtxLock,0);
        if (state->cuDecoder)
        {
            cuvidDestroyDecoder(state->cuDecoder);
            state->cuDecoder = NULL;
        }


        state->dci.ulWidth = pFormat->coded_width;
        state->dci.ulHeight = pFormat->coded_height;
        state->dci.ulNumDecodeSurfaces = MAX_FRM_CNT;
        state->dci.CodecType = pFormat->codec;
        state->dci.ChromaFormat = pFormat->chroma_format;
        // Output (pass through)
        state->dci.OutputFormat = cudaVideoSurfaceFormat_NV12;
        state->dci.DeinterlaceMode = cudaVideoDeinterlaceMode_Weave; // No deinterlacing

        if(state->dci.ulTargetWidth == 0 || state->dci.ulTargetHeight == 0)
        {
            state->dci.ulTargetWidth = state->dci.ulWidth;
            state->dci.ulTargetHeight = state->dci.ulHeight;
        }

        // need to initialize the GpuMat circular buffer if we're in that mode
        if(state->decoderMode == DECODER_TO_GPU_GPUMAT)
            state->pGpuMatQueue->Init(state->dci.ulTargetWidth,state->dci.ulTargetHeight);

//        state->dci.ulTargetWidth = state->dci.ulWidth; // decode to same size (no scaling)
//        state->dci.ulTargetHeight = state->dci.ulHeight;
        state->dci.ulNumOutputSurfaces = 8;
        state->dci.ulCreationFlags = cudaVideoCreate_PreferCUVID;
        state->dci.vidLock = state->cuCtxLock;

        // TEST
        state->pDecodedFrameQueue->init(state->dci.ulWidth, state->dci.ulHeight);
        // END TEST

        // Create the decoder
        CUresult result = cuvidCreateDecoder(&state->cuDecoder, &state->dci);

        cuvidCtxUnlock(state->cuCtxLock,0);

        if (result != CUDA_SUCCESS)
        {
            Diagnostics::DebugMessage("Failed to Create Decoder");
            //Diagnostics::DebugMessage(GetCudaErrorMessage(result));
            return 0;
        }

    }

    return 1;
}

int CUDAAPI VideoDecoder::HandlePictureDecode(void *pUserData, CUVIDPICPARAMS *pPicParams)
{
    // Callback function
    // Called by video parser when a picture is ready to be decoded (decode order)


    DecodeSession *state = (DecodeSession *)pUserData;


    switch(state->decoderMode)
    {
    case DECODER_TO_CPU_ARGB:
        break;
    case DECODER_TO_CPU_RGB:
        break;
    case DECODER_TO_GPU_ARGB:
        break;
    case DECODER_TO_GPU_RGB:
        break;
    case DECODER_TO_GPU_GPUMAT:
        break;
    case DECODER_TO_ENCODER:
        // if transcoding, wait for the FrameQueue to have an available slot
        state->pDecodedFrameQueue->waitUntilFrameAvailable(pPicParams->CurrPicIdx);
        break;
    case DECODER_TO_CPU_NV12:
        break;
	case DECODER_TO_DIRECTX:
		break;
    }


//    CAutoCtxLock lck(state->cuCtxLock);

    cuvidCtxLock(state->cuCtxLock,0);

    CUresult result = cuvidDecodePicture(state->cuDecoder, pPicParams); 

    cuvidCtxUnlock(state->cuCtxLock,0);

    if (result != CUDA_SUCCESS)
    {
        Diagnostics::DebugMessage("Failed to Decode Picture");
        //Diagnostics::DebugMessage(GetCudaErrorMessage(result));
    }

    return (result == CUDA_SUCCESS);
}



int CUDAAPI VideoDecoder::HandlePictureDisplay(void *pUserData, CUVIDPARSERDISPINFO *pPicParams)
{   
    // Callback function
    // called by parser when a decoded frame is ready (still on the gpu)

    DecodeSession *state = (DecodeSession *)pUserData;

    if((state->skipCounter++ % state->skipCount) != 0)
    {
         return 1; // skip
    }


    switch(state->decoderMode)
    {
    case DECODER_TO_CPU_ARGB:
        PostProcess_ToCpuARGB(state,pPicParams);
        state->count++;
        break;
    case DECODER_TO_CPU_RGB:
        PostProcess_ToCpuRGB(state,pPicParams);
        state->count++;
        break;
    case DECODER_TO_GPU_ARGB:
        PostProcess_ToGpuARGB(state,pPicParams);
        state->count++;
        break;
    case DECODER_TO_GPU_RGB:
        PostProcess_ToGpuRGB(state,pPicParams);
        state->count++;
        break;
    case DECODER_TO_GPU_GPUMAT:
        PostProcess_ToGpuMatQueue(state,pPicParams);
        state->count++;
        break;
    case DECODER_TO_ENCODER:
        // call this section to push decoded NV12 frame onto frame queue(i.e. if transcoding)
        state->pDecodedFrameQueue->enqueue(pPicParams);
        state->count++;
        break;
    case DECODER_TO_CPU_NV12:
        PostProcess_ToCpuNV12(state,pPicParams);
        state->count++;
        break;
	case DECODER_TO_DIRECTX:
		PostProcess_ToDirectX(state,pPicParams);
		state->count++;
		break;
    }

    return 1;
}




int VideoDecoder::PostProcess_ToGpuMatQueue(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{
    // this method performs the following actions:
    //  1 - Convert the last decoded frame from NV12 to ARGB.  This decoded frame is in the FrameQueue.
    //      This is accomplished by calling the NV12ToARGB CUDA kernel.  A pointer to this function is
    //      passed into this method inside the DecodeSession (pstate->postProcFunc).
    //  2 - Copy the ARGB frame, that is output from step 1, into an existing OpenCV GpuMat.  You simply
    //      pass the data pointer from the GpuMat (GpuMat.ptr()) into the CudaToGpuMat CUDA kernel.  A pointer
    //      to this kernel is passed into this method inside the DecodeSession (pstate->postProcFunc2).
    //  3 - The output from step 2 ends up in the GpuMatFIFO.  The GpuMatFIFO is a circular buffer that holds
    //      a fixed number of GpuMat objects.  This function will set the data inside the next available
    //      GpuMat object that is in this FIFO.

    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;

//    CAutoCtxLock lck(pstate->cuCtxLock);

    CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device

    if(pstate->argbFrame == 0)
    {
        result = cuMemAlloc(&pstate->argbFrame, pstate->dci.ulTargetWidth * pstate->dci.ulTargetHeight * 4);
        if(result != CUDA_SUCCESS)
        {

			Diagnostics::DebugMessage("PostProcess_ToGpuMatQueue - failed to allocate memory on gpu for ARGB frame");

            return 0;  // failed to Post Process Frame
        }
    }


    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index,
                                         &pDecodedFrame, &decodedPitch, &vpp);

    w = pstate->dci.ulTargetWidth;
    h = pstate->dci.ulTargetHeight;
    nv12_size = decodedPitch * (h + h/2);  // 12bpp

    size_t texturePitch = pstate->dci.ulTargetWidth * 4;

    uint32_t nWidth  = pstate->dci.ulTargetWidth;
    uint32_t nHeight = pstate->dci.ulTargetHeight;
   
	cuda_NV12ToARGB(pDecodedFrame, pstate->argbFrame, nWidth, nHeight, decodedPitch, texturePitch, pstate->cuStream);

    result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);

    if(result == CUDA_SUCCESS)
    {
        HANDLE handles[] = {pstate->hExitEvent,pstate->hOutputSpaces};
        switch(WaitForMultipleObjects(2, handles, FALSE, INFINITE)) // wait for item in input queue
        {
        case WAIT_OBJECT_0: // exit event
            // EXIT/ABORT
            break;
        case WAIT_OBJECT_0 + 1: // wait for space in output queue
			{
				// call Cuda Kernel that copies data from the ARGB frame to the GpuMat object

				// get the next GpuMat in the GpuMatFIFO that is free for use (the Tail item in the FIFO)
				GPUMAT_FIFO_OBJECT * pGpuMatObj = pstate->pGpuMatQueue->peekTail();

				if(pGpuMatObj != NULL)  // is there a free GpuMat in the GpuMatFIFO AND don't skip this one
				{
					// get pointer to data in GpuMat										
					CUdeviceptr gpuMatDataPtr = (CUdeviceptr)pGpuMatObj->pGpuMat->ptr();

					cuda_CudaToGpuMat(gpuMatDataPtr, pstate->argbFrame,  nWidth,  nHeight);

					pGpuMatObj->timestamp = pPicParams->timestamp;

					if(result == CUDA_SUCCESS)
					{
						pstate->pGpuMatQueue->push(); // increments the tail pointer of the GpuMatFIFO circular buffer
					}
				}
			}
			break;
        }  

    }

    return 1;
}



int VideoDecoder::PostProcess_ToCpuRGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{
    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;


//    CAutoCtxLock lck(pstate->cuCtxLock);

    CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device

    if(pstate->argbFrame == 0)
    {
        result = cuMemAlloc(&pstate->argbFrame, pstate->dci.ulTargetWidth * pstate->dci.ulTargetHeight * 4);
        if(result != CUDA_SUCCESS)
        {
			Diagnostics::DebugMessage("PostProcess_ToCpuRGB - failed to allocate memory on gpu for ARGB frame");

            return 0;  // failed to Post Process Frame
        }
    }


    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index,
                                         &pDecodedFrame, &decodedPitch, &vpp);

    w = pstate->dci.ulTargetWidth;
    h = pstate->dci.ulTargetHeight;
    nv12_size = decodedPitch * (h + h/2);  // 12bpp

    size_t texturePitch = pstate->dci.ulTargetWidth * 4;

    uint32_t nWidth  = pstate->dci.ulTargetWidth;
    uint32_t nHeight = pstate->dci.ulTargetHeight;
  

    /////////////////////////////////////////////////////////////////////////

    result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);

    if(result == CUDA_SUCCESS)
    {

        // run kernel that converts ARGB frame to an RGB frame

            // allocate space on gpu for constructed RGB frame
            CUdeviceptr rgbFrame;
            result = cuMemAlloc(&rgbFrame,nWidth*nHeight*3);
			           
			cuda_NV12ToRGB(pDecodedFrame, rgbFrame, nWidth,  nHeight, decodedPitch, nWidth*3);
			
            if(result == CUDA_SUCCESS)
            {
                HANDLE handles[] = {pstate->hExitEvent,pstate->hOutputSpaces};
                switch(WaitForMultipleObjects(2, handles, FALSE, INFINITE)) // wait for item in input queue
                {
                case WAIT_OBJECT_0: // exit event
                    // EXIT/ABORT
                    break;
                case WAIT_OBJECT_0 + 1: // wait for space in output queue
                // copy RGB frame from GPU to Host
                    // allocate space on the Host for the RGB frame
                    size_t numBytes = w * h * 3;

                    // get pointer to next available frame in queue
                    DecodedFrameOnCpu* pFrame = pstate->pCpuDecodedFrameQueue->peekTail();

                    // check to see if there's enough room in the queue slot to hold the image.  if not, make room.
                    if(pFrame->bufferSize < numBytes)
                    {
                        if(pFrame->pData!=nullptr) delete pFrame->pData;
                        pFrame->pData = (char*)malloc(numBytes);
                        pFrame->bufferSize = numBytes;
                    }

                    // copy data into the frame
                    result = cuMemcpyDtoH(pFrame->pData, rgbFrame, numBytes);
                    pFrame->format = FORMAT_RGB;
                    pFrame->height = h;
                    pFrame->width = w;
                    pFrame->numBytes = numBytes;
                    pFrame->timestamp = (uint64_t)pPicParams->timestamp;

                    // push output queue to signal that this frame has data in it
                        pstate->pCpuDecodedFrameQueue->push();
                    break;
                }


                // release RGB frame memory on GPU
                result = cuMemFree(rgbFrame);
            }
    }

    return 1;
}

int VideoDecoder::PostProcess_ToCpuARGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{  
    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;

//    CAutoCtxLock lck(pstate->cuCtxLock);

    CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device

    if(pstate->argbFrame == 0)
    {
        result = cuMemAlloc(&pstate->argbFrame, pstate->dci.ulTargetWidth * pstate->dci.ulTargetHeight * 4);
        if(result != CUDA_SUCCESS)
        {
			Diagnostics::DebugMessage("PostProcess_ToCpuARGB - failed to allocate memory on gpu for ARGB frame");

            return 0;  // failed to Post Process Frame
        }
    }


    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index,
                                         &pDecodedFrame, &decodedPitch, &vpp);


    w = pstate->dci.ulTargetWidth;
    h = pstate->dci.ulTargetHeight;
    nv12_size = decodedPitch * (h + h/2);  // 12bpp

    size_t texturePitch = pstate->dci.ulTargetWidth * 4;


    /////////////////////////////////////////////////////////////////////////


    // Each thread will output 2 pixels at a time.  The grid size width is half
    // as large because of this
    uint32_t nWidth  = pstate->dci.ulTargetWidth;
    uint32_t nHeight = pstate->dci.ulTargetHeight;
    
	cuda_NV12ToARGB(pDecodedFrame, pstate->argbFrame, nWidth, nHeight, decodedPitch, texturePitch, pstate->cuStream);

    result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);

    if(result == CUDA_SUCCESS)
    {
        HANDLE handles[] = {pstate->hExitEvent,pstate->hOutputSpaces};
        switch(WaitForMultipleObjects(2, handles, FALSE, INFINITE)) // wait for item in input queue
        {
        case WAIT_OBJECT_0: // exit event
            // EXIT/ABORT
            break;
        case WAIT_OBJECT_0 + 1: // wait for space in output queue
            // copy ARGB frame from GPU to Host

            // allocate space on the Host for the ARGB frame
            size_t numBytes = w * h * 4;

            // get pointer to next available frame in queue
            DecodedFrameOnCpu* pFrame = pstate->pCpuDecodedFrameQueue->peekTail();

            // check to see if there's enough room in the queue slot to hold the image.  if not, make room.
            if(pFrame->bufferSize < numBytes)
            {
                if(pFrame->pData!=nullptr) delete pFrame->pData;
                pFrame->pData = (char*)malloc(numBytes);
                pFrame->bufferSize = numBytes;
            }

            // copy data into the frame
            result = cuMemcpyDtoH(pFrame->pData, pstate->argbFrame, numBytes);
            pFrame->format = FORMAT_ARGB;
            pFrame->height = h;
            pFrame->width = w;
            pFrame->numBytes = numBytes;
            pFrame->timestamp = (uint64_t)pPicParams->timestamp;

            // push output queue to signal that this frame has data in it
            pstate->pCpuDecodedFrameQueue->push();

        break;
        }

    }

    return 1;
}

int VideoDecoder::PostProcess_ToGpuRGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{
    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;


//    CAutoCtxLock lck(pstate->cuCtxLock);

    CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device

    if(pstate->argbFrame == 0)
    {
        result = cuMemAlloc(&pstate->argbFrame, pstate->dci.ulTargetWidth * pstate->dci.ulTargetHeight * 4);
        if(result != CUDA_SUCCESS)
        {
			Diagnostics::DebugMessage("PostProcess_ToGpuRGB - failed to allocate memory on gpu for ARGB frame");

            return 0;  // failed to Post Process Frame
        }
    }


    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index,
                                         &pDecodedFrame, &decodedPitch, &vpp);


    w = pstate->dci.ulTargetWidth;
    h = pstate->dci.ulTargetHeight;
    nv12_size = decodedPitch * (h + h/2);  // 12bpp

    size_t texturePitch = pstate->dci.ulTargetWidth * 4;

    uint32_t nWidth  = pstate->dci.ulTargetWidth;
    uint32_t nHeight = pstate->dci.ulTargetHeight;

    result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);

    if(result == CUDA_SUCCESS)
    {

        HANDLE handles[] = {pstate->hExitEvent,pstate->hOutputSpaces};

        switch(WaitForMultipleObjects(2, handles, FALSE, INFINITE)) // wait for space in output queue

        {
        case WAIT_OBJECT_0: // exit event
            // EXIT/ABORT
            break;
        case WAIT_OBJECT_0 + 1: // wait for space in output queue
            // run kernel that converts ARGB frame to an RGB frame

                // get pointer to next available frame in queue
                DecodedFrameOnGpu* pFrame = pstate->pGpuDecodedFrameQueue->peekTail();

                pFrame->format = FORMAT_RGB;
                pFrame->height = nHeight;
                pFrame->width = nWidth;
                pFrame->numBytes = nWidth * nHeight * 3;
                pFrame->timestamp = (uint64_t)pPicParams->timestamp;

                if(pFrame->bufferSize < pFrame->numBytes)
                {
                    // free the existing buffer (that's too small)
                    cudaFree((void*)pFrame->pData);

                    // allocate the new buffer on gpu for constructed RGB frame
                    result = cuMemAlloc( &pFrame->pData,pFrame->numBytes);
                    pFrame->bufferSize = pFrame->numBytes;
                }
               
				cuda_NV12ToRGB(pDecodedFrame, pFrame->pData, nWidth,  nHeight, decodedPitch, nWidth*3);

                pstate->pGpuDecodedFrameQueue->push();

        break;
        }

    }

    return 1;
}

int VideoDecoder::PostProcess_ToGpuARGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{
    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;


//    CAutoCtxLock lck(pstate->cuCtxLock);

    CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device

    if(pstate->argbFrame == 0)
    {
        result = cuMemAlloc(&pstate->argbFrame, pstate->dci.ulTargetWidth * pstate->dci.ulTargetHeight * 4);
        if(result != CUDA_SUCCESS)
        {
			Diagnostics::DebugMessage("PostProcess_ToGpuARGB - failed to allocate memory on gpu for ARGB frame");

            return 0;  // failed to Post Process Frame
        }
    }


    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index, &pDecodedFrame, &decodedPitch, &vpp);


    w = pstate->dci.ulTargetWidth;
    h = pstate->dci.ulTargetHeight;
    nv12_size = decodedPitch * (h + h/2);  // 12bpp

    size_t texturePitch = pstate->dci.ulTargetWidth * 4;

    uint32_t nWidth  = pstate->dci.ulTargetWidth;
    uint32_t nHeight = pstate->dci.ulTargetHeight;

	cuda_NV12ToARGB(pDecodedFrame, pstate->argbFrame, nWidth, nHeight, decodedPitch, texturePitch,pstate->cuStream);

	cudaEvent_t event;
	cudaEventCreateWithFlags(&event, cudaEventDisableTiming);
	cudaEventRecord(event, pstate->cuStream);
	cudaStreamWaitEvent(pstate->cuStream, event, 0);
	cudaEventDestroy(event);

    result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);

    if(result == CUDA_SUCCESS)
    {
        HANDLE handles[] = {pstate->hExitEvent,pstate->hOutputSpaces};
        switch(WaitForMultipleObjects(2, handles, FALSE, INFINITE)) // wait for space in output queue
        {
        case WAIT_OBJECT_0: // exit event
            // EXIT/ABORT
            break;
        case WAIT_OBJECT_0 + 1: // wait for space in output queue
            // build DecodedFrameOnGpu struct to point to newly decoded frame

            // get pointer to next available frame in queue
            DecodedFrameOnGpu* pFrame = pstate->pGpuDecodedFrameQueue->peekTail();

            pFrame->format = FORMAT_RGB;
            pFrame->height = nHeight;
            pFrame->width = nWidth;
            pFrame->numBytes = nWidth * nHeight * 4;
            pFrame->timestamp = (uint64_t)pPicParams->timestamp;

             if(pFrame->bufferSize < pFrame->numBytes)
             {
                 // free existing buffer
                 cudaFree((void*)pFrame->pData);

                 // allocate new buffer
                 cuMemAlloc(&pFrame->pData,pFrame->numBytes);
                 pFrame->bufferSize = pFrame->numBytes;
             }

             cuMemcpy(pFrame->pData,pstate->argbFrame,pFrame->numBytes);

            pstate->pGpuDecodedFrameQueue->push();
        break;
        }
    }

    return 1;
}






int VideoDecoder::PostProcess_ToCpuNV12(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{
    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;

        w = pstate->dci.ulTargetWidth;
        h = pstate->dci.ulTargetHeight;
        CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device



    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index,
                                         &pDecodedFrame, &decodedPitch, &vpp);

    nv12_size = decodedPitch * (h + h/2);  // 12bpp



    if(result == CUDA_SUCCESS)
    {
        HANDLE handles[] = {pstate->hExitEvent,pstate->hOutputSpaces};
        switch(WaitForMultipleObjects(2, handles, FALSE, INFINITE)) // wait for item in input queue
        {
        case WAIT_OBJECT_0: // exit event
            // EXIT/ABORT
            break;
        case WAIT_OBJECT_0 + 1: // wait for space in output queue
            // copy NV12 frame from GPU to Host

            // allocate space on the Host for the NV12 frame
            size_t numBytes = nv12_size;

            // get pointer to next available frame in queue
            DecodedFrameOnCpu* pFrame = pstate->pCpuDecodedFrameQueue->peekTail();

            // check to see if there's enough room in the queue slot to hold the image.  if not, make room.
            if(pFrame->bufferSize < numBytes)
            {
                if(pFrame->pData!=nullptr) delete pFrame->pData;
                pFrame->pData = (char*)malloc(numBytes);
                pFrame->bufferSize = numBytes;
            }

            // copy data into the frame
    //        result = cuMemcpyDtoH(pFrame->pData, pDecodedFrame, numBytes);
            pFrame->format = FORMAT_NV12;
            pFrame->height = h;
            pFrame->width = w;
            pFrame->numBytes = numBytes;
            pFrame->timestamp = (uint64_t)pPicParams->timestamp;

            // push output queue to signal that this frame has data in it
            pstate->pCpuDecodedFrameQueue->push();
        break;
        }

    }

    result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);


    return 1;
}



int VideoDecoder::PostProcess_ToDirectX(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams)
{
    CUresult result;
    CUVIDPROCPARAMS vpp;
    unsigned int decodedPitch = 0, w, h;
    int nv12_size;


//    CAutoCtxLock lck(pstate->cuCtxLock);

    CUdeviceptr  pDecodedFrame =  0; // pointer to the decoded NV12 frame on the device

    if(pstate->argbFrame == 0)
    {
        result = cuMemAlloc(&pstate->argbFrame, pstate->dci.ulTargetWidth * pstate->dci.ulTargetHeight * 4);
        if(result != CUDA_SUCCESS)
        {
			Diagnostics::DebugMessage("PostProcess_ToGpuARGB - failed to allocate memory on gpu for ARGB frame");

            return 0;  // failed to Post Process Frame
        }
    }


    memset(&vpp, 0, sizeof(CUVIDPROCPARAMS));
    vpp.progressive_frame = pPicParams->progressive_frame;
    vpp.top_field_first = pPicParams->top_field_first;
    vpp.unpaired_field = 1;


    // map decoded video frame to CUDA surface
    result = cuvidMapVideoFrame(pstate->cuDecoder, pPicParams->picture_index,
                                         &pDecodedFrame, &decodedPitch, &vpp);


    w = pstate->dci.ulTargetWidth;
    h = pstate->dci.ulTargetHeight;
    nv12_size = decodedPitch * (h + h/2);  // 12bpp

    size_t texturePitch = pstate->dci.ulTargetWidth * 4;

    uint32_t nWidth  = pstate->dci.ulTargetWidth;
    uint32_t nHeight = pstate->dci.ulTargetHeight;

	cuda_NV12ToARGB(pDecodedFrame, pstate->argbFrame, nWidth, nHeight, decodedPitch, texturePitch, pstate->cuStream);

    
	result = cuvidUnmapVideoFrame(pstate->cuDecoder, pDecodedFrame);
	

    if(result == CUDA_SUCCESS)
    {		

		if (!pstate->pD3D9->bDeviceLost)
		{
			cudaError_t result_t;
			cudaStream_t    stream = 0;
			const int nbResources = 1;
			cudaGraphicsResource *ppResources[nbResources] =
			{
				pstate->pD3D9->cudaResource
			};

			result_t = cudaGraphicsMapResources(nbResources, ppResources, stream);

			
			//
			// run kernels which will populate the contents of those textures
			//

			cuda_CopyCudaArrayToD3D9Texture((CUdeviceptr)pstate->pD3D9->cudaLinearMemory,
											pstate->argbFrame,
											pstate->pD3D9->pitch,
											pstate->pD3D9->width,
											pstate->pD3D9->height);



			cudaArray *cuArray;
			cudaGraphicsSubResourceGetMappedArray(&cuArray, pstate->pD3D9->cudaResource, 0, 0);

			// then we want to copy cudaLinearMemory to the D3D texture, via its mapped form : cudaArray
			cudaMemcpy2DToArray(
				cuArray, // dst array
				0, 0,    // offset
				pstate->pD3D9->cudaLinearMemory, pstate->pD3D9->width*4,       // src
				pstate->pD3D9->width*4, pstate->pD3D9->height, // extent
				cudaMemcpyDeviceToDevice); // kind



			//
			// unmap the resources
			//
	

			result_t = cudaGraphicsUnmapResources(nbResources, ppResources, stream);

			DrawScene(pstate->pD3D9);
			
		}

    }


    return 1;
}



CircularFifoCpu* VideoDecoder::GetCpuDecodedFrameQueue()
{
    return &m_outputQueueCpu;
}

GpuMatFIFO *VideoDecoder::GetGpuMatFifo()
{
    return &m_outputQueueGpuMat;
}

CircularFifoGpu* VideoDecoder::GetGpuDecodedFrameQueue()
{
    return &m_outputQueueGpu;
}

bool VideoDecoder::FrameReadyCpu()
{
    return m_outputQueueCpu.itemAvailable();
}

bool VideoDecoder::FrameReadyGpu()
{
    return m_outputQueueGpu.itemAvailable();
}

void VideoDecoder::FlushDecoder()
{
    CUresult result;
    CUVIDSOURCEDATAPACKET pkt;

    // Flush the decoder
    pkt.flags = CUVID_PKT_ENDOFSTREAM;
    pkt.payload_size = 0;
    pkt.payload = NULL;
    pkt.timestamp = 0;
    result = cuvidParseVideoData(m_state.cuParser, &pkt);

    ReleaseSemaphore(m_hOutputSpaces,1,NULL);
}


DecodeSession *VideoDecoder::GetDecodeSession()
{
    return &m_state;
}


void VideoDecoder::VideoSourceFinished()
{
//    m_bFinished.setTrue();
    SetEvent(m_hExitEvent);
}


bool VideoDecoder::GetNextDecodedFrameCpu(char** ppData, uint32_t* pNumBytes, uint32_t* pWidth, uint32_t* pHeight, DECODED_IMAGE_FORMAT* pFormat, uint64_t* pTimestamp)
{
    // this function does the following:
    //  1 - checks to see if there are any decoded frames in the queue, if not, it returns false and nulls out all data
    //
    //  2 - if there is a frame in the queue, it returns true and populates the parameters passed in
    //

    bool success = false;

    if(m_state.pCpuDecodedFrameQueue->itemAvailable())
    {
        // get pointer to the next frame
        DecodedFrameOnCpu* pFrame = m_state.pCpuDecodedFrameQueue->peekHead();

        // use the image for something
        *ppData = pFrame->pData;
        *pNumBytes = pFrame->numBytes;
        *pWidth = pFrame->width;
        *pHeight = pFrame->height;
        *pFormat = pFrame->format;
        *pTimestamp = pFrame->timestamp;

        success = true;
    }
    else
    {
        *ppData = nullptr;
        *pNumBytes = 0;
        *pWidth = 0;
        *pHeight = 0;
        *pFormat = FORMAT_NONE;
        *pTimestamp = 0;
    }

   return success;

}

void VideoDecoder::ReleaseFrame()
{
    // when finished using the data in the current queue position, call this function to release it for re-use
    m_state.pCpuDecodedFrameQueue->pop();
}

bool VideoDecoder::GetNextDecodedFrameGpu(CUdeviceptr* ppData, uint32_t* pNumBytes, uint32_t* pWidth, uint32_t* pHeight, DECODED_IMAGE_FORMAT* pFormat, uint64_t* pTimestamp)
{
    // this function does the following:
    //  1 - checks to see if there are any decoded frames in the queue, if not, it returns false and nulls out all data
    //
    //  2 - if there is a frame in the queue, it returns true and populates the parameters passed in
    //

    bool success = false;

    if(m_state.pGpuDecodedFrameQueue->itemAvailable())
    {
        // get pointer to the next frame
        DecodedFrameOnGpu* pFrame = m_state.pGpuDecodedFrameQueue->peekHead();

        // use the image for something
        *ppData = pFrame->pData;
        *pNumBytes = pFrame->numBytes;
        *pWidth = pFrame->width;
        *pHeight = pFrame->height;
        *pFormat = pFrame->format;
        *pTimestamp = pFrame->timestamp;

        success = true;
    }
    else
    {
        *ppData = NULL;
        *pNumBytes = 0;
        *pWidth = 0;
        *pHeight = 0;
        *pFormat = FORMAT_NONE;
        *pTimestamp = 0;
    }

   return success;

}

void VideoDecoder::ReleaseFrameGpu()
{
    // when finished using the data in the current queue position, call this function to release it for re-use
    m_state.pGpuDecodedFrameQueue->pop();
}


bool VideoDecoder::CopyGpuFrameToCpuFrame(char *pDestCpu, CUdeviceptr pSourceGpu, uint32_t numBytes)
{	
	CUresult result = cuMemcpyDtoH(pDestCpu, pSourceGpu, numBytes);

	return true;
}


GPUMAT_FIFO_OBJECT *VideoDecoder::PeekNextGpuMat()
{
    // this function returns a pointer to the next GpuMat in the GpuMat queue.
    // it does not free up this position in the queue.  In order to free this position, you must call PopNextGpuMat()
    // returns valid pointer if successful
    // returns NULL if queue is empty
    return m_outputQueueGpuMat.peekHead();
}



bool VideoDecoder::PopNextGpuMat()
{
    // this function releases this position in the GpuMat queue
    // returns true is successful
    // returns false if queue was empty
    return m_outputQueueGpuMat.pop();
}



void VideoDecoder::GetDecodedImageSize(uint32_t *pWidth, uint32_t *pHeight)
{
    *pWidth = m_state.dci.ulTargetWidth;
    *pHeight = m_state.dci.ulTargetHeight;
}

void VideoDecoder::GetWindowsHandles(HANDLE *pInputQueueSpaceAvailableSemaphore,
                                     HANDLE *pOutputQueueSemaphore,
                                     HANDLE *pDecoderStoppedEvent)
{
    *pInputQueueSpaceAvailableSemaphore  = m_hInputSpaces;
    *pOutputQueueSemaphore = m_hOutputItems;
    *pDecoderStoppedEvent = m_hDecoderStoppedEvent;
}

HANDLE VideoDecoder::GetInputQueueSpaceAvailSemaphore()
{
    return m_hInputSpaces;
}

HANDLE VideoDecoder::GetFrameQueueSemaphore()
{
    return m_state.pDecodedFrameQueue->GetFrameQueueSemaphore();
}

HANDLE VideoDecoder::GetDecoderStoppedEvent()
{
    return m_hDecoderStoppedEvent;
}

bool VideoDecoder::GetDecoderRunning()
{
    bool running = m_bDecoderRunning.load();
    return running;
}

uint32_t VideoDecoder::InputQueue_GetPushCount()
{
    return SystemState::SS_get_inputQueue_countPush();
}

uint32_t VideoDecoder::InputQueue_GetPopCount()
{
    return SystemState::SS_get_inputQueue_countPop();
}

std::string VideoDecoder::GetCudaErrorMessage(CUresult result)
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

void VideoDecoder::SetSkipCount(int count)
{
    if(count<1) count = 1;
    if(count>10000) count = 10000;
    m_state.skipCount = count;
}

CUstream VideoDecoder::GetCudaStream()
{
	return m_state.cuStream;
}



///////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//	Direct X Routines


bool VideoDecoder::Init_D3DSurface(IDirect3DSurface9 *pSurface, int width, int height)
{	
	// this function is used with the WPF D3DImage class.  
	// pSurface = 

	m_state.pD3D9->pSurface = pSurface;
	m_state.pD3D9->width = width;
	m_state.pD3D9->height = height;
	m_state.pD3D9->pitch = width;	

	bool success = true;

	// check to see if a surface already exists for given surface index...if so, release the resources it has
	if (m_state.pD3D9->cudaLinearMemory != 0)
	{
		cudaFree(m_state.pD3D9->cudaLinearMemory);
	}
	if (m_state.pD3D9->cudaResource != 0)
	{
		cudaFree(m_state.pD3D9->cudaResource);
	}

	cudaError_t res = cudaGraphicsD3D9RegisterResource(&m_state.pD3D9->cudaResource, m_state.pD3D9->pSurface, cudaGraphicsRegisterFlagsNone);

	// cuda cannot write into the texture directly : the texture is seen as a cudaArray and can only be mapped as a texture
	// Create a buffer so that cuda can write into it
	// pixel fmt is DXGI_FORMAT_R8G8B8A8_SNORM
	if (res == cudaSuccess)
	{
		res = cudaMallocPitch(&m_state.pD3D9->cudaLinearMemory, &m_state.pD3D9->pitch, m_state.pD3D9->width * 4, m_state.pD3D9->height);
		if (res == cudaSuccess)
		{
			res = cudaMemset(m_state.pD3D9->cudaLinearMemory, 1, m_state.pD3D9->pitch * m_state.pD3D9->height);
			if (res != cudaSuccess) success = false;
		}
		else success = false;
	}
	else success = false;

	return success;
}




void    VideoDecoder::D3D9_SetWindow(void* vpWnd, int width, int height)
{
	m_D3D9.hWnd = (HWND)vpWnd;
	m_D3D9.width = width;
	m_D3D9.height = height;
}

bool VideoDecoder::D3D9_Interop_Init(void* vpWnd, int width, int height)
{	

	bool success = true;

	m_D3D9.bDeviceLost = false;		
	m_D3D9.bWindowed = true;
	m_D3D9.width = width;
	m_D3D9.height = height;
	
	
	HWND hWnd = (HWND)vpWnd;

	// Initialize Direct3D
    if (SUCCEEDED(InitD3D9(hWnd, width, height)) &&
        //SUCCEEDED(InitCUDAInterop(&m_D3D9)) &&
        SUCCEEDED(InitTexture(&m_D3D9)))
    {
        if (!m_D3D9.bDeviceLost)
        {
            RegisterD3D9ResourceWithCUDA(&m_D3D9);
        }
    }
	else
	{
		success = false;
	}

	return success;
}

HRESULT VideoDecoder::InitD3D9(HWND hWnd, int windowWidth, int windowHeight)
{
    // Create the D3D object.
	if (S_OK != Direct3DCreate9Ex(D3D_SDK_VERSION, &m_D3D9.pD3D))
    {
        return E_FAIL;
    }

    D3DADAPTER_IDENTIFIER9 adapterId;
    int device;
    bool bDeviceFound = false;
    printf("\n");

    cudaError cuStatus;

    for (m_D3D9.iAdapter = 0; m_D3D9.iAdapter < m_D3D9.pD3D->GetAdapterCount(); m_D3D9.iAdapter++)
    {
        HRESULT hr = m_D3D9.pD3D->GetAdapterIdentifier(m_D3D9.iAdapter, 0, &adapterId);

        if (FAILED(hr))
        {
            continue;
        }

        cuStatus = cudaD3D9GetDevice(&device, adapterId.DeviceName);
        printf("> Display Device #%d: \"%s\" %s Direct3D9\n",
               m_D3D9.iAdapter, adapterId.Description,
               (cuStatus == cudaSuccess) ? "supports" : "does not support");

        if (cudaSuccess == cuStatus)
        {
            bDeviceFound = true;
            break;
        }
    }

    // we check to make sure we have found a cuda-compatible D3D device to work on
    if (!bDeviceFound)
    {
        printf("\n");
        printf("  No CUDA-compatible Direct3D9 device available\n");
        printf("PASSED\n");
        // destroy the D3D device
        m_D3D9.pD3D->Release();
        exit(EXIT_SUCCESS);
    }

	
    // Create the D3D Display Device
    RECT                  rc;
    GetClientRect(hWnd,&rc);
	D3DDISPLAYMODE        d3ddm;
	HRESULT hr = m_D3D9.pD3D->GetAdapterDisplayMode(D3DADAPTER_DEFAULT, &d3ddm);    
	ZeroMemory(&m_D3D9.d3dpp, sizeof(m_D3D9.d3dpp));

    m_D3D9.d3dpp.Windowed               = TRUE;
    m_D3D9.d3dpp.BackBufferCount        = 1;
    m_D3D9.d3dpp.SwapEffect             = D3DSWAPEFFECT_DISCARD;
    m_D3D9.d3dpp.hDeviceWindow          = hWnd;
    //d3dpp.BackBufferWidth      = g_bQAReadback?g_WindowWidth:(rc.right  - rc.left);
    //d3dpp.BackBufferHeight       = g_bQAReadback?g_WindowHeight:(rc.bottom - rc.top);
    m_D3D9.d3dpp.BackBufferWidth        = windowWidth;
    m_D3D9.d3dpp.BackBufferHeight       = windowHeight;

    m_D3D9.d3dpp.BackBufferFormat       = d3ddm.Format;

	
	if (FAILED(m_D3D9.pD3D->CreateDeviceEx(0, D3DDEVTYPE_HAL, hWnd,
                                    D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED,
									&m_D3D9.d3dpp, NULL, &m_D3D9.pD3DDevice)))
    {
        return E_FAIL;
    }

    // We clear the back buffer
    m_D3D9.pD3DDevice->BeginScene();
    m_D3D9.pD3DDevice->Clear(0, NULL, D3DCLEAR_TARGET, 0, 1.0f, 0);
    m_D3D9.pD3DDevice->EndScene();

    return S_OK;
}



HRESULT VideoDecoder::InitCUDAInterop(D3D9Params *pD3D9)
{
    printf("InitCUDA() g_pD3DDevice = %p\n", pD3D9->pD3DDevice);

    // Now we need to bind a CUDA context to the DX9 device,    
	// this sets the Direct3D 9 Device to use for interoperability with a CUDA device
    cudaError_t res = cudaD3D9SetDirect3DDevice(pD3D9->pD3DDevice);
    //getLastCudaError("cudaD3D9SetDirect3DDevice failed");

    return S_OK;
}


HRESULT VideoDecoder::InitTexture(D3D9Params *pD3D9)
{
    // 2D texture 
    
    if (FAILED(pD3D9->pD3DDevice->CreateTexture(pD3D9->width, pD3D9->height, 1, 0,
                                           D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &pD3D9->pTexture, NULL))) 
    {
        return E_FAIL;
    }	  

    return S_OK;
}


HRESULT VideoDecoder::ReleaseTextures(D3D9Params *pD3D9)
{
    // unregister the Cuda resources
    cudaGraphicsUnregisterResource(pD3D9->cudaResource);
    //getLastCudaError("cudaGraphicsUnregisterResource (m_texture) failed");
    cudaFree(pD3D9->cudaLinearMemory);
    //getLastCudaError("cudaFree (m_texture) failed");


    //
    // clean up Direct3D
    //
    {
        // release the resources we created
        pD3D9->pTexture->Release();
    }

    return S_OK;
}



HRESULT VideoDecoder::RegisterD3D9ResourceWithCUDA(D3D9Params *pD3D9)
{
    // 2D
    // register the Direct3D resources that we'll use
    // we'll read to and write from g_texture_2d, so don't set any special map flags for it
    cudaError_t res = cudaGraphicsD3D9RegisterResource(&pD3D9->cudaResource, pD3D9->pTexture, cudaGraphicsRegisterFlagsNone);	
    //getLastCudaError("cudaGraphicsD3D9RegisterResource (m_texture) failed");
    // cuda cannot write into the texture directly : the texture is seen as a cudaArray and can only be mapped as a texture
    // Create a buffer so that cuda can write into it
    // pixel fmt is DXGI_FORMAT_R8G8B8A8_SNORM
    cudaMallocPitch(&pD3D9->cudaLinearMemory, &pD3D9->pitch, pD3D9->width * 4, pD3D9->height);
    //getLastCudaError("cudaMallocPitch (m_texture) failed");
    cudaMemset(pD3D9->cudaLinearMemory, 1, pD3D9->pitch * pD3D9->height);

    return S_OK;
}




HRESULT VideoDecoder::DeviceLostHandler(D3D9Params * pD3D9)
{
	////////////////////////////////////////////////////////////////////////////////	
	//    - this function handles reseting and initialization of the D3D device
	//      in the event this Device gets Lost
	////////////////////////////////////////////////////////////////////////////////

    HRESULT hr = S_OK;

    fprintf(stderr, "-> Starting DeviceLostHandler() \n");

    // test the cooperative level to see if it's okay
    // to render
    if (FAILED(hr = pD3D9->pD3DDevice->TestCooperativeLevel()))
    {
        fprintf(stderr, "TestCooperativeLevel = %08x failed, will attempt to reset\n", hr);

        // if the device was truly lost, (i.e., a fullscreen device just lost focus), wait
        // until we g_et it back

        if (hr == D3DERR_DEVICELOST)
        {
            fprintf(stderr, "TestCooperativeLevel = %08x DeviceLost, will retry next call\n", hr);
            return S_OK;
        }

        // eventually, we will g_et this return value,
        // indicating that we can now reset the device
        if (hr == D3DERR_DEVICENOTRESET)
        {
            fprintf(stderr, "TestCooperativeLevel = %08x will try to RESET the device\n", hr);
            // if we are windowed, read the desktop mode and use the same format for
            // the back buffer; this effectively turns off color conversion

            if (pD3D9->bWindowed)
            {
                pD3D9->pD3D->GetAdapterDisplayModeEx(pD3D9->iAdapter, &pD3D9->d3ddm, NULL);
                pD3D9->d3dpp.BackBufferFormat = pD3D9->d3ddm.Format;
            }

            // now try to reset the device
            if (FAILED(hr = pD3D9->pD3DDevice->Reset(&pD3D9->d3dpp)))
            {
                fprintf(stderr, "TestCooperativeLevel = %08x RESET device FAILED\n", hr);
                return hr;
            }
            else
            {
                fprintf(stderr, "TestCooperativeLevel = %08x RESET device SUCCESS!\n", hr);

                // This is a common function we use to restore all hardware resources/state
                RestoreContextResources(pD3D9);

                fprintf(stderr, "TestCooperativeLevel = %08x INIT device SUCCESS!\n", hr);

                // we have acquired the device
                pD3D9->bDeviceLost = false;
            }
        }
    }

    return hr;
}



HRESULT VideoDecoder::RestoreContextResources(D3D9Params *pD3D9)
{
	////////////////////////////////////////////////////////////////////////////////
	//    - this function restores all of the CUDA/D3D resources and contexts
	////////////////////////////////////////////////////////////////////////////////
    // Reinitialize D3D9 resources, CUDA resources/contexts
    InitCUDAInterop(pD3D9);
    InitTexture(pD3D9);
    RegisterD3D9ResourceWithCUDA(pD3D9);

    return S_OK;
}




HRESULT VideoDecoder::DrawScene(D3D9Params *pD3D9)
{
    HRESULT hr = S_OK;
	int border = 2;

    if (pD3D9->bDeviceLost)
    {
        if (FAILED(hr = DeviceLostHandler(pD3D9)))
        {
            fprintf(stderr, "DeviceLostHandler FAILED returned %08x\n", hr);
            return hr;
        }
    }

    if (!pD3D9->bDeviceLost)
    {
        //
        // we will use this index and vertex data throughout
        //
        unsigned int IB[6] =
        {
            0,1,2,
            0,2,3,
        };
		
        struct VertexStruct
        {
            float position[3];
            float texture[3];
        };

        //
        // initialize the scene
        //
		D3DVIEWPORT9 viewport_window = {0, 0, pD3D9->width, pD3D9->height, 0, 1};
        pD3D9->pD3DDevice->SetViewport(&viewport_window);
        pD3D9->pD3DDevice->BeginScene();
        pD3D9->pD3DDevice->Clear(0, NULL, D3DCLEAR_TARGET, 0, 1.0f, 0);
        pD3D9->pD3DDevice->SetRenderState(D3DRS_CULLMODE, D3DCULL_NONE);
        pD3D9->pD3DDevice->SetRenderState(D3DRS_LIGHTING, FALSE);
        pD3D9->pD3DDevice->SetFVF(D3DFVF_XYZ|D3DFVF_TEX1|D3DFVF_TEXCOORDSIZE3(0));

        //
        // draw the 2d texture
        //
        VertexStruct VB[4] =
        {
            {  {-1,-1,0,}, {0,0,0,},  },
            {  { 1,-1,0,}, {1,0,0,},  },
            {  { 1, 1,0,}, {1,1,0,},  },
            {  {-1, 1,0,}, {0,1,0,},  },
        };
		D3DVIEWPORT9 viewport = {border, border, pD3D9->width-(2*border), pD3D9->height-(2*border), 0, 1};
        pD3D9->pD3DDevice->SetViewport(&viewport);
		pD3D9->pD3DDevice->SetTexture(0,pD3D9->pTexture);
        pD3D9->pD3DDevice->DrawIndexedPrimitiveUP(D3DPT_TRIANGLELIST, 0, 4, 2, IB, D3DFMT_INDEX32, VB, sizeof(VertexStruct));

        
        //
        // end the scene
        //
        pD3D9->pD3DDevice->EndScene();
        hr = pD3D9->pD3DDevice->Present(NULL, NULL, NULL, NULL);

        if (hr == D3DERR_DEVICELOST)
        {
            fprintf(stderr, "DrawScene Present = %08x detected D3D DeviceLost\n", hr);
            pD3D9->bDeviceLost = true;

            ReleaseTextures(pD3D9);            
        }
    }

    return hr;
}

void VideoDecoder::D3D9_CleanUp()
{
	ReleaseTextures(&m_D3D9);

	{
        // destroy the D3D device
        m_D3D9.pD3DDevice->Release();
        m_D3D9.pD3D->Release();		
    }
}