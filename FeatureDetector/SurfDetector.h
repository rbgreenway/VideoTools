#pragma once

#include <stdint.h>
#include <stdlib.h>

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

using namespace cv;
using namespace cv::xfeatures2d;


struct SURFDetector
{
    Ptr<Feature2D> surf;
    SURFDetector(double hessian = 800.0)
    {
        surf =  SURF::create(hessian);
    }
    template<class T>
    void operator()(const T& in, const T& mask, std::vector<cv::KeyPoint>& pts, T& descriptors, bool useProvided = false)
    {
        surf->detectAndCompute(in, mask, pts, descriptors, useProvided);
    }
};

template<class KPMatcher>
struct SURFMatcher
{
    KPMatcher matcher;
    template<class T>
    void match(const T& in1, const T& in2, std::vector<cv::DMatch>& matches)
    {
        matcher.match(in1, in2, matches);
    }
};


class SurfDetector
{
public:
	SurfDetector(void);
	~SurfDetector(void);

	int SetReferenceImage(uint8_t *pImagedata, uint32_t imageWidth, uint32_t imageHeight, 
						   uint32_t roiX, uint32_t roiY, uint32_t roiW, uint32_t roiH);

	double FindMatchDistance(uint8_t *pImagedata);

	double CalculateImageCorrelation(uint8_t* pImage);

private:
	cv::Mat m_refImage;
	cv::Mat m_roiImage;	
	cv::Mat m_refDescriptors;
	std::vector<KeyPoint> m_refKeypoints;

	//instantiate detectors/matchers
	SURFDetector m_surf;
	SURFMatcher<BFMatcher>  m_matcher;
	//FlannBasedMatcher m_matcher;

	uint32_t m_imageWidth;
	uint32_t m_imageHeight;
	uint32_t m_roiX;
	uint32_t m_roiY;
	uint32_t m_roiW;
	uint32_t m_roiH;
};



