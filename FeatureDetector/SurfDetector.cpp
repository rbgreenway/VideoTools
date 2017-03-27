#include "stdafx.h"
#include "SurfDetector.h"





SurfDetector::SurfDetector(void)
{

}


SurfDetector::~SurfDetector(void)
{
	
}


int SurfDetector::SetReferenceImage(uint8_t *pImagedata, uint32_t imageWidth, uint32_t imageHeight, 
						   uint32_t roiX, uint32_t roiY, uint32_t roiW, uint32_t roiH)
{
	// returns the number of keypoints found

	uint32_t bytesPerPixel = 4;
	size_t size = imageWidth * imageHeight * bytesPerPixel;
	size_t sizeRoi = roiW * roiH * bytesPerPixel;

	m_imageWidth = imageWidth;
	m_imageHeight = imageHeight;
	m_roiX = roiX;
	m_roiY = roiY;
	m_roiW = roiW;
	m_roiH = roiH;


	// build mats from reference image
	m_refImage = cv::Mat(imageHeight,imageWidth,CV_8UC4,pImagedata);
	m_roiImage = m_refImage(cv::Rect(roiX,roiY,roiW,roiH));

	// find reference descriptors	
	m_surf(m_roiImage, Mat(), m_refKeypoints, m_refDescriptors);

	int num = m_refKeypoints.size();

	return num;

}


double SurfDetector::FindMatchDistance(uint8_t *pImagedata)
{
	// build mats from test image
	cv::Mat testImage = cv::Mat(m_imageHeight,m_imageWidth,CV_8UC4,pImagedata);
	cv::Mat testRoiImage  = testImage(cv::Rect(m_roiX,m_roiY,m_roiW,m_roiH));	

	//  SURF Calcs
	//declare input/output
	std::vector<KeyPoint> testKeypoints;
	std::vector<DMatch> matches;		
	Mat testDescriptors;
		


	m_surf(testRoiImage, Mat(), testKeypoints, testDescriptors);

	if(testDescriptors.rows>0)
	{
		m_matcher.match(m_refDescriptors, testDescriptors, matches);
	}

	
	// find minimum and maximum distances
		double max_dist = 0; double min_dist = 100; 
		double sum_dist = 0;
		if(matches.size()>0)
		{
			for( int i = 0; i < m_refDescriptors.rows; i++ ) 
			{ 
				double dist = matches[i].distance; 
				if( dist < min_dist ) min_dist = dist; 
				if( dist > max_dist ) max_dist = dist; 
				sum_dist += dist;
			}

			if(min_dist == 100)
			{
				min_dist++;
				min_dist--;
			}
		}

	return sum_dist;
}


double SurfDetector::CalculateImageCorrelation(uint8_t* pImagedata) //Mat image, Mat patch)
{
	// build mats from test image
	int pad = 1;
	cv::Mat testImage = cv::Mat(m_imageHeight,m_imageWidth,CV_8UC4,pImagedata);
	cv::Mat testRoiImage  = testImage(cv::Rect(m_roiX+pad,m_roiY+pad,m_roiW-(2*pad),m_roiH-(2*pad)));

    // image = the reference image of which to compare
    // patch = the template image that is compared to the image to see if it matches.  The patch image must be less than or equal
    //         in size to image.  If it is smaller in size, it is moved around over image to find the best correlation.

    double correlationValue = 0;

    // Available OpenCV matching methods:
    // CV_TM_SQDIFF, CV_TM_SQDIFF_NORMED, CV_TM_CCORR, CV_TM_CCORR_NORMED, CV_TM_CCOEFF, CV_TM_CCOEFF_NORMED
    int match_method = CV_TM_CCOEFF;  // DON'T USE ANY OF THE NORMED METHODS HERE!!


    /// Create the result matrix
    Mat result;
	int result_cols = m_roiImage.cols - testRoiImage.cols + 1;
    int result_rows = m_roiImage.rows - testRoiImage.rows + 1;
    result.create( result_rows, result_cols, CV_32FC1 );


	/// convert to grayscale
	Mat grayRef, grayTest;

	cvtColor(m_roiImage, grayRef, CV_BGRA2GRAY); 
	cvtColor(testRoiImage, grayTest, CV_BGRA2GRAY);

    /// Do the Matching and Normalize
    matchTemplate( grayRef, grayTest, result, match_method );
    //normalize( result, result, 0, 1, NORM_MINMAX, -1, Mat() );

	/*double m[100];
	for(int r=0;r<result.rows;r++)
		for(int c=0;c<result.cols;c++)
		{
			int i = r*3+c;
			m[i] = result.at<float>(r,c);
		}*/


    /// Localizing the best match with minMaxLoc
    double minVal; double maxVal; Point minLoc; Point maxLoc;
    minMaxLoc( result, &minVal, &maxVal, &minLoc, &maxLoc, Mat() );


    /// For SQDIFF and SQDIFF_NORMED, the best matches are lower values. For all the other methods, the higher the better
    if( match_method  == CV_TM_SQDIFF || match_method == CV_TM_SQDIFF_NORMED )
      { correlationValue = minVal; }
    else
      { correlationValue = maxVal; }


    return correlationValue;
}

