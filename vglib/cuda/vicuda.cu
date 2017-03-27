#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#include "cuda.h"
#include "../helper_cuda.h"
#include "opencv2/cudev.hpp"
using namespace cv::cuda;


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Utility Functions
struct GpuTimer
{
	cudaEvent_t start;
	cudaEvent_t stop;

	GpuTimer()
	{
		cudaEventCreate(&start);
		cudaEventCreate(&stop);
	}

	~GpuTimer()
	{
		cudaEventDestroy(start);
		cudaEventDestroy(stop);
	}

	void Start()
	{
		cudaEventRecord(start, 0);
	}

	void Stop()
	{
		cudaEventRecord(stop, 0);
	}

	float ElapsedMillis()
	{
		float elapsed;
		cudaEventSynchronize(stop);
		cudaEventElapsedTime(&elapsed, start, stop);
		return elapsed;
	}
};


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////


// CUDA kernel for outputing the final ARGB output from NV12;

__global__ void NV12ToARGB(uint32_t *srcImage,   size_t nv12Pitch,
                             uint32_t *dstImage,   size_t argbPitch,
                             uint32_t width,       uint32_t height)
{
    int32_t x, y;
    uint32_t yuv101010Pel[2];
    uint32_t processingPitch = nv12Pitch;
    uint32_t dstImagePitch   = argbPitch >> 2;
    uint8_t *srcImageU8     = (uint8_t *)srcImage;


    // Pad borders with duplicate pixels, and we multiply by 2 because we process 2 pixels per thread
    x = blockIdx.x * (blockDim.x << 1) + (threadIdx.x << 1);
    y = blockIdx.y *  blockDim.y       +  threadIdx.y;

    if (x >= width)
        return; //x = width - 1;

    if (y >= height)
        return; // y = height - 1;

    // Read 2 Luma components at a time, so we don't waste processing since CbCr are decimated this way.
    // if we move to texture we could read 4 luminance values
	// Also convert to 10-bit value (by shifting left by 2)
    yuv101010Pel[0] = (srcImageU8[y * processingPitch + x    ]) << 2;
    yuv101010Pel[1] = (srcImageU8[y * processingPitch + x + 1]) << 2;

    uint32_t chromaOffset    = processingPitch * height;
    int32_t y_chroma = y >> 1;

    if (y & 1)  // odd scanline ?
    {
        uint32_t chromaCb;
        uint32_t chromaCr;

        chromaCb = srcImageU8[chromaOffset + y_chroma * processingPitch + x    ];
        chromaCr = srcImageU8[chromaOffset + y_chroma * processingPitch + x + 1];

        if (y_chroma < ((height >> 1) - 1)) // interpolate chroma vertically
        {
            chromaCb = (chromaCb + srcImageU8[chromaOffset + (y_chroma + 1) * processingPitch + x    ] + 1) >> 1;
            chromaCr = (chromaCr + srcImageU8[chromaOffset + (y_chroma + 1) * processingPitch + x + 1] + 1) >> 1;
        }

        yuv101010Pel[0] |= (chromaCb << 12);
        yuv101010Pel[0] |= (chromaCr << 22);

        yuv101010Pel[1] |= (chromaCb << 12);
        yuv101010Pel[1] |= (chromaCr << 22);
    }
    else
    {
        yuv101010Pel[0] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x    ] << 12);
        yuv101010Pel[0] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x + 1] << 22);

        yuv101010Pel[1] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x    ] << 12);
        yuv101010Pel[1] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x + 1] << 22);
    }

    // this steps performs the color conversion
    uint32_t yuvi[6];
    float red[2], green[2], blue[2];

    yuvi[0] = (yuv101010Pel[0] &   0x3FF);
    yuvi[1] = ((yuv101010Pel[0] >>  10)       & 0x3FF);
    yuvi[2] = ((yuv101010Pel[0] >> 20) & 0x3FF);

    yuvi[3] = (yuv101010Pel[1] &   0x3FF);
    yuvi[4] = ((yuv101010Pel[1] >>  10)       & 0x3FF);
    yuvi[5] = ((yuv101010Pel[1] >> 20) & 0x3FF);

    // YUV to RGB Transformation conversion
   	float luma[2], chromaCb[2], chromaCr[2];	
	uint32_t ARGB[2];

	// Prepare for hue adjustment
    luma[0]     = (float)yuvi[0];
    chromaCb[0] = (float)((int32_t)yuvi[1] - 512.0f);
    chromaCr[0] = (float)((int32_t)yuvi[2] - 512.0f);

	luma[1]     = (float)yuvi[3];
    chromaCb[1] = (float)((int32_t)yuvi[4] - 512.0f);
    chromaCr[1] = (float)((int32_t)yuvi[5] - 512.0f);

    // Convert YUV To RGB with hue adjustment
    red[0]  = (luma[0] * 1.1644f) +						       (chromaCr[0] * 1.5960f);           
    green[0]= (luma[0] * 1.1644f) + (chromaCb[0] * -0.3918f) + (chromaCr[0] * -0.8130f);            
    blue[0] = (luma[0] * 1.1644f) + (chromaCb[0] * 2.0172f);

	red[1]  = (luma[1] * 1.1644f) +						       (chromaCr[1] * 1.5960f);           
    green[1]= (luma[1] * 1.1644f) + (chromaCb[1] * -0.3918f) + (chromaCr[1] * -0.8130f);            
    blue[1] = (luma[1] * 1.1644f) + (chromaCb[1] * 2.0172f);


    // Clamp the results to RGBA to 10 bits
	if(red[0]<0.0f)   red[0]   = 0.0f;	if(red[0]>1023.0f)   red[0]   = 1023.0f;
	if(green[0]<0.0f) green[0] = 0.0f;	if(green[0]>1023.0f) green[0] = 1023.0f;
	if(blue[0]<0.0f)  blue[0]  = 0.0f;	if(blue[0]>1023.0f)  blue[0]  = 1023.0f;

	if(red[1]<0.0f)   red[1]   = 0.0f;	if(red[1]>1023.0f)   red[1]   = 1023.0f;
	if(green[1]<0.0f) green[1] = 0.0f;	if(green[1]>1023.0f) green[1] = 1023.0f;
	if(blue[1]<0.0f)  blue[1]  = 0.0f;	if(blue[1]>1023.0f)  blue[1]  = 1023.0f;

	
	// Convert to 8 bit unsigned integers per color component with alpha = 0xff000000
    ARGB[0] = (((uint32_t)blue[0]  >> 2) | (((uint32_t)green[0] >> 2) << 8) | (((uint32_t)red[0]   >> 2) << 16) | 0xff000000);
	ARGB[1] = (((uint32_t)blue[1]  >> 2) | (((uint32_t)green[1] >> 2) << 8) | (((uint32_t)red[1]   >> 2) << 16) | 0xff000000);
	
	// copy to destination image
    dstImage[y * dstImagePitch + x     ] = ARGB[0];
    dstImage[y * dstImagePitch + x + 1 ] = ARGB[1];

    __syncthreads();  // wait for all threads to complete
}

extern "C"
void cuda_NV12ToARGB(CUdeviceptr nv12ImagePtr, CUdeviceptr argbImagePtr,  
					   uint32_t width,  uint32_t height, uint32_t nv12Pitch, uint32_t argbPitch, CUstream stream)
{
	uint32_t * srcImage = (uint32_t*)nv12ImagePtr;
    uint32_t * dstImage = (uint32_t*)argbImagePtr;
       
	dim3 threadsPerBlock(32,16);  // 32x16 = 512 threads per block

	// NOTE the 2 here ------V   and here ------------V which are there because each thread processes 2 pixels at a time 
	dim3 numBlocks((width + (2*threadsPerBlock.x)-1)/(2*threadsPerBlock.x), (height + threadsPerBlock.y-1)/threadsPerBlock.y); 
	NV12ToARGB<<<numBlocks,threadsPerBlock,0,stream>>>(srcImage,nv12Pitch,dstImage,argbPitch,width,height);
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////

// CUDA kernel for outputing the final RGB output from NV12;

__global__ void NV12ToRGB(uint32_t *srcImage,   size_t nv12Pitch,
                             uint8_t *dstImage,   size_t rgbPitch,
                             uint32_t width,       uint32_t height)
{
    int32_t x, y;
    uint32_t yuv101010Pel[2];
    uint32_t processingPitch = nv12Pitch;
    uint32_t dstImagePitch   = rgbPitch;
    uint8_t *srcImageU8     = (uint8_t *)srcImage;


    // Pad borders with duplicate pixels, and we multiply by 2 because we process 2 pixels per thread
    x = blockIdx.x * (blockDim.x << 1) + (threadIdx.x << 1);
    y = blockIdx.y *  blockDim.y       +  threadIdx.y;

    if (x >= width) return; 
    if (y >= height) return; 

    // Read 2 Luma components at a time, so we don't waste processing since CbCr are decimated this way.
    // if we move to texture we could read 4 luminance values
	// Also convert to 10-bit value (by shifting left by 2)
    yuv101010Pel[0] = (srcImageU8[y * processingPitch + x    ]) << 2;
    yuv101010Pel[1] = (srcImageU8[y * processingPitch + x + 1]) << 2;

    uint32_t chromaOffset    = processingPitch * height;
    int32_t y_chroma = y >> 1;

    if (y & 1)  // odd scanline ?
    {
        uint32_t chromaCb;
        uint32_t chromaCr;

        chromaCb = srcImageU8[chromaOffset + y_chroma * processingPitch + x    ];
        chromaCr = srcImageU8[chromaOffset + y_chroma * processingPitch + x + 1];

        if (y_chroma < ((height >> 1) - 1)) // interpolate chroma vertically
        {
            chromaCb = (chromaCb + srcImageU8[chromaOffset + (y_chroma + 1) * processingPitch + x    ] + 1) >> 1;
            chromaCr = (chromaCr + srcImageU8[chromaOffset + (y_chroma + 1) * processingPitch + x + 1] + 1) >> 1;
        }

        yuv101010Pel[0] |= (chromaCb << 12);
        yuv101010Pel[0] |= (chromaCr << 22);

        yuv101010Pel[1] |= (chromaCb << 12);
        yuv101010Pel[1] |= (chromaCr << 22);
    }
    else
    {
        yuv101010Pel[0] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x    ] << 12);
        yuv101010Pel[0] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x + 1] << 22);

        yuv101010Pel[1] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x    ] << 12);
        yuv101010Pel[1] |= ((uint32_t)srcImageU8[chromaOffset + y_chroma * processingPitch + x + 1] << 22);
    }

    // this steps performs the color conversion
    uint32_t yuvi[6];
    float red[2], green[2], blue[2];

    yuvi[0] = (yuv101010Pel[0] &   0x3FF);
    yuvi[1] = ((yuv101010Pel[0] >>  10)       & 0x3FF);
    yuvi[2] = ((yuv101010Pel[0] >> 20) & 0x3FF);

    yuvi[3] = (yuv101010Pel[1] &   0x3FF);
    yuvi[4] = ((yuv101010Pel[1] >>  10)       & 0x3FF);
    yuvi[5] = ((yuv101010Pel[1] >> 20) & 0x3FF);

    // YUV to RGB Transformation conversion
   	float luma[2], chromaCb[2], chromaCr[2];

	// Prepare for hue adjustment
    luma[0]     = (float)yuvi[0];
    chromaCb[0] = (float)((int32_t)yuvi[1] - 512.0f);
    chromaCr[0] = (float)((int32_t)yuvi[2] - 512.0f);

	luma[1]     = (float)yuvi[3];
    chromaCb[1] = (float)((int32_t)yuvi[4] - 512.0f);
    chromaCr[1] = (float)((int32_t)yuvi[5] - 512.0f);

    // Convert YUV To RGB with hue adjustment
    red[0]  = (luma[0] * 1.1644f) +						       (chromaCr[0] * 1.5960f);           
    green[0]= (luma[0] * 1.1644f) + (chromaCb[0] * -0.3918f) + (chromaCr[0] * -0.8130f);            
    blue[0] = (luma[0] * 1.1644f) + (chromaCb[0] * 2.0172f);

	red[1]  = (luma[1] * 1.1644f) +						       (chromaCr[1] * 1.5960f);           
    green[1]= (luma[1] * 1.1644f) + (chromaCb[1] * -0.3918f) + (chromaCr[1] * -0.8130f);            
    blue[1] = (luma[1] * 1.1644f) + (chromaCb[1] * 2.0172f);


    // Clamp the results to RGB to 10 bits
	if(red[0]<0.0f)   red[0]   = 0.0f;	if(red[0]>1023.0f)   red[0]   = 1023.0f;
	if(green[0]<0.0f) green[0] = 0.0f;	if(green[0]>1023.0f) green[0] = 1023.0f;
	if(blue[0]<0.0f)  blue[0]  = 0.0f;	if(blue[0]>1023.0f)  blue[0]  = 1023.0f;

	if(red[1]<0.0f)   red[1]   = 0.0f;	if(red[1]>1023.0f)   red[1]   = 1023.0f;
	if(green[1]<0.0f) green[1] = 0.0f;	if(green[1]>1023.0f) green[1] = 1023.0f;
	if(blue[1]<0.0f)  blue[1]  = 0.0f;	if(blue[1]>1023.0f)  blue[1]  = 1023.0f;
		
	// copy to destination image
	uint32_t offset = (y * dstImagePitch) + (x * 3);
	
	//uint32_t offset = (y * dstImagePitch) + (blockIdx.x * blockDim.x + threadIdx.x) * 6;

	dstImage[offset + 0] = (uint8_t)((uint32_t)red[0] >> 2);
	dstImage[offset + 1] = (uint8_t)((uint32_t)green[0] >> 2);
	dstImage[offset + 2] = (uint8_t)((uint32_t)blue[0] >> 2);

	dstImage[offset + 3] = (uint8_t)((uint32_t)red[1] >> 2);
	dstImage[offset + 4] = (uint8_t)((uint32_t)green[1] >> 2);
	dstImage[offset + 5] = (uint8_t)((uint32_t)blue[1] >> 2);

    __syncthreads();  // wait for all threads to complete
}

extern "C"
void cuda_NV12ToRGB(CUdeviceptr nv12ImagePtr, CUdeviceptr rgbImagePtr,  
					  uint32_t width,  uint32_t height, uint32_t nv12Pitch, uint32_t rgbPitch)
{
	uint32_t * srcImage = (uint32_t*)nv12ImagePtr;
    uint8_t * dstImage = (uint8_t*)rgbImagePtr;
       
	dim3 threadsPerBlock(32,16);  // 32x16 = 512 threads per block

	// NOTE the 2 here ------V   and here ------------V which are there because each thread processes 2 pixels at a time 
	dim3 numBlocks((width + (2*threadsPerBlock.x)-1)/(2*threadsPerBlock.x), (height + threadsPerBlock.y-1)/threadsPerBlock.y); 
	NV12ToRGB<<<numBlocks,threadsPerBlock>>>(srcImage,nv12Pitch,dstImage,rgbPitch,width,height);
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////


__global__ void CudaToGpuMat(uint32_t *destImage, uint32_t *sourceImage, uint32_t width, uint32_t height)
{
    // destImage should be a pointer to the GpuMat data (i.e. GpuMat.ptr())
    // sourceImage should be a pointer to an RGBA image in GPU memory (BGRA format)
    // width is the width in pixels
    // height is the height in pixels

    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x < width && y < height)
    {
        uint32_t idx = y * width + x;

		// convert from RGBA to BGRA
		uint32_t value = sourceImage[idx];		
		
		uint8_t *ptr = (uint8_t*)&value;

		uint8_t red = ptr[0];
		uint8_t green = ptr[1];
		uint8_t blue = ptr[2];
		uint8_t alpha = ptr[3];

		ptr[0] = blue;
		ptr[1] = green;
		ptr[2] = red;
		ptr[3] = alpha;
		
        destImage[idx] = value;
    }
}

extern "C"
void cuda_CudaToGpuMat(CUdeviceptr GpuMatDataPtr, CUdeviceptr CudaImagePtr,  uint32_t width,  uint32_t height)
{
	uint32_t * srcImage = (uint32_t*)CudaImagePtr;
    uint32_t * dstImage = (uint32_t*)GpuMatDataPtr;
       
	dim3 threadsPerBlock(32,16);  // 32x16 = 512 threads per block
	dim3 numBlocks((width + threadsPerBlock.x-1)/threadsPerBlock.x, (height + threadsPerBlock.y-1)/threadsPerBlock.y); 
	CudaToGpuMat<<<numBlocks,threadsPerBlock>>>(dstImage,srcImage,width,height);
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////

__global__ void NV12PanelToComposite(uint8_t* PanelPtr, uint8_t* CompositePtr,			// gpu memory pointers to panel image and composite image
										uint32_t PanelWidth, uint32_t PanelHeight,			// pixel width/height of panel
										uint32_t CompositeWidth, uint32_t CompositeHeight,	// pixel width/height of composite
										uint32_t PanelRow, uint32_t PanelColumn)			// target location of panel within composite
{
	uint32_t x = blockIdx.x * blockDim.x + threadIdx.x; // column of pixel inside panel
    uint32_t y = blockIdx.y * blockDim.y + threadIdx.y; // row of pixel inside panel

	// reassign variables just to shorten the name for convenience
	uint32_t r = PanelRow;			// row of panel inside composite
	uint32_t c = PanelColumn;		// column of panel inside composite
	uint32_t WP = PanelWidth;		// pixel width of panel
	uint32_t HP = PanelHeight;		// pixel height of panel
	uint32_t WC = CompositeWidth;	// pixel width of composite
	uint32_t HC = CompositeHeight;	// pixel height of composite

	uint32_t X = c * WP + x;		// column of pixel inside composite
	uint32_t Y = r * HP + y;		// row of pixel inside composite

	uint32_t n = (y * WP) + x;		// index into Luma array of panel
	uint32_t N = (Y * WC) + X;		// index into Luma array of composite

	uint8_t* LP = PanelPtr;			// pointer to start of Luma array of panel
	uint8_t* LC = CompositePtr;     // pointer to start of Luma array of composite
	uint8_t* CP = LP + (WP * HP);	// pointer to start of Chroma array of panel
	uint8_t* CC = LC + (WC * WP);	// pointer to start of Chroma array of composite

	
	LC[N] = LP[n];	// copy Luma value from panel to composite

	if((y%2) == 0)	// only on even rows, copy Chroma from panel to composite (only half as many Chroma rows as Luma rows)
	{
		uint32_t nc = (WP * HP) + (y/2 * WP) + x;	// index into Chroma array of panel
		uint32_t Nc = (WC * HC) + (Y/2 * WC) + X;	// index into Chroma array of composite

		CC[Nc] = CP[nc];	// copy Chroma value from panel to composite
	}

	__syncthreads();	// wait for all threads to complete that are copying the panel to the composite
}

extern "C"
void cuda_NV12PanelToComposite(CUdeviceptr PanelCUPtr, CUdeviceptr CompositeCUPtr,  
							   uint32_t PanelWidth,  uint32_t PanelHeight,
							   uint32_t CompositeWidth, uint32_t CompositeHeight,
							   uint32_t PanelRow, uint32_t PanelColumn)
{
	uint8_t * PanelPtr = (uint8_t*)PanelCUPtr;
    uint8_t * CompositePtr = (uint8_t*)CompositeCUPtr;
       
	dim3 threadsPerBlock(32,16);  // 32x16 = 512 threads per block
	dim3 numBlocks((PanelWidth + threadsPerBlock.x-1)/threadsPerBlock.x, (PanelHeight + threadsPerBlock.y-1)/threadsPerBlock.y); 
	NV12PanelToComposite<<<numBlocks,threadsPerBlock>>>(PanelPtr,CompositePtr,PanelWidth,PanelHeight,
													    CompositeWidth,CompositeHeight,PanelRow,PanelColumn);
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////

__global__ void CopyCudaArrayToD3D9Texture(uint8_t *dest, uint8_t *source, uint16_t pitch, uint16_t width, uint16_t height)
{
	// calc x,y position of pixel to operate on
	uint32_t x = blockIdx.x * blockDim.x + threadIdx.x; // column of pixel inside panel
    uint32_t y = blockIdx.y * blockDim.y + threadIdx.y; // row of pixel inside panel

	// make sure we don't try to operate outside the image
	if(x>=width) return;
	if(y>=height) return;

	// calc position of pixel in cuda array (remember that pitch may not equal width)
	//uint32_t nD = ((height-y)*pitch) + (x*4);
	uint32_t nD = (y*pitch) + (x * 4);
	uint32_t nS = (y*width*4) + (x*4);

	// copy data
	dest[nD]   = source[nS];
	dest[nD+1] = source[nS+1];
	dest[nD+2] = source[nS+2];
	dest[nD+3] = source[nS+3];	
}


extern "C"
void cuda_CopyCudaArrayToD3D9Texture(CUdeviceptr pDest, CUdeviceptr pSource, uint16_t pitch, uint16_t width, uint16_t height, cudaStream_t stream)
{

	uint8_t* dest = (uint8_t*)pDest;
	uint8_t* source = (uint8_t*)pSource;

	//dim3 threadsPerBlock(32,16);  // 32x16 = 512 threads per block	
	dim3 threadsPerBlock(32, 32);  // 32x16 = 512 threads per block	
	dim3 numBlocks((width + threadsPerBlock.x-1)/threadsPerBlock.x, (height + threadsPerBlock.y-1)/threadsPerBlock.y); 

	CopyCudaArrayToD3D9Texture<<<numBlocks,threadsPerBlock,0,stream>>>(dest,source,pitch,width,height);		

}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////

