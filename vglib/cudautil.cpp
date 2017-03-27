#include "cudautil.h"


CudaUtil::CudaUtil()
{
    m_cudaDriverReady = true;
    m_cudaContext = NULL;
    m_cudaDevice = -1;

    m_result = cuInit(0);

    if(m_result == CUDA_SUCCESS)
    {
        if(GetCudaDeviceCount(m_deviceCount))
        {
            // Get handle for device 0
            m_result = cuDeviceGet(&m_cudaDevice, 0);
            if(m_result != CUDA_SUCCESS)
            {
                m_errMsg = GetCudaErrorMessage(m_result);
                m_cudaDriverReady = false;

                Diagnostics::DebugMessage("Error in cuDeviceGet");
                Diagnostics::DebugMessage(GetCudaErrorMessage(m_result));
            }
            else
            {
                // Create context
				
				m_result = cuCtxCreate(&m_cudaContext, 0, m_cudaDevice);				

                if(m_result != CUDA_SUCCESS)
                {
                    m_errMsg = GetCudaErrorMessage(m_result);
                    m_cudaDriverReady = false;
                    Diagnostics::DebugMessage("Error in cuCtxCreate");
                    Diagnostics::DebugMessage(GetCudaErrorMessage(m_result));
                }
                else
                {                   
                    // pop the cuda context to create a "floating context", i.e. one that can be passed to other threads
                    m_result = cuCtxPopCurrent(&m_cudaContext);
                    if(m_result != CUDA_SUCCESS)
                    {
                        m_errMsg = GetCudaErrorMessage(m_result);
                        m_cudaDriverReady = false;
                        Diagnostics::DebugMessage("Error in cuCtxPopCurrent");
                        Diagnostics::DebugMessage(GetCudaErrorMessage(m_result));
                    }
                    else
                    {
                        cuCtxPushCurrent(m_cudaContext); // set the current cuda context
						
						cudaFree(0);  // call this to force Cuda to initialize (and thus getting rid of the delay during the first call to a Cuda function)
                    }
                }
            }
        }
    }
    else
    {
        m_errMsg = GetCudaErrorMessage(m_result);
        m_cudaDriverReady = false;
    }
}




CudaUtil::~CudaUtil()
{
    if (m_cudaContext)
    {
        CUresult err = cuCtxDestroy(m_cudaContext);
        if (err != CUDA_SUCCESS)
            printf("WARNING: cuCtxDestroy failed (%d)\n", err);
        m_cudaContext = NULL;
    }
}





bool CudaUtil::GetCudaDeviceCount(int &count)
{
    if(!m_cudaDriverReady)
    {
        return false;
    }

    bool success = true;

    m_result = cuDeviceGetCount(&count);
    if(m_result != CUDA_SUCCESS)
    {
        m_errMsg = GetCudaErrorMessage(m_result);
        success = false;
    }

    return success;
}


bool CudaUtil::GetComputeCapability(int &major, int &minor)
{
    bool success = true;

    if(!m_cudaDriverReady)
    {
        return false;
    }

    int value;
    m_result = cuDeviceGetAttribute(&value,CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR,m_cudaDevice);

    if(m_result == CUDA_SUCCESS)
    {
        major = value;

        m_result = cuDeviceGetAttribute(&value,CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR,m_cudaDevice);

        if(m_result == CUDA_SUCCESS)
        {
            minor = value;
        }
        else
        {
            m_errMsg = GetCudaErrorMessage(m_result);
            success = false;
        }
    }
    else
    {
        m_errMsg = GetCudaErrorMessage(m_result);
        success = false;
    }

    return success;
}

bool CudaUtil::GetDeviceName(std::string &name)
{
    bool success = true;

    if(!m_cudaDriverReady)
    {
        return false;
    }

    char _name[100];

    m_result = cuDeviceGetName (_name, 99, m_cudaDevice);
    if(m_result == CUDA_SUCCESS)
    {
        std::string str(_name);
        name = str;
    }
    else
    {
        name = "unknown";
        success = false;
    }

    return success;
}

bool CudaUtil::GetDeviceMemory(size_t &totalMem, size_t &freeMem)
{
    bool success = true;

    if(!m_cudaDriverReady)
    {
        return false;
    }

    m_result = cuMemGetInfo(&freeMem,&totalMem);
    if(m_result == CUDA_SUCCESS)
    {
    }
    else
    {
        freeMem = (size_t)0;
        totalMem = (size_t)0;
        success = false;
    }

    return success;
}



bool CudaUtil::GetContext(CUcontext **pCtx)
{
    bool success = true;

    if(!m_cudaDriverReady)
    {
        m_errMsg = "Cuda Driver not ready";
        return false;
    }

    *pCtx = &m_cudaContext;

    return success;
}

bool CudaUtil::IsCudaReady()
{
    return m_cudaDriverReady;
}



void CudaUtil::SetContext(CUcontext *pCtx)
{
	SetContext(pCtx);
}



std::string CudaUtil::GetLastErrorMessage()
{
    return m_errMsg;
}



std::string CudaUtil::GetCudaErrorMessage(CUresult cudaResult)
{
    if(!m_cudaDriverReady)
    {
        return "Cuda Driver could not initialize";
    }


    char msg[2048];
    const char* pmsg = &msg[0];
    const char** ppmsg = &pmsg;
    CUresult result = cuGetErrorString(cudaResult, ppmsg);

    if(result != CUDA_SUCCESS)
    {
        m_errMsg = "Failed to retrieve error message for CUresult = " + std::to_string((int)cudaResult);
    }
    else
    {
        std::string errMsg(pmsg);
        m_errMsg = errMsg;
    }

    return m_errMsg;
}




std::string CudaUtil::GetCudaErrorDescription(CUresult result)
{
    // these error descriptions are taken from cuda.h

    std::string errMsg;

    switch(result)
    {

    case CUDA_SUCCESS:
        errMsg = "No Errors";
        break;

    case CUDA_ERROR_INVALID_VALUE:
        errMsg = "One or more of the parameters passed to the API call is not within an acceptable range of values.";
        break;

    case CUDA_ERROR_OUT_OF_MEMORY:
        errMsg = "The API call failed because it was unable to allocate enough memory to perform the requested operation.";
        break;

    case CUDA_ERROR_NOT_INITIALIZED:
        errMsg = "The CUDA driver has not been initialized with ::cuInit() or that initialization has failed.";
        break;

    case CUDA_ERROR_DEINITIALIZED:
        errMsg = "The CUDA driver is in the process of shutting down.";
        break;

    case CUDA_ERROR_PROFILER_DISABLED:
        errMsg = "Profiler is not initialized for this run. This can happen when the application is running with external profiling tools like visual profiler.";
        break;

    case CUDA_ERROR_NO_DEVICE:
        errMsg = "No CUDA-capable devices were detected by the installed CUDA driver.";
        break;

    case CUDA_ERROR_INVALID_DEVICE:
        errMsg = "The device ordinal supplied by the user does not correspond to a valid CUDA device.";
        break;

    case CUDA_ERROR_INVALID_IMAGE:
        errMsg = "the device kernel image is invalid. This can also indicate an invalid CUDA module.";
        break;

    case CUDA_ERROR_INVALID_CONTEXT:
        errMsg = "This most frequently indicates that there is no context bound to the\ncurrent thread. This can also be returned if the context passed to an\nAPI call is not a valid handle (such as a context that has had\n::cuCtxDestroy() invoked on it). This can also be returned if a user\nmixes different API versions (i.e. 3010 context with 3020 API calls).\nSee ::cuCtxGetApiVersion() for more details.";
        break;

    case CUDA_ERROR_CONTEXT_ALREADY_CURRENT:
        errMsg = "The context being supplied as a parameter to the API call was already the active context.";
        break;

    case CUDA_ERROR_MAP_FAILED:
        errMsg = "A map or register operation has failed.";
        break;

    case CUDA_ERROR_UNMAP_FAILED:
        errMsg = "A unmap or register operation has failed.";
        break;

    case CUDA_ERROR_ARRAY_IS_MAPPED:
        errMsg = "The specified array is currently mapped and thus cannot be destroyed.";
        break;

    case CUDA_ERROR_ALREADY_MAPPED:
        errMsg = "The resource is already mapped.";
        break;

    case CUDA_ERROR_NO_BINARY_FOR_GPU:
        errMsg = "There is no kernel image available that is suitable\n for the device. This can occur when a user specifies code generation\noptions for a particular CUDA source file that do not include the\ncorresponding device configuration.";
        break;

    case CUDA_ERROR_ALREADY_ACQUIRED:
        errMsg = "A resource has already been acquired.";
        break;

    case CUDA_ERROR_NOT_MAPPED:
        errMsg = "A resource is not mapped.";
        break;

    case CUDA_ERROR_NOT_MAPPED_AS_ARRAY:
        errMsg = "A mapped resource is not available for access as an array.";
        break;

    case CUDA_ERROR_NOT_MAPPED_AS_POINTER:
        errMsg = "A mapped resource is not available for access as a pointer.";
        break;

    case CUDA_ERROR_ECC_UNCORRECTABLE:
        errMsg = "An uncorrectable ECC error was detected during execution.";
        break;

    case CUDA_ERROR_UNSUPPORTED_LIMIT:
        errMsg = "The ::CUlimit passed to the API call is not supported by the active device.";
        break;

    case CUDA_ERROR_CONTEXT_ALREADY_IN_USE:
        errMsg = "The ::CUcontext passed to the API call can only be bound\nto a single CPU thread at a time but is already bound\nto a CPU thread.";
        break;

    case CUDA_ERROR_PEER_ACCESS_UNSUPPORTED:
        errMsg = "Peer access is not supported across the given devices.";
        break;

    case CUDA_ERROR_INVALID_PTX:
        errMsg = "A PTX JIT compilation failed.";
        break;

    case CUDA_ERROR_INVALID_GRAPHICS_CONTEXT:
        errMsg = "An error with OpenGL or DirectX context.";
        break;

    case CUDA_ERROR_INVALID_SOURCE:
        errMsg = "The device kernel source is invalid.";
        break;

    case CUDA_ERROR_FILE_NOT_FOUND:
        errMsg = "The file specified was not found.";
        break;

    case CUDA_ERROR_SHARED_OBJECT_SYMBOL_NOT_FOUND:
        errMsg = "A link to a shared object failed to resolve.";
        break;

    case CUDA_ERROR_SHARED_OBJECT_INIT_FAILED:
        errMsg = "Initialization of a shared object failed.";
        break;

    case CUDA_ERROR_OPERATING_SYSTEM:
        errMsg = "An OS call failed.";
        break;

    case CUDA_ERROR_INVALID_HANDLE:
        errMsg = "A resource handle passed to the API call was not valid.\nResource handles are opaque types like ::CUstream and ::CUevent.";
        break;

    case CUDA_ERROR_NOT_FOUND:
        errMsg = "A named symbol was not found. Examples of symbols are global/constant\nvariable names, texture names, and surface names.";
        break;

    case CUDA_ERROR_NOT_READY:
        errMsg = "Asynchronous operations issued previously have not completed yet.\nThis result is not actually an error, but must be indicated\ndifferently than ::CUDA_SUCCESS (which indicates completion). Calls that\nmay return this value include ::cuEventQuery() and ::cuStreamQuery().";
        break;

    case CUDA_ERROR_ILLEGAL_ADDRESS:
        errMsg = "While executing a kernel, the device encountered a\nload or store instruction on an invalid memory address.\nThe context cannot be used, so it must be destroyed (and a new one should be created).\nAll existing device memory allocations from this context are invalid\nand must be reconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_LAUNCH_OUT_OF_RESOURCES:
        errMsg = "This indicates that a launch did not occur because it did not have\nappropriate resources. This error usually indicates that the user has\nattempted to pass too many arguments to the device kernel, or the\nkernel launch specifies too many threads for the kernel's register\ncount. Passing arguments of the wrong size (i.e. a 64-bit pointer\nwhen a 32-bit int is expected) is equivalent to passing too many\narguments and can also result in this error.";
        break;

    case CUDA_ERROR_LAUNCH_TIMEOUT:
        errMsg = "This indicates that the device kernel took too long to execute. This can\nonly occur if timeouts are enabled - see the device attribute\n::CU_DEVICE_ATTRIBUTE_KERNEL_EXEC_TIMEOUT for more information. The\ncontext cannot be used (and must be destroyed similar to\n::CUDA_ERROR_LAUNCH_FAILED). All existing device memory allocations from\nthis context are invalid and must be reconstructed if the program is to\ncontinue using CUDA.";
        break;

    case CUDA_ERROR_LAUNCH_INCOMPATIBLE_TEXTURING:
        errMsg = "A kernel launch that uses an incompatible texturing mode.";
        break;

    case CUDA_ERROR_PEER_ACCESS_ALREADY_ENABLED:
        errMsg = "A call to ::cuCtxEnablePeerAccess() is trying to re-enable peer\naccess to a context which has already had peer access to it enabled.";
        break;

    case CUDA_ERROR_PEER_ACCESS_NOT_ENABLED:
        errMsg = "::cuCtxDisablePeerAccess() is trying to disable peer access which has not been\nenabled yet via ::cuCtxEnablePeerAccess().";
        break;

    case CUDA_ERROR_PRIMARY_CONTEXT_ACTIVE:
        errMsg = "The primary context for the specified device has already been initialized.";
        break;

    case CUDA_ERROR_CONTEXT_IS_DESTROYED:
        errMsg = "The context current to the calling thread has been destroyed using\n::cuCtxDestroy, or is a primary context which has not yet been initialized.";
        break;

    case CUDA_ERROR_ASSERT:
        errMsg = "A device-side assert triggered during kernel execution. The context\ncannot be used anymore, and must be destroyed. All existing device\nmemory allocations from this context are invalid and must be\nreconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_TOO_MANY_PEERS:
        errMsg = "The hardware resources required to enable peer access have been\nexhausted for one or more of the devices passed to ::cuCtxEnablePeerAccess().";
        break;

    case CUDA_ERROR_HOST_MEMORY_ALREADY_REGISTERED:
        errMsg = "The memory range passed to ::cuMemHostRegister() has already been registered.";
        break;

    case CUDA_ERROR_HOST_MEMORY_NOT_REGISTERED:
        errMsg = "The pointer passed to ::cuMemHostUnregister() does not correspond to any currently registered memory region.";
        break;

    case CUDA_ERROR_HARDWARE_STACK_ERROR:
        errMsg = "While executing a kernel, the device encountered a stack error.\nThis can be due to stack corruption or exceeding the stack size limit.\nThe context cannot be used, so it must be destroyed (and a new one should be created).\nAll existing device memory allocations from this context are invalid\nand must be reconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_ILLEGAL_INSTRUCTION:
        errMsg = "While executing a kernel, the device encountered an illegal instruction.\nThe context cannot be used, so it must be destroyed (and a new one should be created).\nAll existing device memory allocations from this context are invalid\nand must be reconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_MISALIGNED_ADDRESS:
        errMsg = "While executing a kernel, the device encountered a load or store instruction\non a memory address which is not aligned.\nThe context cannot be used, so it must be destroyed (and a new one should be created).\nAll existing device memory allocations from this context are invalid\nand must be reconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_INVALID_ADDRESS_SPACE:
        errMsg = "While executing a kernel, the device encountered an instruction\nwhich can only operate on memory locations in certain address spaces\n(global, shared, or local), but was supplied a memory address not\nbelonging to an allowed address space.\nThe context cannot be used, so it must be destroyed (and a new one should be created).\nAll existing device memory allocations from this context are invalid\nand must be reconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_INVALID_PC:
        errMsg = "While executing a kernel, the device program counter wrapped its address space.\nThe context cannot be used, so it must be destroyed (and a new one should be created).\nAll existing device memory allocations from this context are invalid\nand must be reconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_LAUNCH_FAILED:
        errMsg = "An exception occurred on the device while executing a kernel. Common\ncauses include dereferencing an invalid device pointer and accessing\nout of bounds shared memory. The context cannot be used, so it must\nbe destroyed (and a new one should be created). All existing device\nmemory allocations from this context are invalid and must be\nreconstructed if the program is to continue using CUDA.";
        break;

    case CUDA_ERROR_NOT_PERMITTED:
        errMsg = "The attempted operation is not permitted.";
        break;

    case CUDA_ERROR_NOT_SUPPORTED:
        errMsg = "The attempted operation is not supported on the current system or device.";
        break;

    case CUDA_ERROR_UNKNOWN:
        errMsg = "An unknown internal error has occurred.";
        break;

    default:
        errMsg = "An unknown CUDA error.";
        break;
    }

 return errMsg;
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// DirectX Interop Stuff


bool CudaUtil::CopyImageToSurface(int SurfaceIndex, CUdeviceptr ImageData)
{
	if (m_SurfaceMap.find(SurfaceIndex) == m_SurfaceMap.end()) {
		// surface with this index not found
		return false;
	}

	cuCtxPushCurrent(m_cudaContext);

	DirectXParams *pparams = m_SurfaceMap[SurfaceIndex];


	cudaError_t result_t;
	cudaStream_t    stream = 0;  
	
	//cudaStreamCreate(&stream);
	cudaStreamCreateWithFlags(&stream, cudaStreamNonBlocking);

	const int nbResources = 1;
	cudaGraphicsResource *ppResources[nbResources] =
	{
		pparams->cudaResource
	};

	result_t = cudaGraphicsMapResources(nbResources, ppResources, stream);


	//
	// run kernels which will populate the contents of those textures
	//
	
	//cuda_CopyCudaArrayToD3D9Texture(pparams->cudaLinearMemory, ImageData, pparams->pitch, pparams->width, pparams->height, stream);
	//	
	//cudaArray *cuArray;
	//
	//cudaGraphicsSubResourceGetMappedArray(&cuArray, pparams->cudaResource, 0, 0);

	//// then we want to copy cudaLinearMemory to the D3D texture, via its mapped form : cudaArray	
	//cudaMemcpy2DToArrayAsync(
	//	cuArray, // dst array
	//	0, 0,    // offset
	//	(void*)pparams->cudaLinearMemory, 
	//	pparams->pitch,       // src
	//	pparams->width * 4, pparams->height, // extent
	//	cudaMemcpyDeviceToDevice,stream); // kind

	////
	//// unmap the resources
	////
	//result_t = cudaGraphicsUnmapResources(nbResources, ppResources, stream);


	// TEST START

	cudaArray *cuArray;

	cudaGraphicsSubResourceGetMappedArray(&cuArray, pparams->cudaResource, 0, 0);
	
	cudaMemcpyToArrayAsync(cuArray, 0, 0, (void*)ImageData, pparams->pitch*pparams->height, cudaMemcpyDeviceToDevice, stream);
	
	result_t = cudaGraphicsUnmapResources(nbResources, ppResources, stream);

	// TEST END


	//cudaStreamSynchronize(stream);

	cudaStreamDestroy(stream);

	cuCtxPopCurrent(&m_cudaContext);


	return true;
}




bool CudaUtil::RemoveD3DSurface(int SurfaceIndex)
{
	bool success = true;
	if (m_SurfaceMap.find(SurfaceIndex) == m_SurfaceMap.end()) {
		// not found
		success = false;
	}
	else {
		// found
		DirectXParams *ptr = m_SurfaceMap[SurfaceIndex];
		cudaFree((void*)ptr->cudaLinearMemory);
		cudaFree(ptr->cudaResource);
		m_SurfaceMap.erase(SurfaceIndex);
	}
	return success;
}



bool CudaUtil::AddD3DSurface(int SurfaceIndex, IDirect3DSurface9 *pSurface, int width, int height)
{
	DirectXParams *pparams = new DirectXParams();

	pparams->pSurface = pSurface;
	pparams->width = width;
	pparams->height = height;
	pparams->cudaLinearMemory = 0;
	pparams->cudaResource = 0;
	pparams->pitch = width;


	bool success = true;

	// check to see if a surface already exists for given surface index...if so, release the resources it has

	if (m_SurfaceMap.find(SurfaceIndex) == m_SurfaceMap.end()) {
		// not found
	}
	else {
		// found
		RemoveD3DSurface(SurfaceIndex);
	}

	cudaError_t res = cudaGraphicsD3D9RegisterResource(&pparams->cudaResource, pparams->pSurface, cudaGraphicsRegisterFlagsNone);

	// cuda cannot write into the texture directly : the texture is seen as a cudaArray and can only be mapped as a texture
	// Create a buffer so that cuda can write into it
	// pixel fmt is DXGI_FORMAT_R8G8B8A8_SNORM
	if (res == cudaSuccess)
	{
		void * linearMemory;
		res = cudaMallocPitch(&linearMemory, &pparams->pitch, pparams->width * 4, pparams->height);
		if (res == cudaSuccess)
		{
			pparams->cudaLinearMemory = (CUdeviceptr)linearMemory;
			res = cudaMemset((void*)pparams->cudaLinearMemory, 1, pparams->pitch * pparams->height);
			if (res != cudaSuccess) success = false;
		}
		else success = false;
	}
	else success = false;

	if (success)
	{
		std::pair<std::map<int, DirectXParams*>::iterator, bool> result = m_SurfaceMap.insert(std::pair<int, DirectXParams*>(SurfaceIndex, pparams));
		success = result.second;
	}

	return success;
}


bool CudaUtil::CopyDataFromGpu(void* cpuDest, void* gpuSource, int numBytes)
{
	bool success = true;

	cudaError_t result = cudaMemcpy(cpuDest, gpuSource, numBytes, cudaMemcpyDeviceToHost);

	if (result != cudaSuccess) success = false;

	return success;
}