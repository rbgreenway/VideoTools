#ifndef VIDEOSINK_AVI_H
#define VIDEOSINK_AVI_H

#include "videosink.h"

class VideoSink_AVI : public VideoSink
{
public:
    VideoSink_AVI();
    ~VideoSink_AVI();

    void Prep();
    void AddFrame(char* pData, uint32_t numBytes, uint32_t frameNumber, bool Ready, DWORD AviDwFlags);

    void Complete();
};

#endif // VIDEOSINK_AVI_H
