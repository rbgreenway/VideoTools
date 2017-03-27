#ifndef CIRCULARFIFOOUTPUT_H
#define CIRCULARFIFOOUTPUT_H

#include <Windows.h>

#include <atomic>
#include <cstddef>

#include "nvenc/NvHWEncoder.h"

#define MAX_OUTPUT_QUEUE_SIZE 8


class CircularFifoOutput{
public:

  // constructor
  CircularFifoOutput();

  // destructor
  ~CircularFifoOutput();  // this should clean up - delete all elements in the array

  bool push();
  bool pop();

  ENCODED_FRAME_DATA* peekHead();
  ENCODED_FRAME_DATA* peekTail();

  HANDLE GetSemaphore_ItemsInQueue();
  HANDLE GetSemaphore_SpacesInQueue();
  int GetNumItemsInQueue();

  bool isEmpty() const;
  bool isFull() const;
  bool spaceAvailable() const;
  bool itemAvailable() const;
  bool isLockFree() const;

private:
  enum { Capacity = MAX_OUTPUT_QUEUE_SIZE + 1};
  size_t increment(size_t idx) const;
  std::atomic<size_t>   _tail;  // tail(input) index
  ENCODED_FRAME_DATA*    _array[Capacity];
  std::atomic<size_t>   _head; // head(output) index

  HANDLE m_hItems; // semaphore signaling the number of items in the FIFO ready to be taken out (monitored by Consumer thread)
  HANDLE m_hSpaces; // semaphore signaling the number of empty spaces in the FIFO (monitored by Producer thread)
};


#endif // CIRCULARFIFOOUTPUT_H
