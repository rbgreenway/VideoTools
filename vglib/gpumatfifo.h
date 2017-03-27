#ifndef GPUMATFIFO_H
#define GPUMATFIFO_H

#include <Windows.h>

#include <atomic>

#include <cuda.h>

// OpenCV headers
//#include <opencv2\opencv.hpp>
//#include <opencv2/cudev.hpp>
//#include <opencv2\gpu\gpu.hpp>
//#include <opencv2\video\background_segm.hpp>
//#include <opencv2\core\core.hpp>
//#include <opencv2\features2d\features2d.hpp>
//#include <opencv2\highgui\highgui.hpp>
//#include <opencv2\calib3d\calib3d.hpp>
//#include <opencv2\imgproc\imgproc.hpp>
//#include <opencv2\objdetect\objdetect.hpp>
//#include <opencv2\nonfree\nonfree.hpp>
//#include <opencv2\legacy\compat.hpp>
//#include <opencv2\gpu\gpumat.hpp>

#include "opencv2/core.hpp"
#include "opencv2/core/utility.hpp"
#include "opencv2/cudabgsegm.hpp"
#include "opencv2/cudalegacy.hpp"
#include "opencv2/video.hpp"

using namespace cv;
using namespace cv::cuda;
using namespace cv::cudev;

#define GPUMATFIFO_SIZE 4 // max number of GpuMat objects in array

struct GPUMAT_FIFO_OBJECT{
    GPUMAT_FIFO_OBJECT(){pGpuMat = nullptr; timestamp = 0;}
    GPUMAT_FIFO_OBJECT(int width, int height, int OpenCvType){
        timestamp = 0;
        CUdeviceptr LoadFrameData;
        CUresult result = cuMemAlloc(&LoadFrameData, width * 4 * height);

        if(result == CUDA_SUCCESS)
        {
            pGpuMat = new GpuMat(height,width,OpenCvType,(void*)LoadFrameData);
        }
        else
        {
            pGpuMat = nullptr;
        }
    }
    ~GPUMAT_FIFO_OBJECT(){if(pGpuMat!=nullptr)delete pGpuMat;}
    GpuMat* pGpuMat;
    uint64_t timestamp;
};


// Lock-free, Single Producer, Single Consumer Circular Queue (actually an array)

// NOTE: This class is meant for use with a SINGLE Producer thread and a SINGLE Consumer thread.  This means
//       that ONLY 1 Thread should call push() and ONLY 1 Thread should call pop().  This turns out to be
//       the Producer thread calls push() and the Consumer thread calls pop().

// When this class is constructed, it builds the array with GpuMat objects that have been set to a specified
// width, height, and OpenCV type (which for now should be CV_8UC4).  This allocates memory space on the GPU
// for the data for each GpuMat.  When we have new data for a GpuMat, we simply take the next available GpuMat
// from the array (call peekTail()), copy the data to that GpuMat, and then increment the Tail index (by calling push()).
// When want to use a GpuMat that is in the Queue, we simply call peekHead() to get a pointer to the next GpuMat object
// ready for use.  Once we're done with that GpuMat, we free it up for use by calling pop().

// This approach prevents us from recreating new GpuMat objects constantly.  Instead, we have a circular buffer
// of GpuMat objects are are re-used by just copying new data into them.


// Tail and Head are indexes of an array of GpuMat objects
// Tail = points to GpuMat ready for new data
// Head = points to GpuMat that has data and is ready for use
// If Tail == Head, the circular buffer is empty
// if Tail + 1 == Head, the circular buffer is full

// Empty State: Head and Tail indexes are the same
//          GpuMat_1    GpuMat_2    GpuMat_3    GpuMat_4
//  Tail        ^
//  Head        ^
//
//
// Add item: New data put in GpuMat_1 (push), Tail index incremented
//          GpuMat_1    GpuMat_2    GpuMat_3    GpuMat_4
//  Tail                    ^
//  Head        ^
//
//
// Add item: New data put in GpuMat_2 (push), Tail index incremented
//          GpuMat_1    GpuMat_2    GpuMat_3    GpuMat_4
//  Tail                                ^
//  Head        ^
//
//
// Remove item: GpuMat_1 is used (pop), Head index incremeted
//          GpuMat_1    GpuMat_2    GpuMat_3    GpuMat_4
//  Tail                                ^
//  Head                    ^
//
//
// Full State: Tail + 1 == Head, No more room in circular buffer
//          GpuMat_1    GpuMat_2    GpuMat_3    GpuMat_4
//  Tail        ^
//  Head                    ^
//
//
//
// Use:
//
// To add a new GpuMat:
//  1 - get pointer to next available GpuMat, GpuMat* pgmat = GpuMatFIFO::peekTail()
//  2 - copy new data into GpuMat using pointer to the GpuMat's data, char* pdata = pgmat->ptr()
//  3 - increment the Tail index by calling GpuMatFIFO::push()
//
// To retrieve a GpuMat:
//  1 - get pointer to first available GpuMat, GpuMat* pgmat = GpuMatFIFO::peekHead()
//  2 - use this GpuMat as desired
//  3 - when finished with this GpuMat, call GpuMatFIFO::pop(), to increment the Head index





class GpuMatFIFO{
public:
  GpuMatFIFO();
  ~GpuMatFIFO();

  void Init(int width, int height);

  bool push();
  bool pop();
  GPUMAT_FIFO_OBJECT * peekTail();
  GPUMAT_FIFO_OBJECT * peekHead();

  HANDLE GetSemaphore_ItemsInQueue();
  HANDLE GetSemaphore_SpacesInQueue();
  int GetNumItemsInQueue();

  bool isEmpty() const;
  bool isFull() const;
  bool spaceAvailable() const;
  bool itemAvailable() const;
  bool isLockFree() const;

private:
  enum { Capacity = GPUMATFIFO_SIZE + 1};
  int Width;
  int Height;
  int Type;
  bool m_initialized;
  size_t increment(size_t idx) const;
  std::atomic<size_t>  _tail;  // tail(input) index
  std::atomic<size_t>  _head;  // head(output) index
  GPUMAT_FIFO_OBJECT*    _array[Capacity];

  HANDLE m_hItems; // semaphore signaling the number of items in the FIFO ready to be taken out (monitored by Consumer thread)
  HANDLE m_hSpaces; // semaphore signaling the number of empty spaces in the FIFO (monitored by Producer thread)
};

#endif // GPUMATFIFO_H
