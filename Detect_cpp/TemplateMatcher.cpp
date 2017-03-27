#include "stdafx.h"
#include "TemplateMatcher.h"


TemplateMatcher::TemplateMatcher()
{
	m_ready = false;
}


TemplateMatcher::~TemplateMatcher()
{
}


void TemplateMatcher::SetReferenceROI(uint8_t *refImageData, int width, int height, int roiX, int roiY, int roiW, int roiH)
{
	// since the full reference image was sent, we pull out the ROI subimage
	int padding = 4;

	m_roiX = roiX;
	m_roiY = roiY;
	m_roiW = roiW;
	m_roiH = roiH;
	m_imageWidth = width;
	m_imageHeight = height;

	Mat fullRefMat = Mat(height, width, CV_8UC4, refImageData);

	m_roiRefMat = Mat(fullRefMat, cv::Rect(roiX + padding, roiY + padding, roiW -(2*padding), roiH - (2*padding)));


	m_ready = true;
}

double TemplateMatcher::CalculateImageCorrelation(uint8_t *testImageData)
{
	// NOTE:  must call SetReferenceROI function before calling this one!

	// image = the reference image of which to compare
	// patch = the template image that is compared to the image to see if it matches.  The patch image must be less than or equal
	//         in size to image.  If it is smaller in size, it is moved around over image to find the best correlation.

	double correlationValue = 0;

	if (m_ready)
	{

		Mat fullTestMat = Mat(m_imageHeight, m_imageWidth, CV_8UC4, testImageData);
		Mat roiTestMat = Mat(fullTestMat, cv::Rect(m_roiX, m_roiY, m_roiW, m_roiH));

		// Available OpenCV matching methods:
		// CV_TM_SQDIFF, CV_TM_SQDIFF_NORMED, CV_TM_CCORR, CV_TM_CCORR_NORMED, CV_TM_CCOEFF, CV_TM_CCOEFF_NORMED
		int match_method = CV_TM_CCORR_NORMED;


		/// Create the result matrix
		Mat result;
		int result_cols = roiTestMat.cols - m_roiRefMat.cols + 1;
		int result_rows = roiTestMat.rows - m_roiRefMat.rows + 1;
		result.create(result_rows, result_cols, CV_32FC1);


		/// Do the Matching and Normalize
		matchTemplate(roiTestMat, m_roiRefMat, result, match_method);
		normalize(result, result, 0, 1, NORM_MINMAX, -1, Mat());


		/// Localizing the best match with minMaxLoc
		double minVal; double maxVal; Point minLoc; Point maxLoc;
		minMaxLoc(result, &minVal, &maxVal, &minLoc, &maxLoc, Mat());


		/// For SQDIFF and SQDIFF_NORMED, the best matches are lower values. For all the other methods, the higher the better
		if (match_method == CV_TM_SQDIFF || match_method == CV_TM_SQDIFF_NORMED)
		{
			correlationValue = minVal;
		}
		else
		{
			correlationValue = maxVal;
		}
	}
	else
		correlationValue = -1.0f;  // SetReferenceImage has not been called yet
	

	return correlationValue;
}