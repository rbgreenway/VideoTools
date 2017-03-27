#include "videosink_outputqueue.h"

VideoSink_OutputQueue::VideoSink_OutputQueue():VideoSink()
{

}

VideoSink_OutputQueue::~VideoSink_OutputQueue()
{

}


void VideoSink_OutputQueue::Prep()
{ 
    m_outputMode = OUTPUT_QUEUE;
}


void VideoSink_OutputQueue::AddFrame(char *pData, uint32_t numBytes, uint32_t frameNumber, bool Ready, DWORD AviDwFlags)
{
    switch(WaitForSingleObject(m_outputQueue.GetSemaphore_SpacesInQueue(),5000))
    {
        case WAIT_OBJECT_0:
            if(m_outputQueue.spaceAvailable())
            {
                ENCODED_FRAME_DATA* pFrame = m_outputQueue.peekTail();

                if(pFrame->bufferSize < numBytes)
                {
                    if(pFrame->pData!=nullptr) delete pFrame->pData;
                    pFrame->pData = (char*)malloc(numBytes);
                    pFrame->bufferSize = numBytes;
                }

                memcpy(pFrame->pData,pData,numBytes);
                pFrame->frameNumber = frameNumber;
                pFrame->NumBytes = numBytes;
                pFrame->Ready = Ready;
                pFrame->AviDwFlags = AviDwFlags;

                m_outputQueue.push();

            }
            break;

        case WAIT_TIMEOUT:
            // signal failure to add frame to output queue (queue is full)
            Diagnostics::DebugMessage("Failed to AddImage in Output Queue.");
            Diagnostics::DebugMessage("Timeout occurred waiting for room in Output Queue");
            break;
    }

}

void VideoSink_OutputQueue::Complete()
{

}


