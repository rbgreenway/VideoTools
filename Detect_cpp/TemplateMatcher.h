#pragma once



#include <stdint.h>
#include <stdlib.h>
#include <vector>

// OpenCV headers
#include "opencv2/core.hpp"
#include "opencv2/core/utility.hpp"
#include "opencv2\imgproc.hpp"
#include "opencv2\video.hpp"

#include "opencv2/core/core.hpp" 
#include "opencv2/features2d/features2d.hpp" 
#include "opencv2/calib3d/calib3d.hpp"
#include "opencv2\features2d.hpp"
#include "opencv2\xfeatures2d.hpp"
#include "opencv2\xfeatures2d\nonfree.hpp"

using namespace std;
using namespace cv;



class TemplateMatcher
{
public:
	TemplateMatcher();
	~TemplateMatcher();
	void SetReferenceROI(uint8_t *refImageData, int width, int height, int roiX, int roiY, int roiW, int roiH);
	double CalculateImageCorrelation(uint8_t *testImageData);

private:
	Mat m_roiRefMat;  // this holds the ROI patch from the reference image

	uint32_t m_imageWidth;
	uint32_t m_imageHeight;
	uint32_t m_roiX;
	uint32_t m_roiY;
	uint32_t m_roiW;
	uint32_t m_roiH;
	bool m_ready;
};

