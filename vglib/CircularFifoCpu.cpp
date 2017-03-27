#include "CircularFifoCpu.h"

// constructor
CircularFifoCpu::CircularFifoCpu():_tail(0), _head(0)
{
    m_hItems  = CreateSemaphore(NULL,0,CIRCULARFIFO_CPU_SIZE,NULL);
    m_hSpaces = CreateSemaphore(NULL,CIRCULARFIFO_CPU_SIZE,CIRCULARFIFO_CPU_SIZE,NULL);

    for (int i = 0; i<Capacity; i++)
    {
        _array[i] = new DecodedFrameOnCpu();
    }
}


// destructor
CircularFifoCpu::~CircularFifoCpu()
{
    for (int i = 0; i<Capacity; i++)
    {
        delete _array[i];
    }

    CloseHandle(m_hItems);
    CloseHandle(m_hSpaces);
}


// Push by Producer can only update the tail
bool CircularFifoCpu::push()
{
  const auto current_tail = _tail.load(std::memory_order_relaxed);
  const auto next_tail = increment(current_tail);
  if(next_tail != _head.load(std::memory_order_acquire))
  {
    _tail.store(next_tail, std::memory_order_release);

    // add 1 to the semaphore signaling the number of items in the queue
    // this semaphore is typically monitored by the Consumer thread
    ReleaseSemaphore(m_hItems,1,NULL); // used if you're Waiting for an item to show up in the queue (so yo ucan take it off)

    return true;
  }

  return false; // full queue
}


// Pop by Consumer can only update the head (load with relaxed, store with release)
//     the tail must be accessed with at least acquire
bool CircularFifoCpu::pop()
{
  const auto current_head = _head.load(std::memory_order_relaxed);
  if(current_head == _tail.load(std::memory_order_acquire))
    return false; // empty queue

  _head.store(increment(current_head), std::memory_order_release);

  // add 1 tot the semaphore signaling the number of empty spaces in the queue
  // this is typically monitored by the Producer thread
  ReleaseSemaphore(m_hSpaces,1,NULL); //used if you're Waiting for an opening in the queue (to put something new in it)

  return true;
}



DecodedFrameOnCpu* CircularFifoCpu::peekHead()
{
    const auto current_head = _head.load(std::memory_order_acquire);
    return _array[current_head];
}



DecodedFrameOnCpu *CircularFifoCpu::peekTail()
{
    const auto current_tail = _tail.load(std::memory_order_acquire);
    return _array[current_tail];
}


HANDLE CircularFifoCpu::GetSemaphore_ItemsInQueue()
{
    return m_hItems;
}

HANDLE CircularFifoCpu::GetSemaphore_SpacesInQueue()
{
    return m_hSpaces;
}

int CircularFifoCpu::GetNumItemsInQueue()
{
    int count = 0;
    auto   t1 = _head.load();
    auto   t2 = _tail.load();
    while(t1 != t2)
    {
        count++;
        t1 = increment(t1);
    }

    return count;
}


bool CircularFifoCpu::isEmpty() const
{
  // snapshot with acceptance of that this comparison operation is not atomic
  return (_head.load() == _tail.load());
}


// snapshot with acceptance that this comparison is not atomic
bool CircularFifoCpu::isFull() const
{
  const auto next_tail = increment(_tail.load());
  return (next_tail == _head.load());
}


// snapshot with acceptance that this comparison is not atomic
bool CircularFifoCpu::spaceAvailable() const
{
    const auto next_tail = increment(_tail.load());
    return (next_tail != _head.load());
}


// snapshot with acceptance that this comparison is not atomic
bool CircularFifoCpu::itemAvailable() const
{
    return (_tail.load() != _head.load());
}



bool CircularFifoCpu::isLockFree() const
{
  return (_tail.is_lock_free() && _head.is_lock_free());
}


size_t CircularFifoCpu::increment(size_t idx) const
{
  return (idx + 1) % Capacity;
}

