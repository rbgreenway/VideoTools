
#ifndef _VIDEO_ENCODER
#define _VIDEO_ENCODER

#ifndef _CRT_SECURE_NO_DEPRECATE
#define _CRT_SECURE_NO_DEPRECATE
#endif

#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <assert.h>

#include <windows.h>


#include "nvenc/nvEncodeAPI.h"
#include "nvenc/NvHWEncoder.h"
#include "FrameQueue.h"
#include "videodecoder.h"

#include "videosink.h"
#include "videosink_outputqueue.h"
#include "videosink_mp4.h"
#include "videosink_avi.h"

#include "diagnostics.h"
#include "systemstate.h"

//#include "turbojpeg.h"

#include <cuda.h>
#include <nvcuvid.h>

#include "jpeg/include/gpujpeg.h"


#define __cu(a) do { \
    CUresult  ret; \
    if ((ret = (a)) != CUDA_SUCCESS) { \
        fprintf(stderr, "%s has returned CUDA error %d\n", #a, ret); \
        return NV_ENC_ERR_GENERIC;\
    }} while(0)



#define MAX_ENCODE_QUEUE 32

#define SET_VER(configStruct, type) {configStruct.version = type##_VER;}




template<class T>
class CNvQueue {

    T** m_pBuffer;
    unsigned int m_uSize;
    unsigned int m_uPendingCount;
    unsigned int m_uAvailableIdx;
    unsigned int m_uPendingndex;
public:
    CNvQueue() : m_pBuffer(NULL), m_uSize(0), m_uPendingCount(0), m_uAvailableIdx(0),
        m_uPendingndex(0)
    {
    }

    ~CNvQueue()
    {
        delete[] m_pBuffer;
    }

    bool Initialize(T *pItems, unsigned int uSize)
    {
        m_uSize = uSize;
        m_uPendingCount = 0;
        m_uAvailableIdx = 0;
        m_uPendingndex = 0;
        m_pBuffer = new T *[m_uSize];
        for (unsigned int i = 0; i < m_uSize; i++)
        {
            m_pBuffer[i] = &pItems[i];
        }
        return true;
    }

    T * GetAvailable()
    {
        T *pItem = NULL;
        if (m_uPendingCount == m_uSize)
        {
            return NULL;
        }
        pItem = m_pBuffer[m_uAvailableIdx];
        m_uAvailableIdx = (m_uAvailableIdx + 1) % m_uSize;
        m_uPendingCount += 1;
        return pItem;
    }

    T* GetPending()
    {
        if (m_uPendingCount == 0)
        {
            return NULL;
        }

        T *pItem = m_pBuffer[m_uPendingndex];
        m_uPendingndex = (m_uPendingndex + 1) % m_uSize;
        m_uPendingCount -= 1;
        return pItem;
    }
};



typedef struct _EncodeFrameConfig
{
    CUdeviceptr  dptr;
    unsigned int pitch;
    unsigned int width;
    unsigned int height;
}EncodeFrameConfig;

#define ENCODER_OUTPUT_QUALITY_VERYHIGH  10240000
#define ENCODER_OUTPUT_QUALITY_HIGH      1024000
#define ENCODER_OUTPUT_QUALITY_MED       102400
#define ENCODER_OUTPUT_QUALITY_LOW       10240
#define ENCODER_OUTPUT_QUALITY_VERYLOW   1024


enum ENCODER_MODE {
    ENCODER_TO_H264 = 0,
    ENCODER_TO_HEVC,  // H265
    ENCODER_TO_JPEG
};


typedef struct _JPEG_ENCODER_SESSION
{
    struct gpujpeg_parameters param;
    struct gpujpeg_image_parameters param_image;
    struct gpujpeg_encoder* p_encoder;
    struct gpujpeg_encoder_input encoder_input;
    uint8_t*    image_compressed;
    int         image_compressed_size;
}JPEG_ENCODER_SESSION;


///////////////////////////////////////////////////////////////////////////////////
/// \brief The VideoEncoder class
///


class VideoEncoder
{
public:
    VideoEncoder(VideoDecoder * pVideoDecoder);
    virtual ~VideoEncoder();

    void Start();  // start encoder thread
    void Stop();   // stop encoder thread

    void SetDefaults();

    typedef void (VideoEncoder::*callback_func_void)();
    typedef void (VideoEncoder::*callback_func_voidPtr)(void*);

protected:
    CNvHWEncoder             *m_pNvHWEncoder;
    CUvideoctxlock            m_ctxLock;
    uint32_t                  m_uEncodeBufferCount;
    EncodeBuffer              m_stEncodeBuffer[MAX_ENCODE_QUEUE];
    CNvQueue<EncodeBuffer>    m_EncodeBufferQueue;
    EncodeConfig              m_stEncoderInput;
    EncodeOutputBuffer        m_stEOSOutputBfr;
    int32_t                   m_iEncodedFrames;
    int                       m_frameRate;

public:
    CNvHWEncoder*             GetHWEncoder() { return m_pNvHWEncoder; }
    NVENCSTATUS               Deinitialize();
    NVENCSTATUS               EncodeFrame(EncodeFrameConfig *pEncodeFrame, bool bFlush = false);
    NVENCSTATUS               AllocateIOBuffers(EncodeConfig* pEncodeConfig);
    int32_t                   GetEncodedFrames() { return m_iEncodedFrames; }
    ENCODED_FRAME_DATA*       GetPtrToLastEncodedFrameBuffer();

    std::string GetCudaErrorMessage(CUresult result);
    std::string GetEncoderErrorMessage(NVENCSTATUS code);


    void ConfigureEncoderFinishedCallback(std::function<void(int)> finishedCallback);

    void ConfigureEncoder(unsigned int bitRate, int frameRate, ENCODER_MODE mode, OUTPUT_MODE outMode,
                          int gopLength,
                          int invalidateRefFramesEnableFlag,
                          int intraRefreshEnableFlag,
                          int intraRefreshPeriod,
                          int intraRefreshDuration);

    void SetOutputFilename(std::string filename);

    void EncodeFrame(CUVIDPARSERDISPINFO pInfo);
    int  GetFrameRate();
    void GetWidthHeight(int * w, int * h);

    void GetWindowsHandles(HANDLE *pEncoderStoppedEvent);
    void GetEncoderOutputQueueSemaphore(HANDLE *pSinkOutputQueueSemaphore);
    bool GetNextFrameFromOutputQueue(char **pData, uint32_t *numBytes);
    void     ReleaseFrameFromOutputQueue();
    uint32_t GetFramesIn();
    uint32_t GetFramesOut();

    // JPEG Encoder functions
    bool Jpeg_InitEncoder(int width, int height);
    bool Jpeg_EncodeImage(uint8_t * p_imageData);
    void Jpeg_ShutdownEncoder();
    bool Jpeg_SaveToFile(std::string filename);
    void Jpeg_SetOutputQuality(uint32_t quality);
    uint32_t m_jpegQuality;

    // Turbo JPEG Encoder functions
//    bool TurboJpeg_EncodeImage(unsigned char* p_imageData);

        ////////////////////////////////////////
        /// CALLBACK FUNCTIONS

        // define callback function that gets called with all the encoding processing is complete.
            // this function returns the number of frames encoded.
            std::function<void(int)> EncodingFinished;

        ////////////////////////////////////////


    NVENCSTATUS               FlushEncoder();

protected:
    NVENCSTATUS               ReleaseIOBuffers();

    ENCODED_FRAME_DATA *      mp_LastFrame;

private:
    bool            m_encoderConfigured;
    EncodeConfig    m_encodeConfig;
    VideoDecoder  * mp_decoder;
    DecodeSession * mp_DecodeSession;
    int             m_inputWidth;
    int             m_inputHeight;
    unsigned int    m_outputBitrate;

    JPEG_ENCODER_SESSION  m_jpegEncoderSession;

    VideoSink * mp_videoSink;

    std::thread *   m_threadPtr;

    void StartEncoder();
    void StartJpegEncoder();

    ENCODER_MODE  m_encoderMode;

    HANDLE m_hExitEvent;
    HANDLE m_hEncoderStoppedEvent;
    HANDLE m_hOutputItems;
    HANDLE m_hFrameQueueSemaphore;
    HANDLE m_hDecoderStoppedEvent;

    std::atomic<bool> m_bEncoderRunning;
    std::atomic<bool> m_bDecoderRunning;
};

// NVEncodeAPI entry point
typedef NVENCSTATUS(NVENCAPI *MYPROC)(NV_ENCODE_API_FUNCTION_LIST*);

#endif

