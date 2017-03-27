#ifndef CIRCULARFIFOCPU_H
#define CIRCULARFIFOCPU_H

#include <Windows.h>

#include <atomic>
#include <cstddef>


#define CIRCULARFIFO_CPU_SIZE 8


enum DECODED_IMAGE_FORMAT {
    FORMAT_ARGB,
    FORMAT_RGB,
    FORMAT_NV12,
    FORMAT_GPUMAT,
    FORMAT_DECODING_FINISHED,
    FORMAT_NONE
};

////////////////////////////////////////////////////////////////////////////////////////
/// This struct is a container for a image frame that is ready to be displayed.  Instances of
/// this type will only exist in CPU memory (not on the GPU).  A "display queue" of these structs
/// is part of the DecodeSession struct.

struct DecodedFrameOnCpu{
public:
    DecodedFrameOnCpu(){pData = nullptr;numBytes = 0; width = 0; height = 0; format = FORMAT_NONE; timestamp = 0; bufferSize = 0;}
    DecodedFrameOnCpu(char *pdata, uint32_t bytes,uint16_t w, uint16_t h, DECODED_IMAGE_FORMAT _format, uint64_t _bufferSize)
    {pData = pdata; numBytes = bytes; width = w; height = h; format = _format; bufferSize = _bufferSize;}
    ~DecodedFrameOnCpu(){ if(pData != nullptr) delete pData; }
    char *  pData;
    uint32_t   numBytes;
    uint16_t   width;
    uint16_t   height;
    DECODED_IMAGE_FORMAT format;
    uint64_t   timestamp;
    uint64_t   bufferSize;
};



// Tail and Head are indexes of an array of pointers objects
// Tail = points to object ready for new data
// Head = points to object that has data and is ready for use
// If Tail == Head, the circular buffer is empty
// if Tail + 1 == Head, the circular buffer is full

// Empty State: Head and Tail indexes are the same
//          Object_1    Object_2    Object_3    Object_4
//  Tail        ^
//  Head        ^
//
//
// Add item: New data put in Object_1 (push), Tail index incremented
//          Object_1    Object_2    Object_3    Object_4
//  Tail                    ^
//  Head        ^
//
//
// Add item: New data put in Object_2 (push), Tail index incremented
//          Object_1    Object_2    Object_3    Object_4
//  Tail                                ^
//  Head        ^
//
//
// Remove item: Object_1 is used (pop), Head index incremeted
//          Object_1    Object_2    Object_3    Object_4
//  Tail                                ^
//  Head                    ^
//
//
// Full State: Tail + 1 == Head, No more room in circular buffer
//          Object_1    Object_2    Object_3    Object_4
//  Tail        ^
//  Head                    ^
//
//
//
// Use:
//
// To add a new Object:
//  1 - get pointer to next available Object, Object* ptr = peekTail()
//  2 - copy new data into Object using pointer to the Object's data, char* pdata = pgmat->ptr()
//  3 - increment the Tail index by calling push()
//
// To retrieve a Object:
//  1 - get pointer to first available Object, Object* pgmat = peekHead()
//  2 - use this Object as desired
//  3 - when finished with this Object, call pop(), to increment the Head index





class CircularFifoCpu{
public:

  // constructor
  CircularFifoCpu();

  // destructor
  ~CircularFifoCpu();  // this should clean up - delete all elements in the array

  bool push();
  bool pop();

  DecodedFrameOnCpu* peekHead();
  DecodedFrameOnCpu* peekTail();

  HANDLE GetSemaphore_ItemsInQueue();
  HANDLE GetSemaphore_SpacesInQueue();
  int GetNumItemsInQueue();

  bool isEmpty() const;
  bool isFull() const;
  bool spaceAvailable() const;
  bool itemAvailable() const;
  bool isLockFree() const;

private:
  enum { Capacity = CIRCULARFIFO_CPU_SIZE + 1};
  size_t increment(size_t idx) const;
  std::atomic<size_t>   _tail;  // tail(input) index
  DecodedFrameOnCpu*    _array[Capacity];
  std::atomic<size_t>   _head; // head(output) index

  HANDLE m_hItems; // semaphore signaling the number of items in the FIFO ready to be taken out (monitored by Consumer thread)
  HANDLE m_hSpaces; // semaphore signaling the number of empty spaces in the FIFO (monitored by Producer thread)
};


#endif // CircularFifoCpu_H
