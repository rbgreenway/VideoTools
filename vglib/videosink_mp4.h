#ifndef VIDEOSINK_MP4_H
#define VIDEOSINK_MP4_H

#include "videosink.h"

class VideoSink_MP4 : public VideoSink
{
public:
    VideoSink_MP4();
    ~VideoSink_MP4();

    void Prep();    
    void AddFrame(char* pData,uint32_t numBytes,uint32_t frameNumber,bool Ready, DWORD AviDwFlags);
    void Complete();
};

#endif // VIDEOSINK_MP4_H
