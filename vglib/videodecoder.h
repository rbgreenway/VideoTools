#ifndef VIDEODECODER_H
#define VIDEODECODER_H


#include <string>
#include <algorithm>
#include <stdint.h>
#include <vector>
#include <queue>
#include <memory>
#include <thread>
#include <mutex>

#include <nvcuvid.h>
#include <cuda.h>

// CUDA utilities and system includes
#include "helper_functions.h"
#include "helper_cuda.h"
#include "helper_cuda_drvapi.h"    // helper file for CUDA Driver API calls and error checking
#include "gpumatfifo.h"

#include <d3dx9.h>
// This header inclues all the necessary D3D10 and CUDA includes
#include <cuda_runtime_api.h>
#include <cuda_d3d9_interop.h>

#include "vicuda.h"

// OpenCV headers
#include "opencv2/core.hpp"
#include "opencv2/core/utility.hpp"
#include "opencv2/cudabgsegm.hpp"
#include "opencv2/cudalegacy.hpp"
#include "opencv2/video.hpp"
#include "opencv2/videostab.hpp"

#include "diagnostics.h"
#include "systemstate.h"
#include "CircularFifoCpu.h"
#include "CircularFifoGpu.h"
#include "CircularFifoInput.h"

using namespace cv;
using namespace cv::cuda;
using namespace cv::cudev;
using namespace cv::videostab;

#include "framequeue.h"



#define MAX_FRM_CNT             4
#define DISPLAY_DELAY           1  // FIXME, = 4 will trigger repeat pattern


////////////////////////////////////////////////////////////////////////////////////////
// This enum defines the various operational modes of the VideoDecoder class
enum DECODER_MODE {
    DECODER_TO_CPU_ARGB,    // ARGB decoded frames are pushed onto CPU decoded frame queue
    DECODER_TO_CPU_RGB,     // RGB decoded frames are pushed onto CPU decoded frame queue
    DECODER_TO_GPU_ARGB,    // ARGB decoded frames are pushed onto GPU decoded frame queue
    DECODER_TO_GPU_RGB,     // RGB decoded frames are pushed onto GPU decoded frame queue
    DECODER_TO_GPU_GPUMAT,  // GpuMat objects are pushed onto GpuMatFIFO
    DECODER_TO_ENCODER,     // this mode for fast h264 transcoding (Decode -> NV12 -> Encode), uses FrameQueue
    DECODER_TO_CPU_NV12,    // NV12 decoded frames are pushed onto CPU decoded frame queue
	DECODER_TO_DIRECTX		// decode to ARGB on GPU, then Interop to DirectX Surface for display
};



////////////////////////////////////////////////////////////////////////////////////////
// Direct X Texture

struct D3D9Params
{
	D3D9Params(){ pD3DDevice = 0; pTexture = 0; pSurface = 0; cudaResource = 0; cudaLinearMemory = 0; hWnd = 0; }

	IDirect3DTexture9	   *pTexture;
	IDirect3DSurface9	   *pSurface;
	cudaGraphicsResource   *cudaResource;
	void				   *cudaLinearMemory;
	size_t					pitch;
	int						width;
	int						height;

	IDirect3D9Ex		   *pD3D;			// Used to create the D3DDevice
	unsigned int			iAdapter;		// index of Direct X adapter
	IDirect3DDevice9Ex	   *pD3DDevice;		// Direct X device
	D3DDISPLAYMODEEX		d3ddm;
	D3DPRESENT_PARAMETERS	d3dpp;
	bool					bDeviceLost;    // indicates whether a DirectX device has been lost
	bool					bWindowed;
	HWND					hWnd;           // handle to window for DirectX display
};

//struct D3D9Params
//{
//	D3D9Params(){ pSurface = 0; cudaResource = 0; cudaLinearMemory = 0; }
//	IDirect3DSurface9      *pSurface;
//	cudaGraphicsResource   *cudaResource;
//	void				   *cudaLinearMemory;
//	size_t					pitch;
//	int						width;
//	int						height;
//};




////////////////////////////////////////////////////////////////////////////////////////
// This struct is used to hold the state of a VideoDecoder instance.  It is required because
// it is passed into various callback functions from the Cuda video decoder.  There is a single
// instance of this for an instance of VideoDecoder.

class VideoDecoder;  // forward declaration

typedef struct
{
public:

    CUvideoparser   cuParser;
    CUvideodecoder  cuDecoder;
    CUdeviceptr     argbFrame;
    CUstream        cuStream;
    CUcontext       cuContext;
    CUvideoctxlock  cuCtxLock;
    uint32_t        count;
    CUVIDDECODECREATEINFO dci;

    DECODED_IMAGE_FORMAT decodedFormat;

    CircularFifoCpu * pCpuDecodedFrameQueue; // output queue of decoded frames on the CPU that have been
                                             // post-processed for consumption by down-stream process.  The
                                             // image data is on the CPU.

    GpuMatFIFO *pGpuMatQueue; // output queue of decoded frames that have been copied to
                              // OpenCV GpuMat structures.  This allows for continued processing
                              // of images using the OpenCV GPU functions.


    CircularFifoGpu * pGpuDecodedFrameQueue; // output queue of decoded frames that have been
                                             // post-processed for consumption by a down-stream
                                             // process, such as JPEG encoding.  The image data is on
                                             // the GPU.

    FrameQueue *    pDecodedFrameQueue; // output queue raw NV12 decoded frames from NVidia decoder.  This queue feeds all of the other
                                        // queues as it holds the raw output from the NVidia decoder.

    DECODER_MODE    decoderMode;

//    HANDLE          inputQueueSemaphore;  // semaphore count indicates number of frames in queue
//    HANDLE          inputQueueSpacesSemaphore; // semaphore count indicates number of spaces available in queue
    HANDLE            hOutputSpaces; // semaphore count indicates number of frames in queue
    HANDLE            hExitEvent;  // event to abort everything

    int                skipCount; // if m_skipCount = N, then every Nth frame output from the Decoder is put on the output queue.
                                    // the default is m_skipCount = 1 which puts every frame output from the Decoder on the output queue.

    int                skipCounter;

	// Direct X stuff
	D3D9Params       *pD3D9; // pointer to data needed for Direct X display of decoded image

	VideoDecoder     *pVD;
	
} DecodeSession;


////////////////////////////////////////////////////////////////////////////////////////
// Helper class for creating an Auto lock for floating contexts

#define USE_FLOATING_CONTEXTS 1

class CAutoCtxLock
{
private:
    CUvideoctxlock m_lock;
public:
#if USE_FLOATING_CONTEXTS
    CAutoCtxLock(CUvideoctxlock lck) {
        m_lock=lck; cuvidCtxLock(m_lock, 0);
                                     }
    ~CAutoCtxLock() {
        cuvidCtxUnlock(m_lock, 0);
                    }
#else
    CAutoCtxLock(CUvideoctxlock lck) { m_lock=lck; }
#endif
};








///////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////
/// Setting up and initializing Cuda
//
//  The VideoDecoder and VideoEncoder classes both utilize a Cuda-enabled GPU (NVidia).  Before
//  instantiating either of these classes, Cuda must be initialized to make sure that:
//      1 - Cuda has been installed and is operating,
//      2 - an appropriate NVidia gpu card is installed, with the proper "compute capability"
//          (currently we suggest compute capability of 5.0 or higher),
//      3 - we acquire a Cuda "context", which is analogous to a PC process.  All our Cuda code
//          will run inside this context.  We actually get a context and a context lock and store
//          them in variables for later use (when setting up a decoder and possibly an encoder).
//
//  The CudaUtil class simplifies this initialization process.  An example of proper initialization is
//  given below.
//
//
//      mp_cudaUtil = new CudaUtil();  // REQUIRED
//
//      if(mp_cudaUtil->IsCudaReady())
//      {
//          int deviceCount = 0;
//          if(mp_cudaUtil->GetCudaDeviceCount(deviceCount)) // OPTIONAL
//          {
//              cout << "Number of Cuda Devices = " << deviceCount << endl;
//              std::string deviceName;
//              if(mp_cudaUtil->GetDeviceName(deviceName)) // OPTIONAL
//              {
//                  cout << "Device Name: " << deviceName << endl;
//                  int major, minor;
//                  if(mp_cudaUtil->GetComputeCapability(major,minor)) // OPTIONAL
//                  {
//                      cout << "Compute Capability: " << major << "." << minor << endl;
//                      size_t totalMem, freeMem;
//                      if(mp_cudaUtil->GetDeviceMemory(totalMem,freeMem))  // OPTIONAL
//                      {
//                          cout << "Total Memory = " << totalMem << "   Free Memory = " << freeMem << endl;
//
//                          mp_cudaUtil->GetContext(m_cudaContext,m_cudaCtxLock); // REQUIRED
//                      }
//                  }
//              }
//          }
//      }
//      else
//      {
//          ui->MessageDisplay->append("Cuda failed to initialize");
//      }



////////////////////////////////////////////////////////////////////////////////////////
/// Decoding/Encoding Pipeline elements
//
//  There are 3 primary classes available for creating decoding and transcoding sessions, but only 2 are exposed to developers:
//
//      VideoDecoder    - decodes frames that have been pushed onto it's "encoded frame queue".  The decoded
//                        frames can be passed directly to a VideoEncoder (transcoding) or post-processed
//                        in a variety of ways (i.e. converted to ARGB for display or copied to an OpenCV
//                        GpuMat for further analysis by OpenCV).  The VideoDecoder is also responsible for
//                        resizing the image if desired.  Currently, the VideoDecoder can only handle H264
//                        encoded frames as input.
//      VideoEncoder    - encodes decoded frames provided by the VideoDecoder.  It reads these frames from a
//                        frame queue whose data resides on the gpu.  Encoded frames are off loaded from the GPU
//                        and passed to the VideoSink for further processing.  Currently, the VideoEncoder can only
//                        create H264 or JPEG encoded frames as output.
//      VideoSink       - handles the stream of encoded frames output by the VideoEncoder.  The processing of these
//                        frames is configurable (i.e. to MP4 file, AVI file, or Output Queue)
//
//  1 - Build VideoDecoder (uses C++ 11 functionality)
//  This simply requires you to create an instance of the VideoDecoder class, initialize it, and optionally
//  configure a callback function that gets called when a decoding session completes. Note that a Cuda context,
//  creating during Cuda initialization, is passed to the VideoDecoder constructor.
//
//  Example:
//        mp_videoDecoder = new VideoDecoder(m_cudaContext);
//
//        bool success = mp_videoDecoder->Init();
//
//        if(success)
//        {
//            cout << "Video Decoder successfully Initialized" << endl;
//
//            // set up callback when Decoder completes a decoding session.
//            // NOTE: the signature of the callback function must return void and have an int parameter.  A count
//            //       of the number of frames decoded will be passed in the int parameter.
//            // NOTE: these callback functions should not perform GUI operations since they will not
//                     run on the GUI thread.
//            // NOTE: the function set up for callback below is: void DecoderFinished(int count)
//
//            using namespace std::placeholders; // for `_1`
//            std::function< void(int) > cb1 = std::bind(&Dialog::DecoderFinished,this,_1);
//            mp_videoDecoder->ConfigureDecoderFinishedCallback(cb1);
//        }
//        else
//        {
//            mp_videoDecoder = nullptr;
//            cout << "Video Decoder Failed to Initialize" << endl;
//            return;
//        }
//
//
//
//  2 - Building a VideoEncoder (uses C++ 11 functionality)
//  This requires the program to create an instance of the VideoEncoder class and optionally configure a
//  callback function that gets called when an encoding session is complete.
//
//  NOTE: a pointer to the associated VideoDecoder, created in step 1 above, is passed to the VideoEncoder constructor.
//
//  Example:
//        mp_videoEncoder = new VideoEncoder(mp_videoDecoder);
//
//        // set up callback when Encoder completes a encoding session
//        // NOTE: these callback functions should not perform GUI operations since they will not run on the GUI thread
//        // NOTE: the signature of the callback is: return void, parameter int.
//        //       i.e.  void EncoderFinished(int count)
//        //             where the count parameter gives the number of frames encoded
//
//        using namespace std::placeholders; // for `_1`
//        std::function< void(int) > cb1 = std::bind(&Dialog::EncoderFinished,this,_1);
//        mp_videoEncoder->ConfigureEncoderFinishedCallback(cb1);
//
//
//  3 - Building a VideoSink (depends on C++ 11)
//  Construction of the VideoSink is performed automatically by the VideoEncoder once the VideoEncoder is configured (by
//  calling VideoEncoder::ConfigureEncoder(...)
//


////////////////////////////////////////////////////////////////////////////////////////
/// Building a Decoding Pipeline
//
//  A decoding pipeline requires an instance of both a VideoDecoder and VideoSource.
// Steps:
//  1 - Initialize Cuda
//  2 - Build a VideoDecoder (needs the Cuda context from step 1)
//  3 - Configure the VideoDecoder
//  4 - Start the VideoDecoder - starts a thread that monitors its encoded-frame input queue
//          A. call mp_videoDecoder->Init();
//          B. call mp_videoDecocer->Start();


////////////////////////////////////////////////////////////////////////////////////////
/// Building a Transcoding Pipeline
//
//  Creating a Video Transcoding pipeline requires and instance of VideoSource, VideoDecoder, VideoEncoder, and VideoSink.
//
//      1 - Initialize Cuda
//      2 - Create the VideoDecoder object as described in the section "Building a Decoding Pipeline"
//      3 - Create a VideoEncoder (needs a pointer to the VideoDecoder instance created above), mp_videoEncoder.
//      4 - Configure the VideoEncoder
//          The VideoEncoder can be configured to perform either H264 or JPEG encoding
//          A. call mp_videoEncoder->ConfigureEncoder(unsigned int bitRate, int frameRate, ENCODER_MODE mode,...)
//              where
//                  bitRate - only matters for JPEG encoding and can be selected from (or provide your own number)
//                #define ENCODER_OUTPUT_QUALITY_VERYHIGH  10240000
//                #define ENCODER_OUTPUT_QUALITY_HIGH      1024000
//                #define ENCODER_OUTPUT_QUALITY_MED       102400
//                #define ENCODER_OUTPUT_QUALITY_LOW       10240
//                #define ENCODER_OUTPUT_QUALITY_VERYLOW   1024
//
//                  frameRate - only matters if the VideoSource is from a recorded video segment, and we are saving to
//                              a file (such as MP4 or AVI).  The pre-recorded frame rate can be obtained from
//                              using: frameRate = vf->at(0)->frameRate, where vf is returned from the call to
//                              ConfigVideoSegment(...).
//
//                  mode - selected from:
//                                enum ENCODER_MODE {
//                                    ENCODER_TO_H264,
//                                    ENCODER_TO_JPEG
//                                };
//      5 - Start the VideoEncoder (the VideoSink does not need to be started, but must be configured before starting
//          the VideoEncoder).
//          call mp_videoEncoder->Start()
//



////////////////////////////////////////////////////////////////////////////////////////
/// Clean up
//
//      Clean up by destroying in order the VideoEncoder, VideoDecoder, and the CudaUtil instances.
//




////////////////////////////////////////////////////////////////////////////////////////
/// VideoDecoder class
//  This class is used to provide access to the Cuda video decoder functionality.  When creating a transcoding pipeline,
//  consisting of a VideoSource, VideoDecoder, VideoEncoder, and VideoSink, the VideoDecoder is typically the first
//  of these classes to be created.

class VideoDecoder
{
public:
    VideoDecoder(CUcontext ctx, uint32_t codec);
    ~VideoDecoder();

    bool Init();

    void Start();
    void Stop();

    std::string GetLastErrorMessage();

    bool EnqueueFrame(unsigned char* pData, uint32_t numBytes, uint64_t timestamp, bool flushDecoder);
    void EndDecoding();
    bool CreateVideoDecoder(uint32_t imageWidth, uint32_t imageHeight,
                            cudaVideoCodec codec, CUvideoctxlock ctxLock);
    bool CreateVideoParser(cudaVideoCodec codec, unsigned int maxDecodedSurfaces);    

    void ConfigureDecoder(int outputWidth, int outputHeight, DECODER_MODE mode, cudaVideoCodec codec);

    void HandleVideoData(unsigned char * packet, uint32_t numBytes, uint64_t timestamp);

    static int CUDAAPI HandleVideoSequence(void *pUserData, CUVIDEOFORMAT *pFormat);
    static int CUDAAPI HandlePictureDecode(void *pUserData, CUVIDPICPARAMS *pPicParams);
    static int CUDAAPI HandlePictureDisplay(void *pUserData, CUVIDPARSERDISPINFO *pPicParams);

    static int PostProcess_ToGpuMatQueue(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);
    static int PostProcess_ToCpuRGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);
    static int PostProcess_ToCpuARGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);
    static int PostProcess_ToGpuRGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);
    static int PostProcess_ToGpuARGB(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);
    static int PostProcess_ToCpuNV12(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);
	static int PostProcess_ToDirectX(DecodeSession *pstate, CUVIDPARSERDISPINFO *pPicParams);

    uint32_t  m_decodedFrames;

    CircularFifoCpu *GetCpuDecodedFrameQueue();
    GpuMatFIFO *GetGpuMatFifo();
    CircularFifoGpu *GetGpuDecodedFrameQueue();

    bool FrameReadyCpu();
    bool FrameReadyGpu();

    void FlushDecoder();

    DecodeSession* GetDecodeSession();

    void VideoSourceFinished();


    bool GetNextDecodedFrameCpu(char **ppData, uint32_t *pNumBytes, uint32_t *pWidth, uint32_t *pHeight, DECODED_IMAGE_FORMAT *pFormat, uint64_t *pTimestamp);
    void ReleaseFrame();

    bool GetNextDecodedFrameGpu(CUdeviceptr *ppData, uint32_t *pNumBytes, uint32_t *pWidth, uint32_t *pHeight, DECODED_IMAGE_FORMAT *pFormat, uint64_t *pTimestamp);
    void ReleaseFrameGpu();
	bool CopyGpuFrameToCpuFrame(char *pDestCpu, CUdeviceptr pSourceGpu, uint32_t numBytes);


    GPUMAT_FIFO_OBJECT *PeekNextGpuMat();  // get a pointer to next GpuMat in the GpuMat queue
    bool     PopNextGpuMat();   // free this position in the GpuMat queue

    void GetDecodedImageSize(uint32_t *pWidth, uint32_t *pHeight);

    void GetWindowsHandles(HANDLE *pInputQueueSemaphore, HANDLE *pOutputQueueSemaphore, HANDLE *pDecoderStoppedEvent);

    HANDLE GetInputQueueSpaceAvailSemaphore();

    HANDLE GetFrameQueueSemaphore();
    HANDLE GetDecoderStoppedEvent();
    bool   GetDecoderRunning();

    uint32_t InputQueue_GetPushCount();
    uint32_t InputQueue_GetPopCount();

    std::string GetCudaErrorMessage(CUresult result);
	
    void SetSkipCount(int count);

	CUstream GetCudaStream();

	/// D3D9 functions
	bool Init_D3DSurface(IDirect3DSurface9 *pSurface, int width, int height);

	void    D3D9_SetWindow(void* vpWnd, int width, int height);
	bool    D3D9_Interop_Init(void* vpWnd, int width, int height);
	HRESULT InitD3D9(HWND hWnd, int windowWidth, int windowHeight);
	static HRESULT InitCUDAInterop(D3D9Params *pD3D9);
	static HRESULT InitTexture(D3D9Params *pD3D9);
	static HRESULT ReleaseTextures(D3D9Params *pD3D9);
	static HRESULT RegisterD3D9ResourceWithCUDA(D3D9Params *pD3D9);
	static HRESULT DeviceLostHandler(D3D9Params * pD3D9);
	static HRESULT RestoreContextResources(D3D9Params *pD3D9);
	static HRESULT DrawScene(D3D9Params *pD3D9);
	void D3D9_CleanUp();


    ////////////////////////////////////////
    /// Windows Handles
    ///

        // this event is used to signal that the decoder loop should exit (i.e. stop decoder)
        HANDLE m_hExitEvent;

        // this event is used to signal when the decoder is not running
        HANDLE m_hDecoderStoppedEvent;

        HANDLE m_hInputItems; // semaphore signaling the number of items in input queue
        HANDLE m_hInputSpaces; // semaphore signaling the number of empty spaces in the input queue

        HANDLE m_hOutputItems; // semaphore signaling the number of items in output queue
        HANDLE m_hOutputSpaces; // semaphore signaling the number of empty spaces in the output queue

    ////////////////////////////////////////


private:
    void StartDecoder();    

    DecodeSession           m_state;
    std::atomic<bool>   m_bDecoderRunning;

    std::thread *      m_threadPtr;

    CUresult           m_cudaResult;
    cudaVideoCodec     m_codec;

    std::string        m_errMsg;

    CircularFifoInput  m_inputQueue;        // input queue (Encodeded Frames)

    CircularFifoCpu    m_outputQueueCpu;    // output queue of decoded frames that have been post-processed
                                            // for consumption by a down-stream process.  The image data
                                            // is in CPU (Host) memory

    CircularFifoGpu    m_outputQueueGpu;    // output queue of decoded frames that have been
                                            // post-processed for consumption by a down-stream
                                            // process, such as JPEG encoding.  The image data is in
                                            // GPU (Device) memory.

    GpuMatFIFO         m_outputQueueGpuMat; // output queue of decoded frames that have been copied to
                                            // OpenCV GpuMat structures.  This allows for continued processing
                                            // of images using the OpenCV GPU functions.

	D3D9Params		      m_D3D9;		// custom struct that holds data necessary to interop an image with DirectX
	
};

#endif // VIDEODECODER_H
