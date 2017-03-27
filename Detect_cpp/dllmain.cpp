// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"

#define DllExport   __declspec( dllexport ) 

static SurfDetector* pSurf;
static AkazeDetector* pAkaze;
static TemplateMatcher* pTMatcher;
static bool Ready = false;

BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:		
		break;
	}
	return TRUE;
}

extern "C" DllExport void CreateDetectors()
{	
	if (!Ready)
	{
		pSurf = new SurfDetector();
		pAkaze = new AkazeDetector();
		pTMatcher = new TemplateMatcher();

		Ready = true;
	}
}

extern "C" DllExport void Shutdown()
{
	if (pSurf != 0) delete pSurf;
	if (pAkaze != 0) delete pAkaze;
	if (pTMatcher != 0) delete pTMatcher;
	Ready = false;
}


extern "C" DllExport int SetReferenceImage(uint8_t *pImagedata, uint32_t imageWidth, uint32_t imageHeight,
	uint32_t roiX, uint32_t roiY, uint32_t roiW, uint32_t roiH)
{
	if (Ready)
	{
		pSurf->SetReferenceImage(pImagedata, imageWidth, imageHeight, roiX, roiY, roiW, roiH);
		pAkaze->SetReferenceImage(pImagedata, imageWidth, imageHeight, roiX, roiY, roiW, roiH);
		pTMatcher->SetReferenceROI(pImagedata, imageWidth, imageHeight, roiX, roiY, roiW, roiH);
	}
	else
	{
		return -1;
	}
}

extern "C" DllExport double Surf_FindMatchDistance(uint8_t *pImageData)
{
	if (Ready)
	{
		return pSurf->FindMatchDistance(pImageData);
	}
	else return -1.0;
}


extern "C" DllExport double Surf_CalcImageCorrelation(uint8_t *pImageData)
{
	if (Ready)
	{
		return pAkaze->CalculateImageCorrelation(pImageData);
	}
	else return -1.0;
}


extern "C" DllExport double Akaze_FindMatchDistance(uint8_t *pImageData)
{
	if (Ready)
	{
		return pAkaze->FindMatchDistance(pImageData);
	}
	else return -1.0;
}


extern "C" DllExport double Akaze_CalcImageCorrelation(uint8_t *pImageData)
{
	if (Ready)
	{
		return pAkaze->CalculateImageCorrelation(pImageData);
	}
	else return -1.0;
}


extern "C" DllExport double TemplateMatch_CalculateImageCorrelation(uint8_t *testImageData)
{
	if (Ready)
	{
		return pTMatcher->CalculateImageCorrelation(testImageData);
	}
	else return -1.0;
}

extern "C" DllExport int Test()
{
	if (Ready)
		return 1;
	else
		return -1;
}