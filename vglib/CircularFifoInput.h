#ifndef CIRCULARFIFOINPUT_H
#define CIRCULARFIFOINPUT_H

#include <Windows.h>

#include <atomic>
#include <cstddef>
#include <vector>

#define CIRCULARFIFO_INPUT_SIZE 8

struct EncodedInputFrame{
public:
    EncodedInputFrame(){pData=nullptr; numBytes=0;bufferSize=0;}
    ~EncodedInputFrame(){if(pData!=nullptr) delete pData;}

    char*	 pData;
    uint32_t numBytes;
    uint32_t bufferSize;
    uint64_t timestamp;
    bool     flushDecoder;
};


class CircularFifoInput{
public:

  // constructor
  CircularFifoInput();

  // destructor
  ~CircularFifoInput();  // this should clean up - delete all elements in the array

  bool push();
  bool pop();

  EncodedInputFrame* peekHead();
  EncodedInputFrame* peekTail();

  HANDLE GetSemaphore_ItemsInQueue();
  HANDLE GetSemaphore_SpacesInQueue();
  int GetNumItemsInQueue();

  bool isEmpty() const;
  bool isFull() const;
  bool spaceAvailable() const;
  bool itemAvailable() const;
  bool isLockFree() const;

private:
  enum { Capacity = CIRCULARFIFO_INPUT_SIZE + 1};
  size_t increment(size_t idx) const;
  std::atomic<size_t>   _tail;  // tail(input) index
  EncodedInputFrame*    _array[Capacity];
  std::atomic<size_t>   _head; // head(output) index

  HANDLE m_hItems; // semaphore signaling the number of items in the FIFO ready to be taken out (monitored by Consumer thread)
  HANDLE m_hSpaces; // semaphore signaling the number of empty spaces in the FIFO (monitored by Producer thread)
};



#endif // CIRCULARFIFOINPUT_H
