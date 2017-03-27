#include "videosink.h"

VideoSink::VideoSink()
{

    m_outputfileName = "output";
    m_frameRate = 10; // default frame rate

}

VideoSink::~VideoSink()
{

}






void VideoSink::SetOutputFileName(std::string filename)
{
    std::string extension;

    switch(m_outputMode)
    {
    case OUTPUT_MP4_FILE:
        extension = ".mp4";
        break;
    case OUTPUT_AVI_FILE:
        extension = ".avi";
        break;
    default:
        break;
    }

    std::size_t found;
    // check to see if a "." is part of the filename.  If it is, erase it and everything after it.
    found = filename.find(".");
    if(found!=std::string::npos) // if true, this means that a "." was found at position = found
    {
        filename = filename.substr(0,found);
    }

    // append proper extension to end of filename
    filename += extension;

    m_outputfileName = filename;    
}


void VideoSink::SetFrameRate(int fRate)
{
    m_frameRate = fRate;
}



bool VideoSink::OutputQueue_GetNextFrame(char** pData, uint32_t* pNumBytes)
{
    // this function does the following:
    //  1 - checks to see if there are any decoded frames in the queue, if not, it returns false and nulls parameters.
    //
    //  2 - if there is a frame in the queue, it returns true and populates the parameters

    bool success = false;

    if(m_outputQueue.itemAvailable())
    {
        ENCODED_FRAME_DATA* pFrame = m_outputQueue.peekHead();

        *pData = pFrame->pData;
        *pNumBytes = pFrame->NumBytes;

        success = true;
    }
    else
    {
        *pData = nullptr;
        *pNumBytes = 0;
    }

    return success;
}


void VideoSink::OutputQueue_ReleaseFrame()
{
    m_outputQueue.pop();
    SystemState::SS_increment_outputQueue_countPop();
}



uint32_t VideoSink::OutputQueue_GetPushCount()
{
    return SystemState::SS_get_outputQueue_countPush();
}

uint32_t VideoSink::OutputQueue_GetPopCount()
{
    return SystemState::SS_get_outputQueue_countPop();
}

void VideoSink::GetOutputItemsSemaphore(HANDLE *pOutputItemsSemaphore)
{
    *pOutputItemsSemaphore = m_outputQueue.GetSemaphore_ItemsInQueue();
}

void VideoSink::GetOutputSpacesSemaphore(HANDLE *pOutputSpacesSemaphore)
{
    *pOutputSpacesSemaphore = m_outputQueue.GetSemaphore_SpacesInQueue();
}

HANDLE VideoSink::GetOutputItemsSemaphore()
{
    return m_outputQueue.GetSemaphore_ItemsInQueue();
}

HANDLE VideoSink::GetOutputSpacesSemaphore()
{
    return m_outputQueue.GetSemaphore_SpacesInQueue();
}


void VideoSink::Prep()
{

}

void VideoSink::AddFrame(char *pData, uint32_t numBytes, uint32_t frameNumber, bool Ready, DWORD AviDwFlags)
{

}

void VideoSink::Complete()
{

}

void VideoSink::SetFrameSize(int width, int height)
{
    m_width = width;
    m_height = height;
}

