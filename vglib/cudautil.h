#ifndef CUDAUTIL_H
#define CUDAUTIL_H

#define WIN32_LEAN_AND_MEAN

#include <string>

#include "cuviddec.h"
#include "cuda_runtime.h"
#include "cuda_d3d9_interop.h"
#include <WinSock2.h>
#include "nvenc/nvEncodeAPI.h"
#include <d3dx9.h>
#include "cudaD3D9.h"
#include "diagnostics.h"
#include <map>
#include "vicuda.h"

struct DirectXParams
{
	DirectXParams(){ pSurface = 0; cudaResource = 0; cudaLinearMemory = 0; cudaStream = 0; };
	IDirect3DSurface9      *pSurface;
	cudaGraphicsResource   *cudaResource;
	CUdeviceptr				cudaLinearMemory;
	CUstream                cudaStream;
	size_t					pitch;
	int						width;
	int						height;
};

class VGLIB_DirectXTools
{
public:


};




struct CudaDevice{
    int ordinal;
    CUdevice device;
    CUcontext context;
};

class CudaUtil
{
public:
	CudaUtil();    
    ~CudaUtil();

    std::string GetCudaErrorMessage(CUresult cudaResult);

    static std::string GetCudaErrorDescription(CUresult result);

    bool GetCudaDeviceCount(int &count);

    bool GetComputeCapability(int &major, int &minor);

    bool GetDeviceName(std::string &name);

    bool GetDeviceMemory(size_t &totalMem, size_t &freeMem);

    bool GetContext(CUcontext **pCtx);

    bool IsCudaReady();

	void SetContext(CUcontext *pCtx);

    std::string GetLastErrorMessage();




	bool CopyImageToSurface(int SurfaceIndex, CUdeviceptr ImageData);
	bool RemoveD3DSurface(int SurfaceIndex);
	bool AddD3DSurface(int SurfaceIndex, IDirect3DSurface9 *pSurface, int width, int height);
	bool CopyDataFromGpu(void* cpuDest, void* gpuSource, int numBytes);
	

private:
    CUresult			m_result;
    CUcontext			m_cudaContext;
    CUdevice			m_cudaDevice;

    std::string m_errMsg;
    bool m_cudaDriverReady;
    int  m_deviceCount;

	std::map<int, DirectXParams*> m_SurfaceMap;

};

#endif // CUDAUTIL_H
