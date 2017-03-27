#ifndef VIDEOSINK_H
#define VIDEOSINK_H

#include <atomic>
#include <queue>
#include <memory>

#include "mp4writer.h"
#include "aviwriter.h"

#include "systemstate.h"
#include "CircularFifoOutput.h"

#include "nvenc/NvHWEncoder.h"



enum OUTPUT_MODE {
    OUTPUT_MP4_FILE = 0,
    OUTPUT_AVI_FILE,
    OUTPUT_QUEUE
};


class VideoSink
{
public:
    VideoSink();
    ~VideoSink();

    AviWriter * mp_aviWriter;
    Mp4Writer * mp_mp4Writer;

    void SetOutputFileName(std::string filename);
    void SetFrameRate(int fRate);
    void SetFrameSize(int width, int height);

    bool OutputQueue_GetNextFrame(char **pData, uint32_t *pNumBytes);
    void OutputQueue_ReleaseFrame();

    uint32_t OutputQueue_GetPushCount();
    uint32_t OutputQueue_GetPopCount();

    void GetOutputItemsSemaphore(HANDLE *pOutputItemsSemaphore);
    void GetOutputSpacesSemaphore(HANDLE *pOutputSpacesSemaphore);

    HANDLE GetOutputItemsSemaphore();
    HANDLE GetOutputSpacesSemaphore();

protected:

    std::string m_outputfileName;
    int         m_frameRate;
    int         m_frameCount;
    int         m_width;
    int         m_height;

    OUTPUT_MODE m_outputMode;


    CircularFifoOutput m_outputQueue;  // output queue of encoded frames that have been post-processed
                                       // for consumption by a down-stream process.  The image data
                                       // is in CPU (Host) memory

public:
    virtual void Prep();    
    virtual void AddFrame(char* pData,uint32_t numBytes,uint32_t frameNumber,bool Ready, DWORD AviDwFlags);
    virtual void Complete();


};

#endif // VIDEOSINK_H
