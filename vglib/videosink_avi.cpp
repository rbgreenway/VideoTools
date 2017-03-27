#include "videosink_avi.h"

VideoSink_AVI::VideoSink_AVI():VideoSink()
{
    mp_aviWriter = nullptr;
}

VideoSink_AVI::~VideoSink_AVI()
{
    if(mp_aviWriter!=nullptr)
        delete mp_aviWriter;
}

void VideoSink_AVI::Prep()
{
//    int width;
//    int height;
//    mp_VideoEncoder->GetWidthHeight(&width,&height);

    std::string fn(m_outputfileName);
    char fname[100];
    strcpy(fname,fn.c_str());

    mp_aviWriter = new AviWriter(fname,m_width,m_height, m_frameRate);

    m_outputMode = OUTPUT_AVI_FILE;
}


void VideoSink_AVI::AddFrame(char *pData, uint32_t numBytes, uint32_t frameNumber, bool Ready, DWORD AviDwFlags)
{
    mp_aviWriter->AddFrame(pData,numBytes,AviDwFlags,frameNumber);
}

void VideoSink_AVI::Complete()
{
    if(mp_aviWriter != nullptr)
    {
        delete mp_aviWriter;
        mp_aviWriter = nullptr;
    }
}
