#ifndef VIDEOSINK_OUTPUTQUEUE_H
#define VIDEOSINK_OUTPUTQUEUE_H

#include "videosink.h"

class VideoSink_OutputQueue : public VideoSink
{
public:
    VideoSink_OutputQueue();
    ~VideoSink_OutputQueue();

    void Prep();
    void AddFrame(char* pData, uint32_t numBytes, uint32_t frameNumber, bool Ready, DWORD AviDwFlags);
    void Complete();
};

#endif // VIDEOSINK_OUTPUTQUEUE_H
