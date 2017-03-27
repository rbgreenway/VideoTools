#include "gpumatfifo.h"



GpuMatFIFO::GpuMatFIFO() : _tail(0), _head(0)
{
    m_hItems  = CreateSemaphore(NULL,0,GPUMATFIFO_SIZE,NULL);
    m_hSpaces = CreateSemaphore(NULL,GPUMATFIFO_SIZE,GPUMATFIFO_SIZE,NULL);

    Width = 0;
    Height = 0;
    Type = 0;
    m_initialized = false;
}

GpuMatFIFO::~GpuMatFIFO()
{
    if(m_initialized)
    {
        for (int i = 0; i<Capacity; i++)
        {
            delete _array[i];
        }
    }

    CloseHandle(m_hItems);
    CloseHandle(m_hSpaces);
}


void GpuMatFIFO::Init(int width, int height)
{
    Width = width;
    Height = height;
    Type = CV_8UC4;

    // destroy previous objects in array
    if(m_initialized)
    {
        for (int i = 0; i<Capacity; i++)
        {
            delete _array[i];
        }
    }

    // create new objects for array
    for (int i = 0; i<Capacity; i++)
    {
        _array[i] = new GPUMAT_FIFO_OBJECT(Width,Height,Type);
    }

    m_initialized = true;
}


bool GpuMatFIFO::push()
{
    // this is called after the data has been copied to the GpuMat
    const auto current_tail = _tail.load();
    const auto next_tail = increment(current_tail);
    if(next_tail != _head.load())
    {
      _tail.store(next_tail);

      // add 1 to the semaphore signaling the number of items in the queue
      // this semaphore is typically monitored by the Consumer thread
      ReleaseSemaphore(m_hItems,1,NULL); // used if you're Waiting for an item to show up in the queue (so yo ucan take it off)

      return true;
    }

    return false;  // full queue
}

bool GpuMatFIFO::pop()
{
    const auto current_head = _head.load();
    if(current_head == _tail.load())
       return false;   // empty queue

    _head.store(increment(current_head));

    // add 1 tot the semaphore signaling the number of empty spaces in the queue
    // this is typically monitored by the Producer thread
    ReleaseSemaphore(m_hSpaces,1,NULL); //used if you're Waiting for an opening in the queue (to put something new in it)

    return true;
}


GPUMAT_FIFO_OBJECT *GpuMatFIFO::peekTail()
{
    if(isFull())
    {
        return NULL;
    }

    return _array[_tail.load()];
}

GPUMAT_FIFO_OBJECT *GpuMatFIFO::peekHead()
{
    if(isEmpty())
    {
        return NULL;
    }

    return _array[_head.load()];
}

HANDLE GpuMatFIFO::GetSemaphore_ItemsInQueue()
{
    return m_hItems;
}

HANDLE GpuMatFIFO::GetSemaphore_SpacesInQueue()
{
    return m_hSpaces;
}

int GpuMatFIFO::GetNumItemsInQueue()
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


bool GpuMatFIFO::isEmpty() const
{
  return (_head.load() == _tail.load());
}



bool GpuMatFIFO::isFull() const
{
  const auto next_tail = increment(_tail.load());
  return (next_tail == _head.load());
}

// snapshot with acceptance that this comparison is not atomic
bool GpuMatFIFO::spaceAvailable() const
{
    const auto next_tail = increment(_tail.load());
    return (next_tail != _head.load());
}


// snapshot with acceptance that this comparison is not atomic
bool GpuMatFIFO::itemAvailable() const
{
    return (_tail.load() != _head.load());
}


bool GpuMatFIFO::isLockFree() const
{
  return (_tail.is_lock_free() && _head.is_lock_free());
}


size_t GpuMatFIFO::increment(size_t idx) const
{
  return (idx + 1) % Capacity;
}
