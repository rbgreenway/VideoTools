#ifndef VICUDA1_H
#define VICUDA1_H

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#include "cuda.h"

extern "C"
{
	void cuda_NV12ToARGB(CUdeviceptr nv12ImagePtr, CUdeviceptr argbImagePtr,  
						   uint32_t width,  uint32_t height, uint32_t nv12Pitch, uint32_t argbPitch, CUstream stream);

	void cuda_NV12ToRGB(CUdeviceptr nv12ImagePtr, CUdeviceptr rgbImagePtr,  
						  uint32_t width,  uint32_t height, uint32_t nv12Pitch, uint32_t rgbPitch);

	void cuda_CudaToGpuMat(CUdeviceptr GpuMatDataPtr, CUdeviceptr CudaImagePtr,  uint32_t width,  uint32_t height);

	void cuda_NV12PanelToComposite(CUdeviceptr PanelCUPtr, CUdeviceptr CompositeCUPtr,  
								   uint32_t PanelWidth,  uint32_t PanelHeight,
								   uint32_t CompositeWidth, uint32_t CompositeHeight,
								   uint32_t PanelRow, uint32_t PanelColumn);

	void cuda_CopyCudaArrayToD3D9Texture(CUdeviceptr pDest, CUdeviceptr pSource, uint16_t pitch, uint16_t width, uint16_t height, cudaStream_t stream = 0);
}

#endif // VICUDA1_H