#ifndef CIRCULARFIFOGPU_H
#define CIRCULARFIFOGPU_H

#include <Windows.h>

#include <cuda.h>
#include "CircularFifoCpu.h"


#define CIRCULARFIFO_GPU_SIZE 2

////////////////////////////////////////////////////////////////////////////////////////
/// This struct is a container for data that describes a decoded image that is in gpu memory.  Instances of
/// this type will only exist in CPU memory (not on the GPU).  A "decoded queue" of these structs
/// is part of the DecodeSession struct.



struct DecodedFrameOnGpu{
public:
    DecodedFrameOnGpu(){pData = NULL; numBytes=0; width=0; height=0;format=FORMAT_NONE;timestamp=0;bufferSize=0;}
    DecodedFrameOnGpu(CUdeviceptr pdata, uint32_t bytes,uint16_t w, uint16_t h, DECODED_IMAGE_FORMAT _format)
    {pData = pdata; numBytes = bytes; width = w; height = h; format = _format;}
    ~DecodedFrameOnGpu(){if(pData != NULL) cuMemFree(pData);}
    CUdeviceptr  pData;
    uint32_t   numBytes;
    uint16_t   width;
    uint16_t   height;
    DECODED_IMAGE_FORMAT format;
    uint64_t timestamp;
    uint32_t bufferSize;
};





class CircularFifoGpu{
public:

  // constructor
  CircularFifoGpu();

  // destructor
  ~CircularFifoGpu();  // this should clean up - delete all elements in the array

  bool push();
  bool pop();

  DecodedFrameOnGpu* peekHead();
  DecodedFrameOnGpu* peekTail();

  HANDLE GetSemaphore_ItemsInQueue();
  HANDLE GetSemaphore_SpacesInQueue();
  int GetNumItemsInQueue();

  bool isEmpty() const;
  bool isFull() const;
  bool spaceAvailable() const;
  bool itemAvailable() const;
  bool isLockFree() const;

private:
  enum { Capacity = CIRCULARFIFO_GPU_SIZE + 1};
  size_t increment(size_t idx) const;
  std::atomic<size_t>   _tail;  // tail(input) index
  DecodedFrameOnGpu*    _array[Capacity];
  std::atomic<size_t>   _head; // head(output) index

  HANDLE m_hItems; // semaphore signaling the number of items in the FIFO ready to be taken out (monitored by Consumer thread)
  HANDLE m_hSpaces; // semaphore signaling the number of empty spaces in the FIFO (monitored by Producer thread)
};


#endif // CIRCULARFIFOGPU_H
