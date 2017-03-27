#include "videosink_mp4.h"

VideoSink_MP4::VideoSink_MP4():VideoSink()
{
    mp_mp4Writer = nullptr;
}

VideoSink_MP4::~VideoSink_MP4()
{
    if(mp_mp4Writer!=nullptr)
        delete mp_mp4Writer;
}

void VideoSink_MP4::Prep()
{
    m_frameCount = 0;

    // prep MP4 file writer
        std::string str = m_outputfileName;
        char fname[100];
        strcpy(fname,str.c_str());

        mp_mp4Writer = new Mp4Writer(fname,m_frameRate);

        m_outputMode = OUTPUT_MP4_FILE;
}


void VideoSink_MP4::AddFrame(char *pData, uint32_t numBytes, uint32_t frameNumber, bool Ready, DWORD AviDwFlags)
{
    int8_t res = mp_mp4Writer->AddFrame(pData, numBytes);
    m_frameCount++;
}

void VideoSink_MP4::Complete()
{
//    int fps = mp_VideoEncoder->GetFrameRate();
    if(mp_mp4Writer != nullptr)
    {
        mp_mp4Writer->WriteFile(m_frameRate);  // writes the MP4 file
        delete mp_mp4Writer;
        mp_mp4Writer = nullptr;
    }
}
